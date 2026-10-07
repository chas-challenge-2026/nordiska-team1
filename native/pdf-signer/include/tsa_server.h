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

typedef struct
{
  unsigned char* response_der;
  size_t         response_der_len;
} tsa_server_result_t;

tsa_server_status_t tsa_server_create(tsa_server_t** out);
tsa_server_status_t tsa_server_create_response(tsa_server_t* server, size_t worker_index,
                                               const unsigned char* request_der,
                                               size_t request_der_len, tsa_server_result_t* result);

void tsa_server_result_dispose(tsa_server_result_t* result);

void tsa_server_destroy(tsa_server_t* server);

#endif
