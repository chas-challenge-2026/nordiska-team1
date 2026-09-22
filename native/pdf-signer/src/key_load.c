#include "key_load.h"
#include <openssl/decoder.h>
#include <openssl/params.h>
#include <openssl/provider.h>
#include <openssl/store.h>
#include <string.h>
#include <openssl/ui.h>
#include <openssl/err.h>
#include "debug_log.h"

/*-------------------Internal helpers------------------------*/
static key_secret_result_t request_secret(const key_credentials_t* credentials,
                                          key_secret_kind_t kind, const key_spec_t* spec,
                                          unsigned char** out_buf, size_t* out_len);
static int                 file_passphrase_cb(char* pass, size_t pass_size, size_t* pass_len,
                                              const OSSL_PARAM params[], void* arg);

#define KEY_SECRET_MAX_LEN 1024
/*----------------------------------------------------------*/

struct key_loader
{
  OSSL_LIB_CTX*  libctx;
  OSSL_PROVIDER* default_provider;
  OSSL_PROVIDER* pkcs11_provider;
};

typedef struct
{
  BIO*              bio;
  OSSL_DECODER_CTX* decoder;
  OSSL_STORE_CTX*   store;
  OSSL_STORE_INFO*  store_info;
  UI_METHOD*        ui_method;
  EVP_PKEY*         pkey;
} key_load_resources_t;

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

static void cleanup_key_load_resources(key_load_resources_t* resources) {
  if (!resources) {
    return;
  }

  if (resources->store_info) {
    OSSL_STORE_INFO_free(resources->store_info);
    resources->store_info = NULL;
  }

  if (resources->pkey) {
    EVP_PKEY_free(resources->pkey);
    resources->pkey = NULL;
  }

  if (resources->decoder) {
    OSSL_DECODER_CTX_free(resources->decoder);
    resources->decoder = NULL;
  }

  if (resources->bio) {
    BIO_free(resources->bio);
    resources->bio = NULL;
  }

  if (resources->store) {
    OSSL_STORE_close(resources->store);
    resources->store = NULL;
  }

  if (resources->ui_method) {
    UI_destroy_method(resources->ui_method);
    resources->ui_method = NULL;
  }
}

