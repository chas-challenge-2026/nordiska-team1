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

static int test_key_loader_pkcs11_missing_module_path(void) {
  key_loader_config_t config = {
      .pkcs11_enabled = true,
      .pkcs11_provider_name = "pkcs11",
      .pkcs11_module_path = NULL,
  };

  key_loader_t *loader = key_loader_create(&config);

  CHECK(loader == NULL);

  return 0;
}

static int test_key_loader_pkcs11_null(void) {
  key_loader_t *loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_loader_destroy(loader);

  return 0;
}

static int test_key_load_unencrypted_pem(void) {
  key_loader_t *loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source = KEY_SOURCE_FILE,
      .u.file.path = "tests/data/private_key.pem",
  };

  key_handle_t handle = {0};

  bool ok = key_load(loader, &spec, NULL, &handle);

  CHECK(ok == true);
  CHECK(handle.pkey != NULL);

  key_dispose(&handle);
  key_loader_destroy(loader);

  return 0;
}

static int test_key_load_missing_pem(void) {
  key_loader_t *loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source = KEY_SOURCE_FILE,
      .u.file.path = "invalid/data/path/private_key.pem",
  };

  key_handle_t handle = {0};

  bool ok = key_load(loader, &spec, NULL, &handle);

  CHECK(ok == false);
  CHECK(handle.pkey == NULL);

  key_dispose(&handle);
  key_loader_destroy(loader);

  return 0;
}

static int test_key_load_invalid_source(void) {
  key_loader_t *loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source = (key_source_t)999,
      .u.file.path = "tests/data/private_key.pem",
  };

  key_handle_t handle = {0};

  bool ok = key_load(loader, &spec, NULL, &handle);
  CHECK(ok == false);
  CHECK(handle.pkey == NULL);

  key_dispose(&handle);
  key_loader_destroy(loader);

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
  failed += test_key_loader_pkcs11_missing_module_path();
  failed += test_key_loader_pkcs11_null();
  failed += test_key_load_unencrypted_pem();
  failed += test_key_load_missing_pem();
  failed += test_key_load_invalid_source();

  if (failed != 0) {
    fprintf(stderr, "%d tests failed\n", failed);
    return EXIT_FAILURE;
  }

  printf("All tests passed\n");

  return EXIT_SUCCESS;
}
