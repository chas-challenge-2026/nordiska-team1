#ifndef TSA_SERVER_H
#define TSA_SERVER_H
#include <stddef.h>

typedef struct tsa_server tsa_server_t;

typedef enum
{
  TSA_SERVER_OK = 0,
  TSA_SERVER_INVALID_ARGUMENT,
  TSA_SERVER_CRYPTO_ERROR,
  TSA_SERVER_CERTIFICATE_ERROR,
  TSA_SERVER_CONTEXT_ERROR,
  TSA_SERVER_INTERNAL_ERROR
} tsa_server_status_t;


/* Owns a DER-encoded RFC 3161 response. */
typedef struct
{
  unsigned char* response_der;
  size_t         response_der_len;
} tsa_server_result_t;

/* Creates the TSA state, key, certificate, and worker contexts. */
tsa_server_status_t tsa_server_create(tsa_server_t** out);

/* Creates an RFC 3161 response using the selected worker context. */
tsa_server_status_t tsa_server_create_response(tsa_server_t* server, size_t worker_index,
                                               const unsigned char* request_der,
                                               size_t request_der_len, tsa_server_result_t* result);

/* Releases output allocated by tsa_server_create_response(). */
void tsa_server_result_dispose(tsa_server_result_t* result);

/* Releases all TSA state and worker contexts. */
void tsa_server_destroy(tsa_server_t* server);

#endif
