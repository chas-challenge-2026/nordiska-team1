#ifndef CERT_LOAD_H
#define CERT_LOAD_H

#include <openssl/x509.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum
{
  CERT_STATUS_OK = 0,
  CERT_STATUS_INVALID_ARGUMENT,
  CERT_STATUS_LOAD_FAILED,
  CERT_STATUS_INTERNAL_ERROR,
} cert_status_t;

/* Owns a loaded X.509 certificate. */
typedef struct
{
  X509* certificate;

} cert_handle_t;

/* Loads and X.509 certificate from a file. */
cert_status_t cert_load_file(const char* path, cert_handle_t* out);

/* Releases the certificate owned by the handle. */
void cert_dispose(cert_handle_t* handle);

#ifdef __cplusplus
}
#endif
#endif
