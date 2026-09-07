#include "key_load.h"
#include <openssl/decoder.h>
#include <openssl/params.h>
#include <openssl/provider.h>
/*-------------------Internal helpers------------------------*/
struct key_loader {
  OSSL_LIB_CTX *libctx;
  OSSL_PROVIDER *default_provider;
  OSSL_PROVIDER *pkcs11_provider;
};
//
// static void release_secret(unsigned char **secret_buf) {
//   if (!secret_buf || !*secret_buf)
//     return;
//
//   OPENSSL_clear_free(*secret_buf, KEY_SECRET_MAX_LEN);
//   *secret_buf = NULL;
// }
//
// static key_secret_result_t request_secret(const key_credentials_t
// *credentials,
//                                           key_secret_kind_t kind,
//                                           const key_spec_t *spec,
//                                           unsigned char **out_buf,
//                                           size_t *out_len) {
//
//   if (!out_len || !out_buf) {
//     fprintf(stderr, "out_len & out_buf cannot be NULL\n");
//     return KEY_SECRET_ERROR;
//   }
//
//   *out_buf = NULL;
//   *out_len = 0;
//
//   if (!credentials || !credentials->callback) {
//     fprintf(stderr, "Credentials & credential callback required\n");
//     return KEY_SECRET_ERROR;
//   }
//
//   if (!spec) {
//     fprintf(stderr, "key_spec_t cannot be NULL\n");
//     return KEY_SECRET_ERROR;
//   }
//   unsigned char *secret_buf = OPENSSL_zalloc(KEY_SECRET_MAX_LEN);
//   if (!secret_buf) {
//     fprintf(stderr, "Failed to allocate memory for secret_buf\n");
//     return KEY_SECRET_ERROR;
//   }
//
//   size_t secret_len = 0;
//
//   key_secret_result_t res =
//       credentials->callback(kind, spec, secret_buf, KEY_SECRET_MAX_LEN,
//                             &secret_len, credentials->userdata);
//
//   if (res != KEY_SECRET_OK || secret_len > KEY_SECRET_MAX_LEN ||
//       secret_len == 0) {
//     fprintf(stderr, "credential callback failed\n");
//     release_secret(&secret_buf);
//     return KEY_SECRET_ERROR;
//   }
//
//   *out_len = secret_len;
//   *out_buf = secret_buf;
//
//   return KEY_SECRET_OK;
// }

static bool load_file_key(key_loader_t *loader, const key_spec_t *spec,
                          const key_credentials_t *credentials,
                          key_handle_t *out) {

  if (!loader || !spec || !out) {
    fprintf(stderr, "invalid or missing argument\n");
    return false;
  }

  out->pkey = NULL;

  if (spec->source != KEY_SOURCE_FILE || !spec->u.file.path) {
    fprintf(stderr, "Invalid or missing path\n");
    return false;
  }

  (void)credentials;

  BIO *bio_in = NULL;
  EVP_PKEY *pkey = NULL;

  bio_in = BIO_new_file(spec->u.file.path, "rb");
  if (!bio_in) {
    fprintf(stderr, "Failed to open key-file\n");
    return false;
  }

  OSSL_DECODER_CTX *dctx = OSSL_DECODER_CTX_new_for_pkey(
      &pkey, NULL, NULL, NULL, OSSL_KEYMGMT_SELECT_PRIVATE_KEY, loader->libctx,
      NULL);

  if (!dctx) {
    fprintf(stderr, "Failed to create decoder context\n");
    BIO_free(bio_in);
    return false;
  }

  if (OSSL_DECODER_from_bio(dctx, bio_in) != 1) {
    fprintf(stderr, "Failed to decode context\n");
    EVP_PKEY_free(pkey);
    OSSL_DECODER_CTX_free(dctx);
    BIO_free(bio_in);
    return false;
  }

  if (!pkey) {
    fprintf(stderr, "unable to retrieve pkey\n");
    OSSL_DECODER_CTX_free(dctx);
    BIO_free(bio_in);
    return false;
  }

  out->pkey = pkey;
  pkey = NULL;

  OSSL_DECODER_CTX_free(dctx);
  BIO_free(bio_in);

  return true;
}

