#define _POSIX_C_SOURCE 200809L

#include "signer_protocol.h"

#include <arpa/inet.h>
#include <pthread.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/socket.h>
#include <sys/un.h>
#include <time.h>
#include <unistd.h>

#define THREAD_COUNT 8
#define TOTAL_REQUESTS 50000

typedef struct
{
  uint32_t sign_mode;
  size_t   requests;
  size_t   failed;
} worker_args_t;

static int read_exact(int fd, unsigned char* buffer, size_t len) {
  size_t total = 0;

  while (total < len) {
    ssize_t n = read(fd, buffer + total, len - total);

    if (n <= 0) {
      return -1;
    }

    total += (size_t)n;
  }

  return 0;
}

static int write_exact(int fd, const unsigned char* buffer, size_t len) {
  size_t total = 0;

  while (total < len) {
    ssize_t n = write(fd, buffer + total, len - total);

    if (n <= 0) {
      return -1;
    }

    total += (size_t)n;
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

  strncpy(addr.sun_path, "/tmp/pdf-signer.sock", sizeof(addr.sun_path) - 1);

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

  unsigned char* contents_hex = malloc(hex_len);

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

  for (size_t i = 0; i < worker->requests; i++) {
    if (sign_request(worker->sign_mode, (unsigned char)i) != 0) {
      worker->failed++;
    }
  }

  return NULL;
}

static double elapsed_seconds(struct timespec start, struct timespec end) {
  return (double)(end.tv_sec - start.tv_sec) + (double)(end.tv_nsec - start.tv_nsec) / 1000000000.0;
}

static int run_stress(uint32_t sign_mode, const char* name) {
  pthread_t     threads[THREAD_COUNT];
  worker_args_t workers[THREAD_COUNT];

  const size_t requests_per_thread = TOTAL_REQUESTS / THREAD_COUNT;
  const size_t remainder           = TOTAL_REQUESTS % THREAD_COUNT;

  memset(workers, 0, sizeof(workers));

  struct timespec start;
  struct timespec end;

  clock_gettime(CLOCK_MONOTONIC, &start);

  for (size_t i = 0; i < THREAD_COUNT; i++) {
    workers[i].sign_mode = sign_mode;
    workers[i].requests  = requests_per_thread + (i < remainder ? 1 : 0);

    if (pthread_create(&threads[i], NULL, worker_main, &workers[i]) != 0) {
      fprintf(stderr, "Failed to create worker thread\n");
      return 1;
    }
  }

  size_t failures = 0;

  for (size_t i = 0; i < THREAD_COUNT; i++) {
    pthread_join(threads[i], NULL);
    failures += workers[i].failed;
  }

  clock_gettime(CLOCK_MONOTONIC, &end);

  const double seconds = elapsed_seconds(start, end);
  const double rps     = (double)TOTAL_REQUESTS / seconds;

  printf("%s\n", name);
  printf("  Requests:   %d\n", TOTAL_REQUESTS);
  printf("  Threads:    %d\n", THREAD_COUNT);
  printf("  Time:       %.3f s\n", seconds);
  printf("  Throughput: %.2f req/s\n", rps);
  printf("  Failures:   %zu\n", failures);

  return failures == 0 ? 0 : 1;
}

int main(void) {
  int failed = 0;

  failed += run_stress(SIGNER_MODE_PLAIN, "Plain CMS");
  failed += run_stress(SIGNER_MODE_TIMESTAMP, "Timestamp CMS");

  return failed == 0 ? 0 : 1;
}
