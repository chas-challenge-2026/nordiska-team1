#include "pdf_sign.h"

#include <stdio.h>
#include <stdlib.h>

#define CHECK(condition)                                                                           \
  do {                                                                                             \
    if (!(condition)) {                                                                            \
      fprintf(stderr, "CHECK failed: %s:%d: %s\n", __FILE__, __LINE__, #condition);                \
      return 1;                                                                                    \
    }                                                                                              \
  } while (0)


static int test_pdf_signer_create_null_out(void) {
  pdf_sign_status_t status = pdf_signer_create(NULL);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_signer_destroy_null(void) {
  pdf_signer_destroy(NULL);

  return 0;
}


static int test_pdf_sign_null_signer(void) {
  unsigned char pdf[32] = {0};

  pdf_sign_request_t req = {
      .pdf              = pdf,
      .pdf_len          = sizeof(pdf),
      .byte_range       = {0, 0, 0, 0},
      .contents_offset  = 8,
      .contents_hex_len = 16,
  };

  pdf_sign_status_t status = pdf_signer_sign(NULL, &req);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_null_request(pdf_signer_t* signer) {
  pdf_sign_status_t status = pdf_signer_sign(signer, NULL);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_null_pdf(pdf_signer_t* signer) {
  pdf_sign_request_t req = {
      .pdf              = NULL,
      .pdf_len          = 32,
      .byte_range       = {0, 0, 0, 0},
      .contents_offset  = 8,
      .contents_hex_len = 16,
  };

  pdf_sign_status_t status = pdf_signer_sign(signer, &req);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_empty_pdf(pdf_signer_t* signer) {
  unsigned char pdf[1] = {0};

  pdf_sign_request_t req = {
      .pdf              = pdf,
      .pdf_len          = 0,
      .byte_range       = {0, 0, 0, 0},
      .contents_offset  = 0,
      .contents_hex_len = 0,
  };

  pdf_sign_status_t status = pdf_signer_sign(signer, &req);

  CHECK(status == PDF_SIGN_INVALID_PDF);

  return 0;
}


static int test_pdf_sign_contents_offset_outside_pdf(pdf_signer_t* signer) {

  unsigned char pdf[32] = {0};

  pdf_sign_request_t req = {
      .pdf              = pdf,
      .pdf_len          = sizeof(pdf),
      .byte_range       = {0, 0, 0, 0},
      .contents_offset  = 33,
      .contents_hex_len = 0,
  };

  pdf_sign_status_t status = pdf_signer_sign(signer, &req);

  CHECK(status == PDF_SIGN_INVALID_PDF);

  return 0;
}


static int test_pdf_sign_contents_length_outside_pdf(pdf_signer_t* signer) {

  unsigned char pdf[32] = {0};

  pdf_sign_request_t req = {
      .pdf              = pdf,
      .pdf_len          = sizeof(pdf),
      .byte_range       = {0, 0, 0, 0},
      .contents_offset  = 20,
      .contents_hex_len = 16,
  };

  pdf_sign_status_t status = pdf_signer_sign(signer, &req);

  CHECK(status == PDF_SIGN_INVALID_PDF);

  return 0;
}


static int test_pdf_sign_valid_request(pdf_signer_t* signer) {
  unsigned char pdf[32] = {0};

  pdf_sign_request_t req = {
      .pdf              = pdf,
      .pdf_len          = sizeof(pdf),
      .byte_range       = {0, 0, 0, 0},
      .contents_offset  = 8,
      .contents_hex_len = 16,
  };

  pdf_sign_status_t status = pdf_signer_sign(signer, &req);

  CHECK(status == PDF_SIGN_OK);

  return 0;
}


int main(void) {
  int failed = 0;

  failed += test_pdf_signer_create_null_out();
  failed += test_pdf_signer_destroy_null();
  failed += test_pdf_sign_null_signer();

  pdf_signer_t* signer = NULL;

  pdf_sign_status_t create_status = pdf_signer_create(&signer);

  if (create_status != PDF_SIGN_OK || !signer) {
    fprintf(stderr, "Failed to create PDF signer: %d\n", create_status);

    return EXIT_FAILURE;
  }

  failed += test_pdf_sign_null_request(signer);
  failed += test_pdf_sign_null_pdf(signer);
  failed += test_pdf_sign_empty_pdf(signer);
  failed += test_pdf_sign_contents_offset_outside_pdf(signer);
  failed += test_pdf_sign_contents_length_outside_pdf(signer);
  failed += test_pdf_sign_valid_request(signer);

  pdf_signer_destroy(signer);

  if (failed != 0) {
    fprintf(stderr, "%d tests failed\n", failed);
    return EXIT_FAILURE;
  }

  printf("All pdf_sign tests passed\n");

  return EXIT_SUCCESS;
}
