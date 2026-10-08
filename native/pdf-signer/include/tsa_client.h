#ifndef TSA_CLIENT_H
#define TSA_CLIENT_H
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum
{
  TSA_STATUS_OK = 0,
  TSA_STATUS_INVALID_ARGUMENT,
  TSA_STATUS_REQUEST_ERROR,
  TSA_STATUS_NETWORK_ERROR,
  TSA_STATUS_RESPONSE_ERROR,
  TSA_STATUS_VERIFY_ERROR,
  TSA_STATUS_INTERNAL_ERROR
} tsa_status_t;

/* Local RFC 3161 TSA client configuration. */
typedef struct
{
  const char* url;
  long        timeout_ms;
} tsa_config_t;

/* Owns the DER-encoded timestamp token returned by the TSA. */
typedef struct
{
  unsigned char* token_der;
  size_t         token_der_len;
} tsa_result_t;

/*
 * Requests and verifies an RFC 3161 timestamp for a CMS signature value.
 *
 * The request imprint is SHA-256(signature).
 */

tsa_status_t tsa_request_timestamp(const tsa_config_t* config, const unsigned char* signature,
                                   size_t signature_len, tsa_result_t* res);

/* Releases the token owned by the result. */
void tsa_result_dispose(tsa_result_t* res);
#ifdef __cplusplus
}
#endif
#endif
