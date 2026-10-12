#include "tsa_server.h"
#include "tsa_http_server.h"
#include "runtime_config.h"
#include <signal.h>
#include <stdio.h>
#include <unistd.h>


static int shutdown_write_fd = -1;

static void handle_signal(int signal) {
  (void)signal;

  unsigned char byte = 1;

  if (shutdown_write_fd >= 0) {
    write(shutdown_write_fd, &byte, 1);
  }
}

int main(void) {
  int shutdown_pipe[2];

  if (pipe(shutdown_pipe) != 0) {
    perror("pipe");
    return -1;
  }

  shutdown_write_fd = shutdown_pipe[1];

  signal(SIGINT, handle_signal);
  signal(SIGTERM, handle_signal);

  tsa_server_t* server = NULL;

  tsa_server_status_t status = tsa_server_create(&server);

  if (status != TSA_SERVER_OK) {
    fprintf(stderr, "Failed to create TSA server\n");

    close(shutdown_pipe[0]);
    close(shutdown_pipe[1]);

    return -1;
  }

  printf("TSA server initialized\n");

  // unsigned short port   = runtime_config_tsa_port();
  int result = tsa_http_server_run(server, shutdown_pipe[0], 8081);

  tsa_server_destroy(server);

  close(shutdown_pipe[0]);
  close(shutdown_pipe[1]);

  printf("TSA server shut down\n");

  return result == 0 ? 0 : -1;
}
