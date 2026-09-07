#include "key_load.h"

#define CHECK(condition)                                                       \
  do {                                                                         \
    if (!(condition)) {                                                        \
      fprintf(stderr, "CHECK failed: %s:%d: %s\n", __FILE__, __LINE__,         \
              #condition);                                                     \
      return 1;                                                                \
    }                                                                          \
  } while (0)

static int test_key_loader_pkcs11_valid(void) {
  key_loader_config_t config = {.pkcs11_enabled = true,
                                .pkcs11_provider_name = "pkcs11",
                                .pkcs11_module_path =
                                    "/usr/lib/libsofthsm2.so"};

  key_loader_t *loader = key_loader_create(&config);

  CHECK(loader != NULL);
  key_loader_destroy(loader);
  return 0;
}

static int test_key_loader_pkcs11_invalid_module_path(void) {
  key_loader_config_t config = {.pkcs11_enabled = true,
                                .pkcs11_provider_name = "pkcs11",
                                .pkcs11_module_path =
                                    "/does/not/exist/libpkcs11.so",
                                .pkcs11_no_deinit = true};

  key_loader_t *loader = key_loader_create(&config);

  CHECK(loader == NULL);

  return 0;
}

static int test_key_loader_pkcs11_create_destroy_multiple(void) {
  key_loader_config_t config = {
      .pkcs11_enabled = true,
      .pkcs11_provider_name = "pkcs11",
      .pkcs11_module_path = "/usr/lib/libsofthsm2.so",
      .pkcs11_no_deinit = true,
  };

  for (int i = 0; i < 30; i++) {
    key_loader_t *loader = key_loader_create(&config);
    CHECK(loader != NULL);

    key_loader_destroy(loader);
  }

  return 0;
}

static int test_key_loader_pkcs11_disabled(void) {
  key_loader_config_t config = {
      .pkcs11_enabled = false,
      .pkcs11_provider_name = "pkcs11",
      .pkcs11_module_path = "/usr/lib/libsofthsm2.so",
      .pkcs11_no_deinit = true,
  };

  key_loader_t *loader = key_loader_create(&config);

  CHECK(loader != NULL);

  key_loader_destroy(loader);
  return 0;
}

static int test_key_loader_pkcs11_invalid_provider_name(void) {
  key_loader_config_t config = {
      .pkcs11_enabled = true,
      .pkcs11_provider_name = "does-not-exist",
      .pkcs11_module_path = "/usr/lib/libsofthsm2.so",
      .pkcs11_no_deinit = true,
  };

  key_loader_t *loader = key_loader_create(&config);

  CHECK(loader == NULL);

  return 0;
}

int main(void) {
  int failed = 0;

  failed += test_key_loader_pkcs11_valid();
  failed += test_key_loader_pkcs11_invalid_module_path();
  failed += test_key_loader_pkcs11_create_destroy_multiple();
  failed += test_key_loader_pkcs11_disabled();
  failed += test_key_loader_pkcs11_invalid_provider_name();

  if (failed != 0) {
    fprintf(stderr, "%d tests failed\n", failed);
    return EXIT_FAILURE;
  }

  printf("All tests passed\n");

  return EXIT_SUCCESS;
}
