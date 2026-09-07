#include "key_load.h"
#include <openssl/evp.h>

#define CHECK(condition)                                                       \
  do {                                                                         \
    if (!(condition)) {                                                        \
      fprintf(stderr, "CHECK failed: %s:%d: %s\n", __FILE__, __LINE__,         \
              #condition);                                                     \
      return 1;                                                                \
    }                                                                          \
  } while (0)

static int test_dispose_null(void) {
  key_dispose(NULL);

  return 0;
}

static int test_dispose_zero_initialized_handle(void) {
  key_handle_t handle = {0};
  key_dispose(&handle);

  CHECK(handle.pkey == NULL);

  return 0;
}

static int test_dispose_twice(void) {
  key_handle_t handle = {0};

  handle.pkey = EVP_PKEY_new();
  CHECK(handle.pkey != NULL);

  key_dispose(&handle);
  CHECK(handle.pkey == NULL);

  key_dispose(&handle);
  CHECK(handle.pkey == NULL);

  return 0;
}

static int test_dispose_openssl_evp_key(void) {
  EVP_PKEY *key = EVP_PKEY_new();
  CHECK(key != NULL);

  key_handle_t handle = {0};
  handle.pkey = key;
  key_dispose(&handle);

  CHECK(handle.pkey == NULL);

  return 0;
}

static int test_key_loader_create_null_config(void) {

  key_loader_t *loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_loader_destroy(loader);

  return 0;
}

static int test_key_loader_destroy_null(void) {
  key_loader_destroy(NULL);

  return 0;
}

static int test_key_loader_destroy(void) {
  key_loader_t *loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_loader_destroy(loader);
  return 0;
}

static int test_key_loader_destroy_multiple_times(void) {

  for (int i = 0; i < 30; i++) {
    key_loader_t *loader = key_loader_create(NULL);
    CHECK(loader != NULL);
    key_loader_destroy(loader);
  }

  return 0;
}

int main(void) {
  int failed = 0;

  failed += test_dispose_null();
  failed += test_dispose_zero_initialized_handle();
  failed += test_dispose_openssl_evp_key();
  failed += test_dispose_twice();
  failed += test_key_loader_destroy_null();
  failed += test_key_loader_create_null_config();
  failed += test_key_loader_destroy();
  failed += test_key_loader_destroy_multiple_times();

  if (failed != 0) {
    fprintf(stderr, "%d tests failed\n", failed);
    return EXIT_FAILURE;
  }

  printf("All tests passed\n");

  return EXIT_SUCCESS;
}
