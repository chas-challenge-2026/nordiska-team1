#include "key_load.h"
#include <string.h>
#include <openssl/err.h>


#define CHECK(condition)                                                                           \
  do {                                                                                             \
    if (!(condition)) {                                                                            \
      fprintf(stderr, "CHECK failed: %s:%d: %s\n", __FILE__, __LINE__, #condition);                \
      return 1;                                                                                    \
    }                                                                                              \
  } while (0)

typedef struct
{
  const unsigned char* pin;
  size_t               pin_len;
} test_pin_ctx_t;

static key_secret_result_t test_pin_callback(key_secret_kind_t kind, const key_spec_t* spec,
                                             unsigned char* buffer, size_t buffer_len,
                                             size_t* secret_len, void* userdata) {
  (void)spec;

  fprintf(stderr, "PIN callback called: kind=%d\n", (int)kind);
  test_pin_ctx_t* ctx = userdata;

  if (!ctx || !buffer || !secret_len) {
    return KEY_SECRET_ERROR;
  }

  if (kind != KEY_SECRET_PKCS11_PIN) {
    return KEY_SECRET_ERROR;
  }

  if (ctx->pin_len > buffer_len) {
    return KEY_SECRET_ERROR;
  }

  memcpy(buffer, ctx->pin, ctx->pin_len);
  *secret_len = ctx->pin_len;

  return KEY_SECRET_OK;
}


static int test_key_loader_pkcs11_valid(void) {
  key_loader_config_t config = {.pkcs11_enabled       = true,
                                .pkcs11_provider_name = "pkcs11",
                                .pkcs11_module_path   = "/usr/lib/libsofthsm2.so"};

  key_loader_t* loader = key_loader_create(&config);

  CHECK(loader != NULL);
  key_loader_destroy(loader);
  return 0;
}

static int test_key_loader_pkcs11_invalid_module_path(void) {
  key_loader_config_t config = {.pkcs11_enabled       = true,
                                .pkcs11_provider_name = "pkcs11",
                                .pkcs11_module_path   = "/does/not/exist/libpkcs11.so",
                                .pkcs11_no_deinit     = true};

  key_loader_t* loader = key_loader_create(&config);

  CHECK(loader == NULL);

  return 0;
}

static int test_key_loader_pkcs11_create_destroy_multiple(void) {
  key_loader_config_t config = {
      .pkcs11_enabled       = true,
      .pkcs11_provider_name = "pkcs11",
      .pkcs11_module_path   = "/usr/lib/libsofthsm2.so",
      .pkcs11_no_deinit     = true,
  };

  for (int i = 0; i < 30; i++) {
    key_loader_t* loader = key_loader_create(&config);
    CHECK(loader != NULL);

    key_loader_destroy(loader);
  }

  return 0;
}

static int test_key_loader_pkcs11_disabled(void) {
  key_loader_config_t config = {
      .pkcs11_enabled       = false,
      .pkcs11_provider_name = "pkcs11",
      .pkcs11_module_path   = "/usr/lib/libsofthsm2.so",
      .pkcs11_no_deinit     = true,
  };

  key_loader_t* loader = key_loader_create(&config);

  CHECK(loader != NULL);

  key_loader_destroy(loader);
  return 0;
}

static int test_key_loader_pkcs11_invalid_provider_name(void) {
  key_loader_config_t config = {
      .pkcs11_enabled       = true,
      .pkcs11_provider_name = "does-not-exist",
      .pkcs11_module_path   = "/usr/lib/libsofthsm2.so",
      .pkcs11_no_deinit     = true,
  };

  key_loader_t* loader = key_loader_create(&config);

  CHECK(loader == NULL);

  return 0;
}

static int test_key_loader_pkcs11_valid_key_load(void) {
  key_loader_config_t config = {.pkcs11_enabled       = true,
                                .pkcs11_provider_name = "pkcs11",
                                .pkcs11_module_path   = "/usr/lib/libsofthsm2.so"};

  key_loader_t* loader = key_loader_create(&config);

  CHECK(loader != NULL);

  key_spec_t spec = {
      .source       = KEY_SOURCE_PKCS11,
      .u.pkcs11.uri = "pkcs11:token=key-load-dev;"
                      "object=pdf-signer-test;"
                      "type=private",
  };

  static const unsigned char pin[] = "1111";

  test_pin_ctx_t pin_ctx = {
      .pin     = pin,
      .pin_len = sizeof(pin) - 1,
  };

  key_credentials_t credentials = {
      .callback = test_pin_callback,
      .userdata = &pin_ctx,
  };


  key_handle_t handle = {0};

  ERR_clear_error();


  bool ok = key_load(loader, &spec, &credentials, &handle);
  CHECK(ok == true);
  CHECK(handle.pkey != NULL);

  key_dispose(&handle);
  key_loader_destroy(loader);
  return 0;
}

int main(void) {
  int failed = 0;

  failed += test_key_loader_pkcs11_valid();
  failed += test_key_loader_pkcs11_invalid_module_path();
  failed += test_key_loader_pkcs11_create_destroy_multiple();
  failed += test_key_loader_pkcs11_disabled();
  failed += test_key_loader_pkcs11_invalid_provider_name();
  failed += test_key_loader_pkcs11_valid_key_load();

  if (failed != 0) {
    fprintf(stderr, "%d tests failed\n", failed);
    return EXIT_FAILURE;
  }

  printf("All tests passed\n");

  return EXIT_SUCCESS;
}
