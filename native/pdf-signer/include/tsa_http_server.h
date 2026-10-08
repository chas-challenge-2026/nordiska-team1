#ifndef TSA_HTTP_SERVER_H
#define TSA_HTTP_SERVER_H

#include "tsa_server.h"

/* Runs the local TSA HTTP server until shutdown_fd becomes readable. */
int tsa_http_server_run(tsa_server_t* server, int shutdown_fd, unsigned short port);

#endif