static int file_passphrase_cb(char* pass, size_t pass_size, size_t* pass_len,
                              const OSSL_PARAM params[], void* arg) {
  (void)params;
  if (!arg || !pass || !pass_len) {
    return 0;
  }
  struct file_passphrase_ctx* ctx = arg;

  unsigned char* secret     = NULL;
  size_t         secret_len = 0;

  if (!ctx || !pass || !pass_len) {
    return 0; // 0 = Error & 1 = Success for OpenSSL
  }

  if (request_secret(ctx->credentials, KEY_SECRET_FILE_PASSPHRASE, ctx->spec, &secret,
                     &secret_len) != KEY_SECRET_OK) {
    debug_log("Failed to get secret");
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

  enum UI_string_types type = UI_get_string_type(uis);


  // Reader can get multiple types of UI_STRING
  if (type != UIT_PROMPT && type != UIT_VERIFY) {
    return 1;
  }

  struct pkcs11_ui_ctx* ctx = UI_get0_user_data(ui);
  if (!ctx) {
    debug_log("Missing PKCS#11 UI context");
    return 0;
  }

  unsigned char* secret     = NULL;
  size_t         secret_len = 0;

  if (request_secret(ctx->credentials, KEY_SECRET_PKCS11_PIN, ctx->spec, &secret, &secret_len) !=
      KEY_SECRET_OK) {
    debug_log("Failed to get PKCS#11 PIN");
    return 0;
  }

  int res = UI_set_result_ex(ui, uis, (const char*)secret, (int)secret_len);

  release_secret(&secret);

  if (res < 0) {
    debug_log("Failed to set PKCS#11 PIN in OpenSSL UI");
    return 0;
  }

  return 1;
}

static key_secret_result_t request_secret(const key_credentials_t* credentials,
                                          key_secret_kind_t kind, const key_spec_t* spec,
                                          unsigned char** out_buf, size_t* out_len) {

  if (!out_len || !out_buf) {
    debug_log("out_len & out_buf cannot be NULL");
    return KEY_SECRET_ERROR;
  }

  *out_buf = NULL;
  *out_len = 0;

  if (!credentials || !credentials->callback) {
    debug_log("Credentials & credential callback required");
    return KEY_SECRET_ERROR;
  }

  if (!spec) {
    debug_log("key_spec_t cannot be NULL");
    return KEY_SECRET_ERROR;
  }
  unsigned char* secret_buf = OPENSSL_zalloc(KEY_SECRET_MAX_LEN);
  if (!secret_buf) {
    debug_log("Failed to allocate memory for secret_buf");
    return KEY_SECRET_ERROR;
  }

  size_t secret_len = 0;

  key_secret_result_t res = credentials->callback(kind, spec, secret_buf, KEY_SECRET_MAX_LEN,
                                                  &secret_len, credentials->userdata);

  if (res != KEY_SECRET_OK || secret_len > KEY_SECRET_MAX_LEN || secret_len == 0) {
    debug_log("credential callback failed");
    release_secret(&secret_buf);
    return KEY_SECRET_ERROR;
  }

  *out_len = secret_len;
  *out_buf = secret_buf;

  return KEY_SECRET_OK;
}

static key_status_t load_file_key(key_loader_t* loader, const key_spec_t* spec,
                                  const key_credentials_t* credentials, key_handle_t* out) {

  if (!loader || !spec || !out) {
    debug_log("invalid or missing argument");
    return KEY_STATUS_INVALID_ARGUMENT;
  }

  out->pkey = NULL;

  if (spec->source != KEY_SOURCE_FILE || !spec->u.file.path) {
    debug_log("Invalid or missing path");
    return KEY_STATUS_INVALID_ARGUMENT;
  }
  key_load_resources_t resources = {0};

  resources.bio = BIO_new_file(spec->u.file.path, "rb");
  if (!resources.bio) {
    debug_log("Failed to open key-file");
    cleanup_key_load_resources(&resources);
    return KEY_STATUS_LOAD_FAILED;
  }

  resources.decoder = OSSL_DECODER_CTX_new_for_pkey(
      &resources.pkey, NULL, NULL, NULL, OSSL_KEYMGMT_SELECT_PRIVATE_KEY, loader->libctx, NULL);

  if (!resources.decoder) {
    debug_log("Failed to create decoder context");
    cleanup_key_load_resources(&resources);
    return KEY_STATUS_INTERNAL_ERROR;
  }

  struct file_passphrase_ctx pass_ctx = {
      .credentials = credentials,
      .spec        = spec,
  };

  if (credentials && credentials->callback) {
    if (!OSSL_DECODER_CTX_set_passphrase_cb(resources.decoder, file_passphrase_cb, &pass_ctx)) {
      cleanup_key_load_resources(&resources);
      return KEY_STATUS_INTERNAL_ERROR;
    }
  }
  if (OSSL_DECODER_from_bio(resources.decoder, resources.bio) != 1) {
    debug_log("Failed to decode context");
    cleanup_key_load_resources(&resources);
    return KEY_STATUS_LOAD_FAILED;
  }

  if (!resources.pkey) {
    debug_log("unable to retrieve pkey");
    cleanup_key_load_resources(&resources);
    return KEY_STATUS_INTERNAL_ERROR;
  }

  out->pkey      = resources.pkey;
  resources.pkey = NULL;

  cleanup_key_load_resources(&resources);
  return KEY_STATUS_OK;
}

static key_status_t load_pkcs11_key(key_loader_t* loader, const key_spec_t* spec,
                                    const key_credentials_t* credentials, key_handle_t* out) {


  if (!loader || !spec || !out) {
    debug_log("invalid or missing argument");
    return KEY_STATUS_INVALID_ARGUMENT;
  }

  out->pkey = NULL;

  if (spec->source != KEY_SOURCE_PKCS11 || !spec->u.pkcs11.uri) {
    debug_log("Invalid or missing PKCS#11 URI");
    return KEY_STATUS_INVALID_ARGUMENT;
  }

  key_load_resources_t resources = {0};

  resources.ui_method = UI_create_method("key-loader-pkcs11");
  if (!resources.ui_method) {
    debug_log("Failed to create ui method");
    cleanup_key_load_resources(&resources);
    return KEY_STATUS_INTERNAL_ERROR;
  }

  if (UI_method_set_reader(resources.ui_method, pkcs11_ui_reader) != 0) {
    debug_log("Failed to set PKCS#11 UI reader");
    cleanup_key_load_resources(&resources);
    return KEY_STATUS_INTERNAL_ERROR;
  }

  struct pkcs11_ui_ctx ui_ctx = {
      .credentials = credentials,
      .spec        = spec,
  };

  resources.store = OSSL_STORE_open_ex(spec->u.pkcs11.uri, loader->libctx, NULL,
                                       resources.ui_method, &ui_ctx, NULL, NULL, NULL);

  if (!resources.store) {
    debug_log("Failed to open OSSL_store");
    cleanup_key_load_resources(&resources);
    return KEY_STATUS_BACKEND_UNAVAILABLE;
  }

  if (!OSSL_STORE_expect(resources.store, OSSL_STORE_INFO_PKEY)) {
    debug_log("Failed to set expected store object type");
    cleanup_key_load_resources(&resources);
    return KEY_STATUS_INTERNAL_ERROR;
  }


  while (!OSSL_STORE_eof(resources.store)) {
    resources.store_info = OSSL_STORE_load(resources.store);

    if (!resources.store_info) {
      if (OSSL_STORE_error(resources.store)) {
        debug_log("Failed to load object from store");
        cleanup_key_load_resources(&resources);
        return KEY_STATUS_LOAD_FAILED;
      }
      continue;
    }

    if (OSSL_STORE_INFO_get_type(resources.store_info) == OSSL_STORE_INFO_PKEY) {
      resources.pkey = OSSL_STORE_INFO_get1_PKEY(resources.store_info);

      if (!resources.pkey) {
        cleanup_key_load_resources(&resources);
        return KEY_STATUS_INTERNAL_ERROR;
      }
      out->pkey      = resources.pkey;
      resources.pkey = NULL;
      cleanup_key_load_resources(&resources);
      return KEY_STATUS_OK;
    }
    OSSL_STORE_INFO_free(resources.store_info);
    resources.store_info = NULL;
  }

  cleanup_key_load_resources(&resources);
  return KEY_STATUS_KEY_NOT_FOUND;
}

/*-----------------------------------------------------------*/

key_loader_t* key_loader_create(const key_loader_config_t* config) {

  key_loader_t* key_loader = calloc(1, sizeof(key_loader_t));
  if (!key_loader) {
    debug_log("Failed to allocate memory for key_loader");
    return NULL;
  }

  OSSL_LIB_CTX* libctx = OSSL_LIB_CTX_new();
  if (!libctx) {
    debug_log("Failed to create libctx from openssl");
    key_loader_destroy(key_loader);
    return NULL;
  }

  key_loader->libctx = libctx;

  key_loader->default_provider = OSSL_PROVIDER_load(key_loader->libctx, "default");

  if (!key_loader->default_provider) {
    debug_log("Failed to load OSSL default provider");
    key_loader_destroy(key_loader);
    return NULL;
  }

  if (!config || !config->pkcs11_enabled) {
    return key_loader;
  }

  if (!config->pkcs11_module_path) {
    debug_log("pkcs11 module path required when pkcs11 is enabled");
    key_loader_destroy(key_loader);
    return NULL;
  }

  char* module_path = OPENSSL_strdup(config->pkcs11_module_path);
  if (!module_path) {
    debug_log("Failed to duplicate modulepath using openssl_strdup");
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

#if OPENSSL_VERSION_NUMBER >= 0x30200000L
  key_loader->pkcs11_provider = OSSL_PROVIDER_load_ex(key_loader->libctx, provider_name, params);
#else
  (void)params;
  key_loader->pkcs11_provider = OSSL_PROVIDER_load(key_loader->libctx, provider_name);
#endif

  OPENSSL_free(module_path);

  if (!key_loader->pkcs11_provider) {
    debug_log("Failed to load provider");
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

key_status_t key_load(key_loader_t* loader, const key_spec_t* spec,
                      const key_credentials_t* credentials, key_handle_t* out) {
  if (!loader || !spec || !out) {
    return KEY_STATUS_INVALID_ARGUMENT;
  }

  if (out->pkey != NULL) {
    return KEY_STATUS_INVALID_ARGUMENT;
  }

  switch (spec->source) {
  case KEY_SOURCE_FILE:
    return load_file_key(loader, spec, credentials, out);
  case KEY_SOURCE_PKCS11:
    return load_pkcs11_key(loader, spec, credentials, out);
  default:
    return KEY_STATUS_UNSUPPORTED_SOURCE;
  }

  return KEY_STATUS_UNSUPPORTED_SOURCE;
}

void key_dispose(key_handle_t* handle) {
  if (!handle)
    return;

  if (!handle->pkey)
    return;

  EVP_PKEY_free(handle->pkey);
  handle->pkey = NULL;
}
