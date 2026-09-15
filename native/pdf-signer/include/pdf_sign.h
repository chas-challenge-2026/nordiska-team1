#ifndef PDF_SIGN_H
#define PDF_SIGN_H

#include <openssl/evp.h>
#include <openssl/x509.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef enum
{
  PDF_SIGN_OK = 0,
  PDF_SIGN_INVALID_ARGUMENT,
  PDF_SIGN_INVALID_PDF,
  PDF_SIGN_CERTIFICATE_ERROR,
  PDF_SIGN_CRYPTO_ERROR,
  PDF_SIGN_OUTPUT_ERROR,
  PDF_SIGN_UNAUTHORIZED,
  PDF_SIGN_INTERNAL_ERROR
} pdf_sign_status_t;

typedef struct
{
  const char* input_path;
  const char* output_path;

  EVP_PKEY* private_key;
  X509*     certificate;
  STACK_OF(X509) * certificate_chain;
} pdf_sign_request_t;

pdf_sign_status_t pdf_sign(const pdf_sign_request_t* req);

#ifdef __cplusplus
}
#endif
#endif
