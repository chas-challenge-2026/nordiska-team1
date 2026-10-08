#include "signer_server.h"

#include "pdf_sign.h"
#include "runtime_config.h"
#include "signer_protocol.h"

#include <arpa/inet.h>
#include <errno.h>
#include <poll.h>
#include <pthread.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <sys/socket.h>
#include <sys/time.h>
#include <sys/un.h>
#include <unistd.h>

#define SIGNER_WORKER_COUNT 8
#define SIGNER_QUEUE_SIZE 256

/*---------------------INTERNAL----------------------*/

typedef struct
{
  int    items[SIGNER_QUEUE_SIZE];
  size_t head;
  size_t tail;
  size_t count;

  pthread_mutex_t mutex;
  pthread_cond_t  not_empty;
  pthread_cond_t  not_full;

  int shutting_down;
} signer_client_queue_t;

typedef struct
{
  signer_client_queue_t* queue;
  pdf_signer_t*          signer;
} signer_worker_t;

static int queue_init(signer_client_queue_t* queue) {
  if (!queue) {
    return -1;
  }

  memset(queue, 0, sizeof(*queue));

  if (pthread_mutex_init(&queue->mutex, NULL) != 0) {
    return -1;
  }

  if (pthread_cond_init(&queue->not_empty, NULL) != 0) {
    pthread_mutex_destroy(&queue->mutex);
    return -1;
  }

  if (pthread_cond_init(&queue->not_full, NULL) != 0) {
    pthread_cond_destroy(&queue->not_empty);
    pthread_mutex_destroy(&queue->mutex);
    return -1;
  }

  return 0;
}

static void queue_destroy(signer_client_queue_t* queue) {
  if (!queue) {
    return;
  }

  pthread_cond_destroy(&queue->not_full);
  pthread_cond_destroy(&queue->not_empty);
  pthread_mutex_destroy(&queue->mutex);
}

static void queue_shutdown(signer_client_queue_t* queue) {
  pthread_mutex_lock(&queue->mutex);

  queue->shutting_down = 1;

  pthread_cond_broadcast(&queue->not_empty);
  pthread_cond_broadcast(&queue->not_full);

  pthread_mutex_unlock(&queue->mutex);
}

static int queue_push(signer_client_queue_t* queue, int client_fd) {
  pthread_mutex_lock(&queue->mutex);

  while (queue->count == SIGNER_QUEUE_SIZE && !queue->shutting_down) {
    pthread_cond_wait(&queue->not_full, &queue->mutex);
  }

  if (queue->shutting_down) {
    pthread_mutex_unlock(&queue->mutex);
    return -1;
  }

  queue->items[queue->tail] = client_fd;
  queue->tail               = (queue->tail + 1) % SIGNER_QUEUE_SIZE;
  queue->count++;

  pthread_cond_signal(&queue->not_empty);
  pthread_mutex_unlock(&queue->mutex);

  return 0;
}

static int queue_pop(signer_client_queue_t* queue, int* client_fd) {
  pthread_mutex_lock(&queue->mutex);

  while (queue->count == 0 && !queue->shutting_down) {
    pthread_cond_wait(&queue->not_empty, &queue->mutex);
  }

  if (queue->count == 0 && queue->shutting_down) {
    pthread_mutex_unlock(&queue->mutex);
    return -1;
  }

  *client_fd  = queue->items[queue->head];
  queue->head = (queue->head + 1) % SIGNER_QUEUE_SIZE;
  queue->count--;

  pthread_cond_signal(&queue->not_full);
  pthread_mutex_unlock(&queue->mutex);

  return 0;
}

static int read_exact(int fd, unsigned char* buffer, size_t len) {
  if (fd < 0 || (!buffer && len > 0)) {
    return -1;
  }

  size_t total_read = 0;

  while (total_read < len) {
    ssize_t n = read(fd, buffer + total_read, len - total_read);

    if (n < 0) {
      if (errno == EINTR) {
        continue;
      }

      return -1;
    }

    if (n == 0) {
      return -1;
    }

    total_read += (size_t)n;
  }

  return 0;
}

static int write_exact(int fd, const unsigned char* buffer, size_t len) {
  if (fd < 0 || (!buffer && len > 0)) {
    return -1;
  }

  size_t total_written = 0;

  while (total_written < len) {
    ssize_t n = write(fd, buffer + total_written, len - total_written);

    if (n < 0) {
      if (errno == EINTR) {
        continue;
      }

      return -1;
    }

    if (n == 0) {
      return -1;
    }

    total_written += (size_t)n;
  }

  return 0;
}

static void send_error_response(int client_fd, signer_protocol_status_t status) {
  unsigned char response_header[SIGNER_RESPONSE_HEADER_SIZE];

  uint32_t status_net  = htonl((uint32_t)status);
  uint32_t hex_len_net = htonl(0);

  memcpy(response_header, &status_net, 4);
  memcpy(response_header + 4, &hex_len_net, 4);

  write_exact(client_fd, response_header, sizeof(response_header));
}