/*-----------------------------------------------------------*/

key_loader_t *key_loader_create(const key_loader_config_t *config) {

  key_loader_t *key_loader = calloc(1, sizeof(key_loader_t));
  if (!key_loader) {
    fprintf(stderr, "Failed to allocate memory for key_loader\n");
    return NULL;
  }

  OSSL_LIB_CTX *libctx = OSSL_LIB_CTX_new();
  if (!libctx) {
    fprintf(stderr, "Failed to create libctx from openssl\n");
    key_loader_destroy(key_loader);
    return NULL;
  }

  key_loader->libctx = libctx;

  key_loader->default_provider =
      OSSL_PROVIDER_load(key_loader->libctx, "default");

  if (!key_loader->default_provider) {
    fprintf(stderr, "Failed to load OSSL default provider\n");
    key_loader_destroy(key_loader);
    return NULL;
  }

  if (!config || !config->pkcs11_enabled) {
    return key_loader;
  }

  if (!config->pkcs11_module_path) {
    fprintf(stderr, "pkcs11 module path required when pkcs11 is enabled\n");
    key_loader_destroy(key_loader);
    return NULL;
  }

  char *module_path = OPENSSL_strdup(config->pkcs11_module_path);
  if (!module_path) {
    fprintf(stderr, "Failed to duplicate modulepath using openssl_strdup\n");
    key_loader_destroy(key_loader);
    return NULL;
  }

  OSSL_PARAM params[4];
  size_t i = 0;
  params[i++] =
      OSSL_PARAM_construct_utf8_string("pkcs11-module-path", module_path, 0);
  params[i++] = OSSL_PARAM_construct_utf8_string("pkcs11-module-load-behavior",
                                                 "early", 0);
  if (config->pkcs11_no_deinit) {
    params[i++] = OSSL_PARAM_construct_utf8_string("pkcs11-module-quirks",
                                                   "no-deinit", 0);
  }
  params[i] = OSSL_PARAM_construct_end();

  const char *provider_name = config->pkcs11_provider_name != NULL
                                  ? config->pkcs11_provider_name
                                  : "pkcs11";

  key_loader->pkcs11_provider =
      OSSL_PROVIDER_load_ex(key_loader->libctx, provider_name, params);
  OPENSSL_free(module_path);

  if (!key_loader->pkcs11_provider) {
    fprintf(stderr, "Failed to load provider\n");
    key_loader_destroy(key_loader);
    return NULL;
  }

  return key_loader;
}

void key_loader_destroy(key_loader_t *loader) {
  if (!loader)
    return;

  if (loader->pkcs11_provider) {
    OSSL_PROVIDER_unload(loader->pkcs11_provider);
    loader->pkcs11_provider = NULL;
  }

  if (loader->default_provider) {
    OSSL_PROVIDER_unload(loader->default_provider);
    loader->default_provider = NULL;
  }

  if (loader->libctx) {
    OSSL_LIB_CTX_free(loader->libctx);
    loader->libctx = NULL;
  }

  free(loader);
}

bool key_load(key_loader_t *loader, const key_spec_t *spec,
              const key_credentials_t *credentials, key_handle_t *out) {
  if (!loader || !spec || !out) {
    return false;
  }

  out->pkey = NULL;

  switch (spec->source) {
  case KEY_SOURCE_FILE:
    return load_file_key(loader, spec, credentials, out);
  case KEY_SOURCE_PKCS11:
    return false;
  default:
    return false;
  }

  return false;
}

void key_dispose(key_handle_t *handle) {
  if (!handle)
    return;

  if (!handle->pkey)
    return;

  EVP_PKEY_free(handle->pkey);
  handle->pkey = NULL;
}
