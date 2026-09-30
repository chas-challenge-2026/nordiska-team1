#include "cert_load.h"

#include <stdio.h>

#define CHECK(cond)                                                                                \
  do {                                                                                             \
    if (!(cond)) {                                                                                 \
      fprintf(stderr, "CHECK failed: %s:%d: %s\n", __FILE__, __LINE__, #cond);                     \
      return 1;                                                                                    \
    }                                                                                              \
  } while (0)

static int test_load_pem_certificate(void) {
  cert_handle_t handle = {0};

  cert_status_t status = cert_load_file("tests/data/signing_cert.pem", &handle);

  CHECK(status == CERT_STATUS_OK);
  CHECK(handle.certificate != NULL);

  cert_dispose(&handle);

  CHECK(handle.certificate == NULL);

  return 0;
}

static int test_load_der_certificate(void) {
  cert_handle_t handle = {0};

  cert_status_t status = cert_load_file("tests/data/signing_cert.der", &handle);

  CHECK(status == CERT_STATUS_OK);
  CHECK(handle.certificate != NULL);

  cert_dispose(&handle);

  return 0;
}

static int test_missing_certificate(void) {
  cert_handle_t handle = {0};

  cert_status_t status = cert_load_file("tests/data/does_not_exist.pem", &handle);

  CHECK(status == CERT_STATUS_LOAD_FAILED);
  CHECK(handle.certificate == NULL);

  return 0;
}

static int test_invalid_arguments(void) {
  cert_handle_t handle = {0};

  CHECK(cert_load_file(NULL, &handle) == CERT_STATUS_INVALID_ARGUMENT);
  CHECK(cert_load_file("tests/data/signing_cert.pem", NULL) == CERT_STATUS_INVALID_ARGUMENT);

  return 0;
}

static int test_dispose(void) {
  cert_handle_t handle = {0};

  cert_dispose(NULL);
  cert_dispose(&handle);
  cert_dispose(&handle);

  CHECK(handle.certificate == NULL);

  return 0;
}

int main(void) {
  CHECK(test_load_pem_certificate() == 0);
  CHECK(test_load_der_certificate() == 0);
  CHECK(test_missing_certificate() == 0);
  CHECK(test_invalid_arguments() == 0);
  CHECK(test_dispose() == 0);

  printf("All cert_load tests passed\n");
  return 0;
}
