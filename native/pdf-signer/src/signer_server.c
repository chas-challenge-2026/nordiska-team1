#include "signer_server.h"
#include "signer_protocol.h"
#include <sys/socket.h>
#include <sys/un.h>
#include <stdio.h>
#include <string.h>
#include <unistd.h>
#include <errno.h>
#include <arpa/inet.h>
#include <poll.h>

/*---------------------INTERNAL----------------------_*/

static void send_error_response(int client_fd, signer_protocol_status_t status) {
  unsigned char response_header[SIGNER_RESPONSE_HEADER_SIZE];

  uint32_t status_net  = htonl((uint32_t)status);
  uint32_t hex_len_net = htonl(0);

  memcpy(response_header, &status_net, 4);
  memcpy(response_header + 4, &hex_len_net, 4);

  size_t bytes_sent = 0;

  while (bytes_sent < SIGNER_RESPONSE_HEADER_SIZE) {
    ssize_t n =
        write(client_fd, response_header + bytes_sent, SIGNER_RESPONSE_HEADER_SIZE - bytes_sent);

    if (n < 0) {
      if (errno == EINTR) {
        continue;
      }

      break;
    }

    if (n == 0) {
      break;
    }

    bytes_sent += (size_t)n;
  }
}
/*--------------------------------------------------------*/

