
#include "signer_protocol.h"

#include <arpa/inet.h>
#include <pthread.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/socket.h>
#include <sys/un.h>
#include <unistd.h>

#define TEST_THREAD_COUNT 32
#define REQUESTS_PER_THREAD 100

#define CHECK(cond)                                                                                \
  do {                                                                                             \
    if (!(cond)) {                                                                                 \
      fprintf(stderr, "CHECK failed: %s (%s:%d)\n", #cond, __FILE__, __LINE__);                    \
      return 1;                                                                                    \
    }                                                                                              \
  } while (0)

typedef struct
{
  uint32_t sign_mode;
  int      failed;
} worker_args_t;

static int read_exact(int fd, unsigned char* buffer, size_t len) {
  size_t total_read = 0;

  while (total_read < len) {
    ssize_t n = read(fd, buffer + total_read, len - total_read);

    if (n < 0) {
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
  size_t total_written = 0;

  while (total_written < len) {
    ssize_t n = write(fd, buffer + total_written, len - total_written);

    if (n < 0) {
      return -1;
    }

    if (n == 0) {
      return -1;
    }

    total_written += (size_t)n;
  }

  return 0;
}

static int sign_request(uint32_t sign_mode, unsigned char digest_byte) {
  int fd = socket(AF_UNIX, SOCK_STREAM, 0);
  if (fd < 0) {
    return -1;
  }

  struct sockaddr_un addr = {0};
  addr.sun_family         = AF_UNIX;

  strncpy(addr.sun_path, "/run/pdf-signer/pdf-signer.sock", sizeof(addr.sun_path) - 1);

  if (connect(fd, (struct sockaddr*)&addr, sizeof(addr)) != 0) {
    close(fd);
    return -1;
  }

  unsigned char request[SIGNER_REQUEST_SIZE] = {0};

  uint32_t version_net    = htonl(SIGNER_PROTOCOL_VERSION);
  uint32_t algorithm_net  = htonl(SIGNER_DIGEST_SHA256);
  uint32_t digest_len_net = htonl(SIGNER_SHA256_DIGEST_LEN);
  uint32_t sign_mode_net  = htonl(sign_mode);

  memcpy(request, &version_net, 4);
  memcpy(request + 4, &algorithm_net, 4);
  memcpy(request + 8, &digest_len_net, 4);
  memcpy(request + 12, &sign_mode_net, 4);

  memset(request + SIGNER_REQUEST_HEADER_SIZE, digest_byte, SIGNER_SHA256_DIGEST_LEN);

  if (write_exact(fd, request, sizeof(request)) != 0) {
    close(fd);
    return -1;
  }

  unsigned char response_header[SIGNER_RESPONSE_HEADER_SIZE];

  if (read_exact(fd, response_header, sizeof(response_header)) != 0) {
    close(fd);
    return -1;
  }

  uint32_t status_net;
  uint32_t hex_len_net;

  memcpy(&status_net, response_header, 4);
  memcpy(&hex_len_net, response_header + 4, 4);

  uint32_t status  = ntohl(status_net);
  uint32_t hex_len = ntohl(hex_len_net);

  if (status != SIGNER_STATUS_OK || hex_len == 0 || hex_len > SIGNER_RESPONSE_MAX_SIZE) {
    close(fd);
    return -1;
  }

  unsigned char* contents_hex = malloc((size_t)hex_len);

  if (!contents_hex) {
    close(fd);
    return -1;
  }

  int result = read_exact(fd, contents_hex, hex_len);

  free(contents_hex);
  close(fd);

  return result;
}

static void* worker_main(void* arg) {
  worker_args_t* worker = arg;

  for (size_t i = 0; i < REQUESTS_PER_THREAD; i++) {
    unsigned char digest_byte = (unsigned char)(i & 0xff);

    if (sign_request(worker->sign_mode, digest_byte) != 0) {
      worker->failed = 1;
      return NULL;
    }
  }

  return NULL;
}

static int run_concurrency_test(uint32_t sign_mode) {
  pthread_t     threads[TEST_THREAD_COUNT];
  worker_args_t workers[TEST_THREAD_COUNT];

  memset(workers, 0, sizeof(workers));

  for (size_t i = 0; i < TEST_THREAD_COUNT; i++) {
    workers[i].sign_mode = sign_mode;

    CHECK(pthread_create(&threads[i], NULL, worker_main, &workers[i]) == 0);
  }

  for (size_t i = 0; i < TEST_THREAD_COUNT; i++) {
    pthread_join(threads[i], NULL);
  }

  for (size_t i = 0; i < TEST_THREAD_COUNT; i++) {
    CHECK(workers[i].failed == 0);
  }

  return 0;
}

int main(void) {
  int failed = 0;

  failed += run_concurrency_test(SIGNER_MODE_PLAIN);
  failed += run_concurrency_test(SIGNER_MODE_TIMESTAMP);

  if (failed == 0) {
    printf("All signer concurrency tests passed\n");
    return 0;
  }

  fprintf(stderr, "%d signer concurrency test(s) failed\n", failed);
  return 1;
}