static int handle_client(pdf_signer_t* signer, int client_fd) {
  unsigned char headers[SIGNER_REQUEST_HEADER_SIZE];

  if (read_exact(client_fd, headers, sizeof(headers)) != 0) {
    return -1;
  }

  uint32_t version_net;
  uint32_t algorithm_net;
  uint32_t digest_len_net;
  uint32_t sign_mode_net;

  memcpy(&version_net, headers, 4);
  memcpy(&algorithm_net, headers + 4, 4);
  memcpy(&digest_len_net, headers + 8, 4);
  memcpy(&sign_mode_net, headers + 12, 4);

  const uint32_t version    = ntohl(version_net);
  const uint32_t algorithm  = ntohl(algorithm_net);
  const uint32_t digest_len = ntohl(digest_len_net);
  const uint32_t sign_mode  = ntohl(sign_mode_net);

  if (version != SIGNER_PROTOCOL_VERSION) {
    send_error_response(client_fd, SIGNER_STATUS_UNSUPPORTED_VERSION);
    return 0;
  }

  if (algorithm != SIGNER_DIGEST_SHA256) {
    send_error_response(client_fd, SIGNER_STATUS_UNSUPPORTED_ALGORITHM);
    return 0;
  }

  if (digest_len != SIGNER_SHA256_DIGEST_LEN) {
    send_error_response(client_fd, SIGNER_STATUS_INVALID_REQUEST);
    return 0;
  }

  if (sign_mode != SIGNER_MODE_PLAIN && sign_mode != SIGNER_MODE_TIMESTAMP) {
    send_error_response(client_fd, SIGNER_STATUS_INVALID_REQUEST);
    return 0;
  }

  signer_request_t req = {
      .version          = version,
      .digest_algorithm = algorithm,
      .digest_len       = digest_len,
      .sign_mode        = sign_mode,
  };

  if (read_exact(client_fd, req.digest, SIGNER_REQUEST_DIGEST_SIZE) != 0) {
    return -1;
  }

  pdf_sign_request_t sign_req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = req.digest,
      .digest_len       = req.digest_len,
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status;

  if (req.sign_mode == SIGNER_MODE_TIMESTAMP) {
    status = pdf_signer_sign_with_timestamp(signer, &sign_req, &result);
  } else {
    status = pdf_signer_sign(signer, &sign_req, &result);
  }

  if (status != PDF_SIGN_OK) {
    send_error_response(client_fd, SIGNER_STATUS_SIGNING_ERROR);
    pdf_sign_result_dispose(&result);
    return -1;
  }

  if (result.contents_hex_len == 0 || result.contents_hex_len > SIGNER_RESPONSE_MAX_SIZE ||
      result.contents_hex_len > UINT32_MAX) {
    send_error_response(client_fd, SIGNER_STATUS_INTERNAL_ERROR);
    pdf_sign_result_dispose(&result);
    return -1;
  }

  unsigned char response_header[SIGNER_RESPONSE_HEADER_SIZE];

  uint32_t status_net  = htonl(SIGNER_STATUS_OK);
  uint32_t hex_len_net = htonl((uint32_t)result.contents_hex_len);

  memcpy(response_header, &status_net, 4);
  memcpy(response_header + 4, &hex_len_net, 4);

  if (write_exact(client_fd, response_header, sizeof(response_header)) != 0) {
    pdf_sign_result_dispose(&result);
    return -1;
  }

  if (write_exact(client_fd, (const unsigned char*)result.contents_hex, result.contents_hex_len) !=
      0) {
    pdf_sign_result_dispose(&result);
    return -1;
  }

  pdf_sign_result_dispose(&result);

  return 0;
}

static void* signer_worker_main(void* arg) {
  signer_worker_t* worker = arg;

  if (!worker) {
    return NULL;
  }

  for (;;) {
    int client_fd = -1;

    if (queue_pop(worker->queue, &client_fd) != 0) {
      break;
    }

    handle_client(worker->signer, client_fd);
    close(client_fd);
  }

  return NULL;
}

/*---------------------------------------------------*/

