#ifndef PDF_SIGN_H
#define PDF_SIGN_H

#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

typedef struct pdf_signer pdf_signer_t;

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
  unsigned char* pdf;
  size_t         pdf_len;
  size_t         byte_range[4];
  size_t         contents_offset;
  size_t         contents_hex_len;
} pdf_sign_request_t;


pdf_sign_status_t pdf_signer_create(pdf_signer_t** out);
pdf_sign_status_t pdf_signer_sign(pdf_signer_t* signer, const pdf_sign_request_t* req);
void              pdf_signer_destroy(pdf_signer_t* signer);

#ifdef __cplusplus
}
#endif
#endif
