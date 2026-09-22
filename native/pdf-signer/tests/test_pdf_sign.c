#include "pdf_sign.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

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
  unsigned char digest[32] = {0};

  pdf_sign_request_t req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = digest,
      .digest_len       = sizeof(digest),
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign(NULL, &req, &result);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_null_request(pdf_signer_t* signer) {
  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign(signer, NULL, &result);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_null_result(pdf_signer_t* signer) {
  unsigned char digest[32] = {0};

  pdf_sign_request_t req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = digest,
      .digest_len       = sizeof(digest),
  };

  pdf_sign_status_t status = pdf_signer_sign(signer, &req, NULL);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_null_digest(pdf_signer_t* signer) {
  pdf_sign_request_t req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = NULL,
      .digest_len       = 32,
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign(signer, &req, &result);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_invalid_digest_length(pdf_signer_t* signer) {
  unsigned char digest[31] = {0};

  pdf_sign_request_t req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = digest,
      .digest_len       = sizeof(digest),
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign(signer, &req, &result);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_invalid_algorithm(pdf_signer_t* signer) {
  unsigned char digest[32] = {0};

  pdf_sign_request_t req = {
      .digest_algorithm = (pdf_sign_digest_algorithm_t)999,
      .digest           = digest,
      .digest_len       = sizeof(digest),
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign(signer, &req, &result);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_valid_request(pdf_signer_t* signer) {
  unsigned char digest[32] = {0};

  pdf_sign_request_t req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = digest,
      .digest_len       = sizeof(digest),
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign(signer, &req, &result);

  CHECK(status == PDF_SIGN_OK);
  CHECK(result.contents_hex != NULL);
  CHECK(result.contents_hex_len == 8192);
  CHECK(strlen(result.contents_hex) == 8192);

  pdf_sign_result_dispose(&result);

  CHECK(result.contents_hex == NULL);
  CHECK(result.contents_hex_len == 0);

  return 0;
}


static int test_pdf_sign_result_dispose_null(void) {
  pdf_sign_result_dispose(NULL);

  return 0;
}


static int test_pdf_sign_result_dispose_twice(void) {
  pdf_sign_result_t result = {0};

  result.contents_hex = malloc(16);
  CHECK(result.contents_hex != NULL);

  result.contents_hex_len = 15;

  pdf_sign_result_dispose(&result);
  pdf_sign_result_dispose(&result);

  CHECK(result.contents_hex == NULL);
  CHECK(result.contents_hex_len == 0);

  return 0;
}


int main(void) {
  int failed = 0;

  failed += test_pdf_signer_create_null_out();
  failed += test_pdf_signer_destroy_null();
  failed += test_pdf_sign_null_signer();
  failed += test_pdf_sign_result_dispose_null();
  failed += test_pdf_sign_result_dispose_twice();

  pdf_signer_t* signer = NULL;

  pdf_sign_status_t create_status = pdf_signer_create(&signer);

  if (create_status != PDF_SIGN_OK || !signer) {
    fprintf(stderr, "Failed to create PDF signer: %d\n", create_status);
    return EXIT_FAILURE;
  }

  failed += test_pdf_sign_null_request(signer);
  failed += test_pdf_sign_null_result(signer);
  failed += test_pdf_sign_null_digest(signer);
  failed += test_pdf_sign_invalid_digest_length(signer);
  failed += test_pdf_sign_invalid_algorithm(signer);
  failed += test_pdf_sign_valid_request(signer);

  pdf_signer_destroy(signer);

  if (failed != 0) {
    fprintf(stderr, "%d tests failed\n", failed);
    return EXIT_FAILURE;
  }

  printf("All pdf_sign tests passed\n");

  return EXIT_SUCCESS;
}
