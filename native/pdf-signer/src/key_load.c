#include "key_load.h"
#include <openssl/decoder.h>
#include <openssl/params.h>
#include <openssl/provider.h>
#include <openssl/store.h>
#include <string.h>
#include <openssl/ui.h>
#include <openssl/err.h>

/*-------------------Internal helpers------------------------*/
static key_secret_result_t request_secret(const key_credentials_t* credentials,
                                          key_secret_kind_t kind, const key_spec_t* spec,
                                          unsigned char** out_buf, size_t* out_len);
static int                 file_passphrase_cb(char* pass, size_t pass_size, size_t* pass_len,
                                              const OSSL_PARAM params[], void* arg);

/*----------------------------------------------------------*/

struct key_loader
{
  OSSL_LIB_CTX*  libctx;
  OSSL_PROVIDER* default_provider;
  OSSL_PROVIDER* pkcs11_provider;
};

struct file_passphrase_ctx
{
  const key_credentials_t* credentials;
  const key_spec_t*        spec;
};

struct pkcs11_ui_ctx
{
  const key_credentials_t* credentials;
  const key_spec_t*        spec;
};

static void release_secret(unsigned char** secret_buf) {
  if (!secret_buf || !*secret_buf)
    return;

  OPENSSL_clear_free(*secret_buf, KEY_SECRET_MAX_LEN);
  *secret_buf = NULL;
}

static int file_passphrase_cb(char* pass, size_t pass_size, size_t* pass_len,
                              const OSSL_PARAM params[], void* arg) {
  (void)params;

  struct file_passphrase_ctx* ctx = arg;

  unsigned char* secret     = NULL;
  size_t         secret_len = 0;

  if (!ctx || !pass || !pass_len) {
    return 0; // 0 = Error & 1 = Success for OpenSSL
  }

  if (request_secret(ctx->credentials, KEY_SECRET_FILE_PASSPHRASE, ctx->spec, &secret,
                     &secret_len) != KEY_SECRET_OK) {
    fprintf(stderr, "Failed to get secret\n");
    return 0;
  }

  if (secret_len > pass_size) {
    release_secret(&secret);
    return 0;
  }

  memcpy(pass, secret, secret_len);
  *pass_len = secret_len;

  release_secret(&secret);

  return 1;
}

static int pkcs11_ui_reader(UI* ui, UI_STRING* uis) {
  if (!ui || !uis) {
    return 0; // 0 is error for OpenSSL
  }

  fprintf(stderr, "PKCS11 UI reader called: type=%d\n", (int)UI_get_string_type(uis));

  enum UI_string_types type = UI_get_string_type(uis);


  // Reader can get multiple types of UI_STRING
  if (type != UIT_PROMPT && type != UIT_VERIFY) {
    return 1;
  }

  struct pkcs11_ui_ctx* ctx = UI_get0_user_data(ui);
  if (!ctx) {
    fprintf(stderr, "Missing PKCS#11 UI context\n");
    return 0;
  }

  unsigned char* secret     = NULL;
  size_t         secret_len = 0;

  if (request_secret(ctx->credentials, KEY_SECRET_PKCS11_PIN, ctx->spec, &secret, &secret_len) !=
      KEY_SECRET_OK) {
    fprintf(stderr, "Failed to get PKCS#11 PIN\n");
    return 0;
  }

  if (secret_len > INT_MAX) {
    release_secret(&secret);
    return 0;
  }

  int res = UI_set_result_ex(ui, uis, (const char*)secret, (int)secret_len);

  release_secret(&secret);

  if (res < 0) {
    fprintf(stderr, "Failed to set PKCS#11 PIN in OpenSSL UI\n");
    return 0;
  }

  return 1;
}

