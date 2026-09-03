#include "key_load.h"
#include <openssl/provider.h>

struct key_loader {
  OSSL_LIB_CTX *libctx;
  OSSL_PROVIDER *default_provider;
  OSSL_PROVIDER *pkcs11_provider;
};

key_loader_t *key_loader_create(const key_loader_config_t *config) {

  (void)config;

  key_loader_t *key_loader = calloc(1, sizeof(key_loader_t));
  if (!key_loader) {
    fprintf(stderr, "Failed to allocate memory for key_loader");
    return NULL;
  }

  OSSL_LIB_CTX *libctx = OSSL_LIB_CTX_new();
  if (!libctx) {
    fprintf(stderr, "Failed to create libctx from openssl");
    key_loader_destroy(key_loader);
    return NULL;
  }

  key_loader->libctx = libctx;

  key_loader->default_provider =
      OSSL_PROVIDER_load(key_loader->libctx, "default");

  if (!key_loader->default_provider) {
    fprintf(stderr, "Failed to load OSSL default provider");
    key_loader_destroy(key_loader);
    return NULL;
  }

  return key_loader;
}

void key_loader_destroy(key_loader_t *loader) {
  if (!loader)
    return;

  // TODO: add pkcs11_provider teardown here when implemented
  if (loader->default_provider)
    OSSL_PROVIDER_unload(loader->default_provider);

  if (loader->libctx)
    OSSL_LIB_CTX_free(loader->libctx);

  free(loader);
}

bool key_load(key_loader_t *loader, const key_spec_t *spec,
              const key_credentials_t *credentials, key_handle_t *out);

void key_dispose(key_handle_t *handle) {
  if (!handle)
    return;

  if (!handle->pkey)
    return;

  EVP_PKEY_free(handle->pkey);
  handle->pkey = NULL;
}
