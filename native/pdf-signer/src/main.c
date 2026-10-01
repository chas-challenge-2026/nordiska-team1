#include "pdf_sign.h"
#include <stdio.h>
#include <signal.h>
#include <unistd.h>
#include "signer_server.h"

static volatile sig_atomic_t running = 1;

static void handle_signal(int signal) {
  (void)signal;
  running = 0;
}

int main(void) {

  signal(SIGINT, handle_signal);
  signal(SIGTERM, handle_signal);

  pdf_signer_t* signer = NULL;

  pdf_sign_status_t status = pdf_signer_create(&signer);

  if (status != PDF_SIGN_OK) {
    fprintf(stderr, "Failed to create pdf signer\n");
    return 1;
  }

  if (signer_server_run(signer) != 0) {
    fprintf(stderr, "Failed to start signer server\n");
    return 1;
  }

  printf("PDF signer initialized\n");

  pdf_signer_destroy(signer);

  printf("PDF signer shut down\n");

  return 0;
}
