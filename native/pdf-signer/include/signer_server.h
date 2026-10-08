#ifndef SIGNER_SERVER_H
#define SIGNER_SERVER_H
#include "pdf_sign.h"

/* Runs the Unix socket signer service until shutdown_fd becomes readable. */
int signer_server_run(pdf_signer_t* signer, int shutdown_fd);

#endif
