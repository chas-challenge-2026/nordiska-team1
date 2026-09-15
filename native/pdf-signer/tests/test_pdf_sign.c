#include "key_load.h"
#include "pdf_sign.h"
#include "cert_load.h"
#include <stdio.h>
#include <string.h>

#define CHECK(condition)                                                                           \
  do {                                                                                             \
    if (!(condition)) {                                                                            \
      fprintf(stderr, "CHECK failed: %s:%d: %s\n", __FILE__, __LINE__, #condition);                \
      return 1;                                                                                    \
    }                                                                                              \
  } while (0)

static int test_pdf_sign_key_cert(void) {
  key_loader_t* loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source      = KEY_SOURCE_FILE,
      .u.file.path = "tests/data/private_key.pem",
  };

  key_handle_t key_handle = {0};

  key_status_t key_status = key_load(loader, &spec, NULL, &key_handle);

  CHECK(key_status == KEY_STATUS_OK);
  CHECK(key_handle.pkey != NULL);

  cert_handle_t cert_handle = {0};

  cert_status_t cert_status = cert_load_file("tests/data/signing_cert.pem", &cert_handle);

  CHECK(cert_status == CERT_STATUS_OK);
  CHECK(cert_handle.certificate != NULL);

  pdf_sign_request_t req = {
      .input_path        = "tests/data/input.pdf",
      .output_path       = "tests/data/output.pdf",
      .private_key       = key_handle.pkey,
      .certificate       = cert_handle.certificate,
      .certificate_chain = NULL,
  };

  pdf_sign_status_t sign_status = pdf_sign(&req);


  cert_dispose(&cert_handle);
  key_dispose(&key_handle);
  key_loader_destroy(loader);
  CHECK(sign_status == PDF_SIGN_OK);

  return 0;
}

static int test_pdf_sign_mismatched_key_cert(void) {
  key_loader_t* loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source      = KEY_SOURCE_FILE,
      .u.file.path = "tests/data/private_key.pem",
  };

  key_handle_t key_handle = {0};

  key_status_t key_status = key_load(loader, &spec, NULL, &key_handle);

  CHECK(key_status == KEY_STATUS_OK);
  CHECK(key_handle.pkey != NULL);

  cert_handle_t cert_handle = {0};

  cert_status_t cert_status = cert_load_file("tests/data/wrong_cert.pem", &cert_handle);

  CHECK(cert_status == CERT_STATUS_OK);
  CHECK(cert_handle.certificate != NULL);

  pdf_sign_request_t req = {
      .input_path        = "tests/data/input.pdf",
      .output_path       = "tests/data/output.pdf",
      .private_key       = key_handle.pkey,
      .certificate       = cert_handle.certificate,
      .certificate_chain = NULL,
  };

  pdf_sign_status_t sign_status = pdf_sign(&req);


  cert_dispose(&cert_handle);
  key_dispose(&key_handle);
  key_loader_destroy(loader);
  CHECK(sign_status == PDF_SIGN_CERTIFICATE_ERROR);

  return 0;
}

int main(void) {
  int failed = 0;

  failed += test_pdf_sign_key_cert();
  failed += test_pdf_sign_mismatched_key_cert();

  if (failed != 0) {
    fprintf(stderr, "%d tests failed\n", failed);
    return EXIT_FAILURE;
  }

  printf("All tests passed\n");

  return EXIT_SUCCESS;
}