int signer_server_run(pdf_signer_t* signer, int shutdown_fd) {
  if (!signer || shutdown_fd < 0) {
    return 1;
  }

  int fd = socket(AF_UNIX, SOCK_STREAM, 0);
  if (fd < 0) {
    perror("socket");
    return 1;
  }

  struct sockaddr_un addr = {0};
  addr.sun_family         = AF_UNIX;

  strncpy(addr.sun_path, "/tmp/pdf-signer.sock", sizeof(addr.sun_path) - 1);
  unlink(addr.sun_path);

  if (bind(fd, (struct sockaddr*)&addr, sizeof(addr)) != 0) {
    perror("bind");
    close(fd);
    return 1;
  }

  if (listen(fd, 16) != 0) {
    perror("listen");
    close(fd);
    unlink(addr.sun_path);
    return 1;
  }

  printf("Signer service listening on %s\n", addr.sun_path);

  struct pollfd fds[2];

  fds[0].fd     = fd;
  fds[0].events = POLLIN;

  fds[1].fd     = shutdown_fd;
  fds[1].events = POLLIN;

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

    unsigned char headers[SIGNER_REQUEST_HEADER_SIZE];

    size_t total_read = 0;

    while (total_read < SIGNER_REQUEST_HEADER_SIZE) {
      ssize_t n = read(client_fd, headers + total_read, SIGNER_REQUEST_HEADER_SIZE - total_read);

      if (n < 0) {
        if (errno == EINTR) {
          continue;
        }
        printf("An error with connection occured for client %d\n", client_fd);
        break;
      }

      if (n == 0) {
        printf("Client %d closed connection\n", client_fd);
        break;
      }

      total_read += n;
    }

    if (total_read != SIGNER_REQUEST_HEADER_SIZE) {
      close(client_fd);
      continue;
    }

    uint32_t version_net;
    uint32_t algorithm_net;
    uint32_t digest_len_net;

    memcpy(&version_net, headers, 4);
    memcpy(&algorithm_net, headers + 4, 4);
    memcpy(&digest_len_net, headers + 8, 4);

    uint32_t version    = ntohl(version_net);
    uint32_t algorithm  = ntohl(algorithm_net);
    uint32_t digest_len = ntohl(digest_len_net);


    if (version != SIGNER_PROTOCOL_VERSION) {
      printf("Invalid protocol version\n");

      send_error_response(client_fd, SIGNER_STATUS_UNSUPPORTED_VERSION);

      close(client_fd);
      continue;
    }

    if (algorithm != SIGNER_DIGEST_SHA256) {
      printf("Invalid digest algorithm\n");

      send_error_response(client_fd, SIGNER_STATUS_UNSUPPORTED_ALGORITHM);

      close(client_fd);
      continue;
    }

    if (digest_len != SIGNER_SHA256_DIGEST_LEN) {
      printf("Invalid digest length\n");

      send_error_response(client_fd, SIGNER_STATUS_INVALID_REQUEST);

      close(client_fd);
      continue;
    }

    total_read = 0;
    unsigned char buffer[SIGNER_REQUEST_DIGEST_SIZE];

    while (total_read < SIGNER_REQUEST_DIGEST_SIZE) {
      ssize_t n = read(client_fd, buffer + total_read, SIGNER_REQUEST_DIGEST_SIZE - total_read);

      if (n < 0) {
        if (errno == EINTR) {
          continue;
        }
        printf("An error with connection occured for client %d\n", client_fd);
        break;
      }

      if (n == 0) {
        printf("Client %d closed connection\n", client_fd);
        break;
      }

      total_read += n;
    }

    if (total_read != SIGNER_REQUEST_DIGEST_SIZE) {
      close(client_fd);
      continue;
    }

    signer_request_t req = {
        .version          = version,
        .digest_algorithm = algorithm,
        .digest_len       = digest_len,
    };

    memcpy(req.digest, buffer, SIGNER_REQUEST_DIGEST_SIZE);

    pdf_sign_request_t sign_req = {.digest_algorithm = PDF_SIGN_DIGEST_SHA256,
                                   .digest           = req.digest,
                                   .digest_len       = req.digest_len};

    pdf_sign_result_t res = {0};

    pdf_sign_status_t status = pdf_signer_sign(signer, &sign_req, &res);

    if (status != PDF_SIGN_OK) {
      send_error_response(client_fd, SIGNER_STATUS_SIGNING_ERROR);
      pdf_sign_result_dispose(&res);
      close(client_fd);
      continue;
    }

    if (res.contents_hex_len > SIGNER_RESPONSE_MAX_SIZE || res.contents_hex_len > UINT32_MAX) {

      send_error_response(client_fd, SIGNER_STATUS_INTERNAL_ERROR);

      pdf_sign_result_dispose(&res);
      close(client_fd);
      continue;
    }

    unsigned char response_header[SIGNER_RESPONSE_HEADER_SIZE];
    uint32_t      status_net  = htonl(SIGNER_STATUS_OK);
    uint32_t      hex_len_net = htonl((uint32_t)res.contents_hex_len);

    memcpy(response_header, &status_net, 4);
    memcpy(response_header + 4, &hex_len_net, 4);

    size_t bytes_sent = 0;

    while (bytes_sent < SIGNER_RESPONSE_HEADER_SIZE) {
      ssize_t n =
          write(client_fd, response_header + bytes_sent, SIGNER_RESPONSE_HEADER_SIZE - bytes_sent);

      if (n < 0) {
        if (errno == EINTR) {
          continue;
        }
        break;
      }

      if (n == 0) {
        break;
      }

      bytes_sent += n;
    }

    if (bytes_sent != SIGNER_RESPONSE_HEADER_SIZE) {
      pdf_sign_result_dispose(&res);
      close(client_fd);
      continue;
    }

    bytes_sent = 0;

    while (bytes_sent < (size_t)res.contents_hex_len) {
      ssize_t n =
          write(client_fd, res.contents_hex + bytes_sent, res.contents_hex_len - bytes_sent);

      if (n < 0) {
        if (errno == EINTR) {
          continue;
        }
        break;
      }

      if (n == 0) {
        break;
      }

      bytes_sent += n;
    }

    if (bytes_sent != res.contents_hex_len) {
      pdf_sign_result_dispose(&res);
      close(client_fd);
      continue;
    }

    pdf_sign_result_dispose(&res);
    close(client_fd);
  }

  close(fd);
  unlink(addr.sun_path);

  return 0;
}