static key_secret_result_t request_secret(const key_credentials_t* credentials,
                                          key_secret_kind_t kind, const key_spec_t* spec,
                                          unsigned char** out_buf, size_t* out_len) {

  if (!out_len || !out_buf) {
    fprintf(stderr, "out_len & out_buf cannot be NULL\n");
    return KEY_SECRET_ERROR;
  }

  *out_buf = NULL;
  *out_len = 0;

  if (!credentials || !credentials->callback) {
    fprintf(stderr, "Credentials & credential callback required\n");
    return KEY_SECRET_ERROR;
  }

  if (!spec) {
    fprintf(stderr, "key_spec_t cannot be NULL\n");
    return KEY_SECRET_ERROR;
  }
  unsigned char* secret_buf = OPENSSL_zalloc(KEY_SECRET_MAX_LEN);
  if (!secret_buf) {
    fprintf(stderr, "Failed to allocate memory for secret_buf\n");
    return KEY_SECRET_ERROR;
  }

  size_t secret_len = 0;

  key_secret_result_t res = credentials->callback(kind, spec, secret_buf, KEY_SECRET_MAX_LEN,
                                                  &secret_len, credentials->userdata);

  if (res != KEY_SECRET_OK || secret_len > KEY_SECRET_MAX_LEN || secret_len == 0) {
    fprintf(stderr, "credential callback failed\n");
    release_secret(&secret_buf);
    return KEY_SECRET_ERROR;
  }

  *out_len = secret_len;
  *out_buf = secret_buf;

  return KEY_SECRET_OK;
}

