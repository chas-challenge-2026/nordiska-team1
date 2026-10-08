#ifndef KEY_LOAD_H
#define KEY_LOAD_H

#include <openssl/evp.h>
#include <openssl/store.h>
#include <stdbool.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/*
 * Long-lived key-loading context.
 *
 * Owns the OpenSSL library context and providers used to load keys.
 * The loader must outlive every EVP_PKEY loaded through it.
 */
typedef struct key_loader key_loader_t;

typedef enum
{
  KEY_STATUS_OK = 0,

  KEY_STATUS_INVALID_ARGUMENT,
  KEY_STATUS_UNSUPPORTED_SOURCE,
  KEY_STATUS_CREDENTIAL_REQUIRED,
  KEY_STATUS_CREDENTIAL_FAILED,
  KEY_STATUS_KEY_NOT_FOUND,
  KEY_STATUS_LOAD_FAILED,
  KEY_STATUS_BACKEND_UNAVAILABLE,
  KEY_STATUS_INTERNAL_ERROR
} key_status_t;


/*
 * Backend configuration for a key loader.
 *
 * Strings are borrowed for the duration of key_loader_create().
 */
typedef struct
{
  bool        pkcs11_enabled;
  const char* pkcs11_provider_name;
  const char* pkcs11_module_path;
  bool        pkcs11_no_deinit;
} key_loader_config_t;


/* Supported private-key sources. */
typedef enum
{
  KEY_SOURCE_FILE = 1,
  KEY_SOURCE_PKCS11
} key_source_t;


/*
 * Identifies a private key.
 *
 * Contains key identity only. Credentials and provider configuration are
 * supplied separately.
 */
typedef struct
{
  key_source_t source;

  union {

    struct
    {
      const char* path;
    } file;


    struct
    {
      const char* uri;
    } pkcs11;
  } u;
} key_spec_t;

/* Credential type requested by the key loader. */
typedef enum
{
  KEY_SECRET_FILE_PASSPHRASE = 1,
  KEY_SECRET_PKCS11_PIN
} key_secret_kind_t;


/* Result returned by a credential callback. */
typedef enum
{
  KEY_SECRET_OK               = 0,
  KEY_SECRET_UNAVAILABLE      = 1,
  KEY_SECRET_BUFFER_TOO_SMALL = 2,
  KEY_SECRET_ERROR            = -1
} key_secret_result_t;

/*
 * Supplies credentials on demand.
 *
 * On success, write the credential bytes to buffer and set secret_len.
 * No null terminator is required. The callback does not own the buffer.
 */
typedef key_secret_result_t (*key_secret_callback_t)(key_secret_kind_t kind, const key_spec_t* spec,
                                                     unsigned char* buffer, size_t buffer_len,
                                                     size_t* secret_len, void* userdata);


/* Credential provider used while loading a key. */
typedef struct
{
  key_secret_callback_t callback;
  void*                 userdata;
} key_credentials_t;


/* Owns a successfully loaded private key. */
typedef struct
{
  EVP_PKEY* pkey;
} key_handle_t;


/*
 * Creates a key loader and initializes its OpenSSL backend state.
 *
 * Returns NULL on failure.
 */
key_loader_t* key_loader_create(const key_loader_config_t* config);


/*
 * Destroys a key loader.
 *
 * All keys loaded through it must be disposed first.
 */
void key_loader_destroy(key_loader_t* loader);

/*
 * Loads the private key identified by spec.
 *
 * On success, out->pkey is non-NULL. On failure, out->pkey is NULL.
 */
key_status_t key_load(key_loader_t* loader, const key_spec_t* spec,
                      const key_credentials_t* credentials, key_handle_t* out);


/* Releases the private key owned by the handle. */
void key_dispose(key_handle_t* handle);

#ifdef __cplusplus
}
#endif
#endif
