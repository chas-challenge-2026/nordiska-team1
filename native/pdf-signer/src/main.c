#include "pdf_sign.h"
#include <stdio.h>
#include <signal.h>
#include <unistd.h>
#include "signer_server.h"

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
    return 1;
  }

  shutdown_write_fd = shutdown_pipe[1];


  signal(SIGINT, handle_signal);
  signal(SIGTERM, handle_signal);

  pdf_signer_t* signer = NULL;

  pdf_sign_status_t status = pdf_signer_create(&signer);

  if (status != PDF_SIGN_OK) {
    fprintf(stderr, "Failed to create pdf signer\n");
    return 1;
  }
  printf("PDF signer initialized\n");

  if (signer_server_run(shutdown_pipe[0]) != 0) {
    fprintf(stderr, "Failed to start signer server\n");
    return 1;
  }


  pdf_signer_destroy(signer);

  printf("PDF signer shut down\n");

  return 0;
}
