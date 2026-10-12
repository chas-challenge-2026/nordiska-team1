#ifndef SIGNER_SERVER_H
#define SIGNER_SERVER_H

/* Runs the Unix socket signer service until shutdown_fd becomes readable. */
int signer_server_run(int shutdown_fd);

#endif
