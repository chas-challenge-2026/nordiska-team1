#include "signer_protocol.h"

#include <arpa/inet.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/socket.h>
#include <sys/un.h>
#include <unistd.h>

#define CHECK(cond)                                                                                \
  do {                                                                                             \
    if (!(cond)) {                                                                                 \
      fprintf(stderr, "CHECK failed: %s (%s:%d)\n", #cond, __FILE__, __LINE__);                    \
      return 1;                                                                                    \
    }                                                                                              \
  } while (0)

static int read_exact(int fd, unsigned char* buffer, size_t len) {
  size_t total_read = 0;

  while (total_read < len) {
    ssize_t n = read(fd, buffer + total_read, len - total_read);

    if (n <= 0) {
      return 1;
    }

    total_read += (size_t)n;
  }

  return 0;
}

static int write_exact(int fd, const unsigned char* buffer, size_t len) {
  size_t total_written = 0;

  while (total_written < len) {
    ssize_t n = write(fd, buffer + total_written, len - total_written);

    if (n <= 0) {
      return 1;
    }

    total_written += (size_t)n;
  }

  return 0;
}

static int test_sign_request_mode(uint32_t sign_mode) {
  int fd = socket(AF_UNIX, SOCK_STREAM, 0);
  CHECK(fd >= 0);

  struct sockaddr_un addr = {0};
  addr.sun_family         = AF_UNIX;

  strncpy(addr.sun_path, "/tmp/pdf-signer.sock", sizeof(addr.sun_path) - 1);

  CHECK(connect(fd, (struct sockaddr*)&addr, sizeof(addr)) == 0);

  unsigned char request[SIGNER_REQUEST_SIZE] = {0};

  uint32_t version_net    = htonl(SIGNER_PROTOCOL_VERSION);
  uint32_t algorithm_net  = htonl(SIGNER_DIGEST_SHA256);
  uint32_t digest_len_net = htonl(SIGNER_SHA256_DIGEST_LEN);
  uint32_t sign_mode_net  = htonl(sign_mode);

  memcpy(request, &version_net, 4);
  memcpy(request + 4, &algorithm_net, 4);
  memcpy(request + 8, &digest_len_net, 4);
  memcpy(request + 12, &sign_mode_net, 4);

  memset(request + SIGNER_REQUEST_HEADER_SIZE, 0x42, SIGNER_SHA256_DIGEST_LEN);

  CHECK(write_exact(fd, request, sizeof(request)) == 0);

  unsigned char response_header[SIGNER_RESPONSE_HEADER_SIZE];

  CHECK(read_exact(fd, response_header, sizeof(response_header)) == 0);

  uint32_t status_net;
  uint32_t hex_len_net;

  memcpy(&status_net, response_header, 4);
  memcpy(&hex_len_net, response_header + 4, 4);

  uint32_t status  = ntohl(status_net);
  uint32_t hex_len = ntohl(hex_len_net);

  CHECK(status == SIGNER_STATUS_OK);
  CHECK(hex_len > 0);
  CHECK(hex_len <= SIGNER_RESPONSE_MAX_SIZE);

  unsigned char* contents_hex = malloc((size_t)hex_len + 1);
  CHECK(contents_hex != NULL);

  CHECK(read_exact(fd, contents_hex, hex_len) == 0);

  contents_hex[hex_len] = '\0';

  for (uint32_t i = 0; i < hex_len; i++) {
    unsigned char c = contents_hex[i];

    CHECK((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'));
  }

  free(contents_hex);
  close(fd);

  return 0;
}
static int test_unsupported_version(void) {
  int fd = socket(AF_UNIX, SOCK_STREAM, 0);
  CHECK(fd >= 0);

  struct sockaddr_un addr = {0};
  addr.sun_family         = AF_UNIX;

  strncpy(addr.sun_path, "/tmp/pdf-signer.sock", sizeof(addr.sun_path) - 1);

  CHECK(connect(fd, (struct sockaddr*)&addr, sizeof(addr)) == 0);

  unsigned char request[SIGNER_REQUEST_SIZE] = {0};

  uint32_t version_net    = htonl(SIGNER_PROTOCOL_VERSION + 1);
  uint32_t algorithm_net  = htonl(SIGNER_DIGEST_SHA256);
  uint32_t digest_len_net = htonl(SIGNER_SHA256_DIGEST_LEN);
  uint32_t sign_mode_net  = htonl(SIGNER_MODE_PLAIN);

  memcpy(request, &version_net, 4);
  memcpy(request + 4, &algorithm_net, 4);
  memcpy(request + 8, &digest_len_net, 4);
  memcpy(request + 12, &sign_mode_net, 4);

  CHECK(write_exact(fd, request, sizeof(request)) == 0);

  unsigned char response_header[SIGNER_RESPONSE_HEADER_SIZE];

  CHECK(read_exact(fd, response_header, sizeof(response_header)) == 0);

  uint32_t status_net;
  uint32_t hex_len_net;

  memcpy(&status_net, response_header, 4);
  memcpy(&hex_len_net, response_header + 4, 4);

  CHECK(ntohl(status_net) == SIGNER_STATUS_UNSUPPORTED_VERSION);
  CHECK(ntohl(hex_len_net) == 0);

  close(fd);
  return 0;
}

static int test_unsupported_algorithm(void) {
  int fd = socket(AF_UNIX, SOCK_STREAM, 0);
  CHECK(fd >= 0);

  struct sockaddr_un addr = {0};
  addr.sun_family         = AF_UNIX;

  strncpy(addr.sun_path, "/tmp/pdf-signer.sock", sizeof(addr.sun_path) - 1);

  CHECK(connect(fd, (struct sockaddr*)&addr, sizeof(addr)) == 0);

  unsigned char request[SIGNER_REQUEST_SIZE] = {0};


  uint32_t version_net    = htonl(SIGNER_PROTOCOL_VERSION);
  uint32_t algorithm_net  = htonl(SIGNER_DIGEST_SHA256 + 1);
  uint32_t digest_len_net = htonl(SIGNER_SHA256_DIGEST_LEN);
  uint32_t sign_mode_net  = htonl(SIGNER_MODE_PLAIN);

  memcpy(request, &version_net, 4);
  memcpy(request + 4, &algorithm_net, 4);
  memcpy(request + 8, &digest_len_net, 4);
  memcpy(request + 12, &sign_mode_net, 4);

  CHECK(write_exact(fd, request, sizeof(request)) == 0);

  unsigned char response_header[SIGNER_RESPONSE_HEADER_SIZE];

  CHECK(read_exact(fd, response_header, sizeof(response_header)) == 0);

  uint32_t status_net;
  uint32_t hex_len_net;

  memcpy(&status_net, response_header, 4);
  memcpy(&hex_len_net, response_header + 4, 4);

  CHECK(ntohl(status_net) == SIGNER_STATUS_UNSUPPORTED_ALGORITHM);
  CHECK(ntohl(hex_len_net) == 0);

  close(fd);
  return 0;
}

static int test_invalid_digest_length(void) {
  int fd = socket(AF_UNIX, SOCK_STREAM, 0);
  CHECK(fd >= 0);

  struct sockaddr_un addr = {0};
  addr.sun_family         = AF_UNIX;

  strncpy(addr.sun_path, "/tmp/pdf-signer.sock", sizeof(addr.sun_path) - 1);

  CHECK(connect(fd, (struct sockaddr*)&addr, sizeof(addr)) == 0);

  unsigned char request[SIGNER_REQUEST_SIZE] = {0};
  uint32_t      version_net                  = htonl(SIGNER_PROTOCOL_VERSION);
  uint32_t      algorithm_net                = htonl(SIGNER_DIGEST_SHA256);
  uint32_t      digest_len_net               = htonl(SIGNER_SHA256_DIGEST_LEN - 1);
  uint32_t      sign_mode_net                = htonl(SIGNER_MODE_PLAIN);

  memcpy(request, &version_net, 4);
  memcpy(request + 4, &algorithm_net, 4);
  memcpy(request + 8, &digest_len_net, 4);
  memcpy(request + 12, &sign_mode_net, 4);
  CHECK(write_exact(fd, request, sizeof(request)) == 0);

  unsigned char response_header[SIGNER_RESPONSE_HEADER_SIZE];

  CHECK(read_exact(fd, response_header, sizeof(response_header)) == 0);

  uint32_t status_net;
  uint32_t hex_len_net;

  memcpy(&status_net, response_header, 4);
  memcpy(&hex_len_net, response_header + 4, 4);

  CHECK(ntohl(status_net) == SIGNER_STATUS_INVALID_REQUEST);
  CHECK(ntohl(hex_len_net) == 0);

  close(fd);
  return 0;
}

int main(void) {
  int failed = 0;

  failed += test_sign_request_mode(SIGNER_MODE_PLAIN);
  failed += test_sign_request_mode(SIGNER_MODE_TIMESTAMP);
  failed += test_unsupported_version();
  failed += test_unsupported_algorithm();
  failed += test_invalid_digest_length();

  if (failed == 0) {
    printf("All signer client tests passed\n");
    return 0;
  }

  fprintf(stderr, "%d signer client test(s) failed\n", failed);

  return 1;
}