static bool load_file_key(key_loader_t* loader, const key_spec_t* spec,
                          const key_credentials_t* credentials, key_handle_t* out) {

  if (!loader || !spec || !out) {
    fprintf(stderr, "invalid or missing argument\n");
    return false;
  }

  out->pkey = NULL;

  if (spec->source != KEY_SOURCE_FILE || !spec->u.file.path) {
    fprintf(stderr, "Invalid or missing path\n");
    return false;
  }

  BIO*      bio_in = NULL;
  EVP_PKEY* pkey   = NULL;

  bio_in = BIO_new_file(spec->u.file.path, "rb");
  if (!bio_in) {
    fprintf(stderr, "Failed to open key-file\n");
    return false;
  }

  OSSL_DECODER_CTX* dctx = OSSL_DECODER_CTX_new_for_pkey(
      &pkey, NULL, NULL, NULL, OSSL_KEYMGMT_SELECT_PRIVATE_KEY, loader->libctx, NULL);

  if (!dctx) {
    fprintf(stderr, "Failed to create decoder context\n");
    BIO_free(bio_in);
    return false;
  }

  struct file_passphrase_ctx pass_ctx = {
      .credentials = credentials,
      .spec        = spec,
  };

  if (credentials && credentials->callback) {
    if (!OSSL_DECODER_CTX_set_passphrase_cb(dctx, file_passphrase_cb, &pass_ctx)) {
      BIO_free(bio_in);
      OSSL_DECODER_CTX_free(dctx);
      return false;
    }
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
  pkey      = NULL;

  OSSL_DECODER_CTX_free(dctx);
  BIO_free(bio_in);

  return true;
}

static bool load_pkcs11_key(key_loader_t* loader, const key_spec_t* spec,
                            const key_credentials_t* credentials, key_handle_t* out) {

  (void)credentials;

  if (!loader || !spec || !out) {
    fprintf(stderr, "invalid or missing argument\n");
    return false;
  }

  out->pkey = NULL;

  if (spec->source != KEY_SOURCE_PKCS11 || !spec->u.pkcs11.uri) {
    fprintf(stderr, "Invalid or missing PKCS#11 URI\n");
    return false;
  }

  OSSL_STORE_CTX* store = NULL;
  EVP_PKEY*       pkey  = NULL;

  UI_METHOD* ui_method = UI_create_method("key-loader-pkcs11");
  if (!ui_method) {
    fprintf(stderr, "Failed to create ui method\n");
    OSSL_STORE_close(store);
    return false;
  }

  if (UI_method_set_reader(ui_method, pkcs11_ui_reader) != 0) {
    fprintf(stderr, "Failed to set PKCS#11 UI reader\n");
    OSSL_STORE_close(store);
    return false;
  }

  struct pkcs11_ui_ctx ui_ctx = {
      .credentials = credentials,
      .spec        = spec,
  };

  store = OSSL_STORE_open_ex(spec->u.pkcs11.uri, loader->libctx, NULL, ui_method, &ui_ctx, NULL,
                             NULL, NULL);

  if (!store) {
    fprintf(stderr, "Failed to open OSSL_store\n");
    return false;
  }

  if (!OSSL_STORE_expect(store, OSSL_STORE_INFO_PKEY)) {
    fprintf(stderr, "Failed to set expected store object type\n");
    OSSL_STORE_close(store);
    return false;
  }


  while (!OSSL_STORE_eof(store)) {
    OSSL_STORE_INFO* info = OSSL_STORE_load(store);

    if (!info) {
      if (OSSL_STORE_error(store)) {
        fprintf(stderr, "Failed to load object from store\n");

        ERR_print_errors_fp(stderr);
        OSSL_STORE_close(store);
        UI_destroy_method(ui_method);

        return false;
      }
      continue;
    }

    if (OSSL_STORE_INFO_get_type(info) == OSSL_STORE_INFO_PKEY) {
      pkey = OSSL_STORE_INFO_get1_PKEY(info);

      OSSL_STORE_INFO_free(info);

      if (!pkey) {
        OSSL_STORE_close(store);
        return false;
      }
      out->pkey = pkey;
      OSSL_STORE_close(store);
      return true;
    }
    OSSL_STORE_INFO_free(info);
  }

  OSSL_STORE_close(store);
  return false;
}

/*-----------------------------------------------------------*/

key_loader_t* key_loader_create(const key_loader_config_t* config) {

  key_loader_t* key_loader = calloc(1, sizeof(key_loader_t));
  if (!key_loader) {
    fprintf(stderr, "Failed to allocate memory for key_loader\n");
    return NULL;
  }

  OSSL_LIB_CTX* libctx = OSSL_LIB_CTX_new();
  if (!libctx) {
    fprintf(stderr, "Failed to create libctx from openssl\n");
    key_loader_destroy(key_loader);
    return NULL;
  }

  key_loader->libctx = libctx;

  key_loader->default_provider = OSSL_PROVIDER_load(key_loader->libctx, "default");

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

  char* module_path = OPENSSL_strdup(config->pkcs11_module_path);
  if (!module_path) {
    fprintf(stderr, "Failed to duplicate modulepath using openssl_strdup\n");
    key_loader_destroy(key_loader);
    return NULL;
  }

  OSSL_PARAM params[4];
  size_t     i = 0;
  params[i++]  = OSSL_PARAM_construct_utf8_string("pkcs11-module-path", module_path, 0);
  params[i++]  = OSSL_PARAM_construct_utf8_string("pkcs11-module-load-behavior", "early", 0);
  if (config->pkcs11_no_deinit) {
    params[i++] = OSSL_PARAM_construct_utf8_string("pkcs11-module-quirks", "no-deinit", 0);
  }
  params[i] = OSSL_PARAM_construct_end();

  const char* provider_name =
      config->pkcs11_provider_name != NULL ? config->pkcs11_provider_name : "pkcs11";

  key_loader->pkcs11_provider = OSSL_PROVIDER_load_ex(key_loader->libctx, provider_name, params);
  OPENSSL_free(module_path);

  if (!key_loader->pkcs11_provider) {
    fprintf(stderr, "Failed to load provider\n");
    key_loader_destroy(key_loader);
    return NULL;
  }

  return key_loader;
}

void key_loader_destroy(key_loader_t* loader) {
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

bool key_load(key_loader_t* loader, const key_spec_t* spec, const key_credentials_t* credentials,
              key_handle_t* out) {
  if (!loader || !spec || !out) {
    return false;
  }

  out->pkey = NULL;

  switch (spec->source) {
  case KEY_SOURCE_FILE:
    return load_file_key(loader, spec, credentials, out);
  case KEY_SOURCE_PKCS11:
    return load_pkcs11_key(loader, spec, credentials, out);
  default:
    return false;
  }

  return false;
}

void key_dispose(key_handle_t* handle) {
  if (!handle)
    return;

  if (!handle->pkey)
    return;

  EVP_PKEY_free(handle->pkey);
  handle->pkey = NULL;
}