int signer_server_run(int shutdown_fd) {
  if (shutdown_fd < 0) {
    return 1;
  }

  signer_client_queue_t queue;

  if (queue_init(&queue) != 0) {
    return 1;
  }

  pthread_t       threads[SIGNER_WORKER_COUNT];
  signer_worker_t workers[SIGNER_WORKER_COUNT];
  pdf_signer_t*   signers[SIGNER_WORKER_COUNT] = {0};

  size_t created_signers = 0;
  size_t started_workers = 0;

  for (size_t i = 0; i < SIGNER_WORKER_COUNT; i++) {
    if (pdf_signer_create(&signers[i]) != PDF_SIGN_OK) {
      queue_shutdown(&queue);

      for (size_t j = 0; j < started_workers; j++) {
        pthread_join(threads[j], NULL);
      }

      for (size_t j = 0; j < created_signers; j++) {
        pdf_signer_destroy(signers[j]);
      }

      queue_destroy(&queue);
      return 1;
    }

    created_signers++;

    workers[i].queue  = &queue;
    workers[i].signer = signers[i];

    if (pthread_create(&threads[i], NULL, signer_worker_main, &workers[i]) != 0) {
      queue_shutdown(&queue);

      for (size_t j = 0; j < started_workers; j++) {
        pthread_join(threads[j], NULL);
      }

      for (size_t j = 0; j < created_signers; j++) {
        pdf_signer_destroy(signers[j]);
      }

      queue_destroy(&queue);
      return 1;
    }

    started_workers++;
  }

  int fd = socket(AF_UNIX, SOCK_STREAM, 0);

  if (fd < 0) {
    perror("socket");

    queue_shutdown(&queue);

    for (size_t i = 0; i < started_workers; i++) {
      pthread_join(threads[i], NULL);
    }

    for (size_t i = 0; i < created_signers; i++) {
      pdf_signer_destroy(signers[i]);
    }

    queue_destroy(&queue);
    return 1;
  }

  struct sockaddr_un addr = {0};
  addr.sun_family         = AF_UNIX;

  const char* socket_path = runtime_config_signer_socket();

  if (strlen(socket_path) >= sizeof(addr.sun_path)) {
    close(fd);

    queue_shutdown(&queue);

    for (size_t i = 0; i < started_workers; i++) {
      pthread_join(threads[i], NULL);
    }

    for (size_t i = 0; i < created_signers; i++) {
      pdf_signer_destroy(signers[i]);
    }

    queue_destroy(&queue);
    return 1;
  }

  strncpy(addr.sun_path, socket_path, sizeof(addr.sun_path) - 1);

  unlink(addr.sun_path);

  if (bind(fd, (struct sockaddr*)&addr, sizeof(addr)) != 0) {
    perror("bind");
    close(fd);

    queue_shutdown(&queue);

    for (size_t i = 0; i < started_workers; i++) {
      pthread_join(threads[i], NULL);
    }

    for (size_t i = 0; i < created_signers; i++) {
      pdf_signer_destroy(signers[i]);
    }

    queue_destroy(&queue);
    return 1;
  }

  if (listen(fd, 128) != 0) {
    perror("listen");

    close(fd);
    unlink(addr.sun_path);

    queue_shutdown(&queue);

    for (size_t i = 0; i < started_workers; i++) {
      pthread_join(threads[i], NULL);
    }

    for (size_t i = 0; i < created_signers; i++) {
      pdf_signer_destroy(signers[i]);
    }

    queue_destroy(&queue);
    return 1;
  }

  printf("Signer service listening on %s with %d workers\n", addr.sun_path, SIGNER_WORKER_COUNT);

  struct pollfd fds[2] = {
      {
          .fd     = fd,
          .events = POLLIN,
      },
      {
          .fd     = shutdown_fd,
          .events = POLLIN,
      },
  };

  int failed = 0;

  for (;;) {
    int poll_result = poll(fds, 2, -1);

    if (poll_result < 0) {
      if (errno == EINTR) {
        continue;
      }

      perror("poll");
      failed = 1;
      break;
    }

    if (fds[1].revents & POLLIN) {
      break;
    }

    if (!(fds[0].revents & POLLIN)) {
      continue;
    }

    int client_fd = accept(fd, NULL, NULL);

    if (client_fd < 0) {
      if (errno == EINTR) {
        continue;
      }

      perror("accept");
      failed = 1;
      break;
    }

    struct timeval timeout = {
        .tv_sec  = 5,
        .tv_usec = 0,
    };

    if (setsockopt(client_fd, SOL_SOCKET, SO_RCVTIMEO, &timeout, sizeof(timeout)) != 0) {
      close(client_fd);
      continue;
    }

    if (setsockopt(client_fd, SOL_SOCKET, SO_SNDTIMEO, &timeout, sizeof(timeout)) != 0) {
      close(client_fd);
      continue;
    }

    if (queue_push(&queue, client_fd) != 0) {
      close(client_fd);
      break;
    }
  }

  close(fd);
  unlink(addr.sun_path);

  queue_shutdown(&queue);

  for (size_t i = 0; i < started_workers; i++) {
    pthread_join(threads[i], NULL);
  }

  for (size_t i = 0; i < created_signers; i++) {
    pdf_signer_destroy(signers[i]);
  }

  queue_destroy(&queue);

  return failed;
}
