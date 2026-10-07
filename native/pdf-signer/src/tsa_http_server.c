#include <pthread.h>
#include <string.h>
#include "tsa_server.h"
#include <unistd.h>
#include <arpa/inet.h>
#include <errno.h>
#include <netinet/in.h>
#include <poll.h>
#include <stdio.h>
#include <sys/socket.h>
#include <stdlib.h>
#include <strings.h>
#include <sys/time.h>

#define TSA_HTTP_WORKER_COUNT 8
#define TSA_HTTP_QUEUE_SIZE 256
#define TSA_HTTP_MAX_HEADER_SIZE 8192
#define TSA_HTTP_MAX_REQUEST_SIZE 65536

/*------------------INTERNAL------------------------*/
typedef struct
{
  int    items[TSA_HTTP_QUEUE_SIZE];
  size_t head;
  size_t tail;
  size_t count;

  pthread_mutex_t mutex;
  pthread_cond_t  not_empty;
  pthread_cond_t  not_full;

  int shutting_down;
} tsa_client_queue_t;

typedef struct
{
  tsa_client_queue_t* queue;
  tsa_server_t*       server;
  size_t              worker_index;
} tsa_http_worker_t;

static int queue_init(tsa_client_queue_t* queue) {
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

static void queue_destroy(tsa_client_queue_t* queue) {
  if (!queue) {
    return;
  }
  pthread_cond_destroy(&queue->not_full);
  pthread_cond_destroy(&queue->not_empty);
  pthread_mutex_destroy(&queue->mutex);
}

static void queue_shutdown(tsa_client_queue_t* queue) {
  pthread_mutex_lock(&queue->mutex);

  queue->shutting_down = 1;

  pthread_cond_broadcast(&queue->not_empty);
  pthread_cond_broadcast(&queue->not_full);

  pthread_mutex_unlock(&queue->mutex);
}


static int queue_push(tsa_client_queue_t* queue, int client_fd) {
  pthread_mutex_lock(&queue->mutex);

  while (queue->count == TSA_HTTP_QUEUE_SIZE && !queue->shutting_down) {
    pthread_cond_wait(&queue->not_full, &queue->mutex);
  }

  if (queue->shutting_down) {
    pthread_mutex_unlock(&queue->mutex);
    return -1;
  }

  queue->items[queue->tail] = client_fd;
  queue->tail               = (queue->tail + 1) % TSA_HTTP_QUEUE_SIZE;
  queue->count++;

  pthread_cond_signal(&queue->not_empty);
  pthread_mutex_unlock(&queue->mutex);

  return 0;
}

static int queue_pop(tsa_client_queue_t* queue, int* client_fd) {
  pthread_mutex_lock(&queue->mutex);

  while (queue->count == 0 && !queue->shutting_down) {
    pthread_cond_wait(&queue->not_empty, &queue->mutex);
  }

  if (queue->count == 0 && queue->shutting_down) {
    pthread_mutex_unlock(&queue->mutex);
    return -1;
  }

  *client_fd  = queue->items[queue->head];
  queue->head = (queue->head + 1) % TSA_HTTP_QUEUE_SIZE;
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

static void send_http_error(int client_fd, int status_code, const char* reason) {
  char header[256];

  int len = snprintf(header, sizeof(header),
                     "HTTP/1.1 %d %s\r\n"
                     "Content-Length: 0\r\n"
                     "Connection: close\r\n"
                     "\r\n",
                     status_code, reason);

  if (len <= 0 || (size_t)len >= sizeof(header)) {
    return;
  }

  write_exact(client_fd, (const unsigned char*)header, (size_t)len);
}


static const char* find_header(const char* headers, const char* name) {
  size_t name_len = strlen(name);

  const char* line = headers;

  while (*line != '\0') {
    const char* line_end = strstr(line, "\r\n");

    if (!line_end) {
      break;
    }

    size_t line_len = (size_t)(line_end - line);

    if (line_len > name_len && strncasecmp(line, name, name_len) == 0 && line[name_len] == ':') {
      return line + name_len + 1;
    }

    line = line_end + 2;
  }

  return NULL;
}

static int handle_client(tsa_http_worker_t* worker, int client_fd) {
  if (!worker || client_fd < 0) {
    return -1;
  }

  char   header[TSA_HTTP_MAX_HEADER_SIZE + 1];
  size_t header_len = 0;

  int header_complete = 0;

  while (header_len < TSA_HTTP_MAX_HEADER_SIZE) {
    ssize_t n = read(client_fd, header + header_len, 1);

    if (n < 0) {
      if (errno == EINTR) {
        continue;
      }

      return -1;
    }

    if (n == 0) {
      return -1;
    }

    header_len++;

    if (header_len >= 4 && memcmp(header + header_len - 4, "\r\n\r\n", 4) == 0) {
      header_complete = 1;
      break;
    }
  }

  if (!header_complete) {
    send_http_error(client_fd, 431, "Request Header Fields Too Large");
    return -1;
  }

  header[header_len] = '\0';

  const char* first_line_end = strstr(header, "\r\n");

  if (!first_line_end) {
    send_http_error(client_fd, 400, "Bad Request");
    return -1;
  }

  if (strncmp(header, "POST ", 5) != 0) {
    send_http_error(client_fd, 405, "Method Not Allowed");
    return -1;
  }

  const char* transfer_encoding = find_header(first_line_end + 2, "Transfer-Encoding");

  if (transfer_encoding) {
    send_http_error(client_fd, 400, "Bad Request");
    return -1;
  }

  const char* content_type = find_header(first_line_end + 2, "Content-Type");

  if (!content_type) {
    send_http_error(client_fd, 400, "Bad Request");
    return -1;
  }

  while (*content_type == ' ' || *content_type == '\t') {
    content_type++;
  }

  const char* content_type_end = strstr(content_type, "\r\n");

  if (!content_type_end) {
    send_http_error(client_fd, 400, "Bad Request");
    return -1;
  }

  size_t content_type_len = (size_t)(content_type_end - content_type);

  const char expected_content_type[] = "application/timestamp-query";

  if (content_type_len != strlen(expected_content_type) ||
      strncasecmp(content_type, expected_content_type, content_type_len) != 0) {
    send_http_error(client_fd, 415, "Unsupported Media Type");
    return -1;
  }

  const char* content_length_header = find_header(first_line_end + 2, "Content-Length");

  if (!content_length_header) {
    send_http_error(client_fd, 411, "Length Required");
    return -1;
  }

  while (*content_length_header == ' ' || *content_length_header == '\t') {
    content_length_header++;
  }

  errno = 0;

  char* end = NULL;

  unsigned long content_length = strtoul(content_length_header, &end, 10);

  if (errno != 0 || end == content_length_header || content_length == 0 ||
      content_length > TSA_HTTP_MAX_REQUEST_SIZE) {
    send_http_error(client_fd, 400, "Bad Request");
    return -1;
  }

  while (*end == ' ' || *end == '\t') {
    end++;
  }

  if (end[0] != '\r' || end[1] != '\n') {
    send_http_error(client_fd, 400, "Bad Request");
    return -1;
  }

  unsigned char* request_der = malloc((size_t)content_length);
  if (!request_der) {
    send_http_error(client_fd, 500, "Internal Server Error");
    return -1;
  }

  if (read_exact(client_fd, request_der, (size_t)content_length) != 0) {
    free(request_der);
    return -1;
  }

  tsa_server_result_t result = {0};

  tsa_server_status_t status = tsa_server_create_response(
      worker->server, worker->worker_index, request_der, (size_t)content_length, &result);
  free(request_der);

  if (status != TSA_SERVER_OK) {
    send_http_error(client_fd, 500, "Internal Server Error");
    return -1;
  }

  char response_header[256];

  int response_header_len = snprintf(response_header, sizeof(response_header),
                                     "HTTP/1.1 200 OK\r\n"
                                     "Content-Type: application/timestamp-reply\r\n"
                                     "Content-Length: %zu\r\n"
                                     "Connection: close\r\n"
                                     "\r\n",
                                     result.response_der_len);

  if (response_header_len <= 0 || (size_t)response_header_len >= sizeof(response_header)) {
    tsa_server_result_dispose(&result);
    return -1;
  }

  if (write_exact(client_fd, (const unsigned char*)response_header, (size_t)response_header_len) !=
      0) {
    tsa_server_result_dispose(&result);
    return -1;
  }

  if (write_exact(client_fd, result.response_der, result.response_der_len) != 0) {
    tsa_server_result_dispose(&result);
    return -1;
  }

  tsa_server_result_dispose(&result);
  return 0;
}

static void* tsa_http_worker_main(void* arg) {
  tsa_http_worker_t* worker = arg;
  if (!worker) {
    return NULL;
  }

  for (;;) {
    int client_fd = -1;

    if (queue_pop(worker->queue, &client_fd) != 0) {
      break;
    }

    handle_client(worker, client_fd);
    close(client_fd);
  }

  return NULL;
}

/*-----------------------------------------------*/

int tsa_http_server_run(tsa_server_t* server, int shutdown_fd, unsigned short port) {
  if (!server || shutdown_fd < 0 || port == 0) {
    return -1;
  }

  tsa_client_queue_t queue;

  if (queue_init(&queue) != 0) {
    return -1;
  }

  pthread_t         threads[TSA_HTTP_WORKER_COUNT];
  tsa_http_worker_t workers[TSA_HTTP_WORKER_COUNT];

  size_t started_workers = 0;

  for (size_t i = 0; i < TSA_HTTP_WORKER_COUNT; i++) {
    workers[i].queue        = &queue;
    workers[i].server       = server;
    workers[i].worker_index = i;

    if (pthread_create(&threads[i], NULL, tsa_http_worker_main, &workers[i]) != 0) {
      queue_shutdown(&queue);

      for (size_t j = 0; j < started_workers; j++) {
        pthread_join(threads[j], NULL);
      }

      queue_destroy(&queue);
      return -1;
    }
    started_workers++;
  }

  int fd = socket(AF_INET, SOCK_STREAM, 0);

  if (fd < 0) {
    perror("socket");
    queue_shutdown(&queue);

    for (size_t i = 0; i < started_workers; i++) {
      pthread_join(threads[i], NULL);
    }

    queue_destroy(&queue);
    return -1;
  }

  int reuse = 1;

  if (setsockopt(fd, SOL_SOCKET, SO_REUSEADDR, &reuse, sizeof(reuse)) != 0) {
    perror("setsockopt");
    close(fd);
    queue_shutdown(&queue);

    for (size_t i = 0; i < started_workers; i++) {
      pthread_join(threads[i], NULL);
    }

    queue_destroy(&queue);
    return -1;
  }


  struct sockaddr_in addr = {0};

  addr.sin_family      = AF_INET;
  addr.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
  addr.sin_port        = htons(port);

  if (bind(fd, (struct sockaddr*)&addr, sizeof(addr)) != 0) {
    perror("bind");
    close(fd);
    queue_shutdown(&queue);

    for (size_t i = 0; i < started_workers; i++) {
      pthread_join(threads[i], NULL);
    }

    queue_destroy(&queue);
    return -1;
  }

  if (listen(fd, 128) != 0) {
    perror("listen");
    close(fd);
    queue_shutdown(&queue);

    for (size_t i = 0; i < started_workers; i++) {
      pthread_join(threads[i], NULL);
    }

    queue_destroy(&queue);
    return -1;
  }

  printf("TSA HTTP server listening on 127.0.0.1:%u\n", port);

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

  for (;;) {
    int poll_result = poll(fds, 2, -1);

    if (poll_result < 0) {
      if (errno == EINTR) {
        continue;
      }

      perror("poll");
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

  queue_shutdown(&queue);

  for (size_t i = 0; i < started_workers; i++) {
    pthread_join(threads[i], NULL);
  }

  queue_destroy(&queue);

  return 0;
}
