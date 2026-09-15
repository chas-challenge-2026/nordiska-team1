#ifndef KEY_LOAD_H
#define KEY_LOAD_H

#include <openssl/evp.h>
#include <stdbool.h>
#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/*
 *Key loader
 *
 * Long-lived key-loading context.
 *
 * Owns the OpenSSL library context and providers used by the key-loading
 * module.
 *
 * The struct is private to key_load.c and exposed publically only as typedef
 * struct key_loader key_loader_t;
 *
 * A key_loader instance must remain alive for as long as any EVP_PKEY loaded
 * through it is still i use.
 *
 * Ownership:
 *  libctx:
 *    Owned by key_loader.
 *    Created during key_loader_create()
 *    Release during key_loader_destroy()
 *
 *  default_provider:
 *    Owned by key_loader.
 *    Loaded into libctx during initilazation.
 *    Released before libctc is destroyed.
 *
 *  pkcs11_provider:
 *    Owned by keyloader when PKCS#11 support is configured.
 *    NULL when PKCS#11 is not initialized or not enabled.
 *    Released before libctx is destroyed.
 *
 *  Lifetime:
 *
 *   key_loader_create()
 *           |
 *           v
 *      key_loader
 *           |
 *           +---- key_load(...)
 *           +---- key_load(...)
 *           |
 *           v
 *     key_loader_destroy()
 *
 * All keys loaded through the loader must be disposed before
 * key_loader_destroy() is called.
 *
 * No caller accessible key identity or credential data is stored here.
 *
 *
 */

typedef struct key_loader key_loader_t;

/*
 * key_loader_config_t
 *
 * Trusted configuration used when creating a keyh loader
 *
 * PKCS#11 provider/module configuration belongs here, not in individual
 * key_specs
 *
 * All strings are borrowed for the duration of key_loader_create()
 * The implementation should copy any values it needs afterwards
 *
 */

typedef struct {
  const char *pkcs11_provider_name; // NULL may be used to select module default
                                    // provider
  const char *pkcs11_module_path;   // NULL if pkcs#11 is not required
} key_loader_config_t;

/*
 * key_source_t
 * Supported private-key sources
 */

typedef enum {
  KEY_SOURCE_FILE = 1, // Local key
  KEY_SOURCE_PKCS11
} key_source_t;

/*
 *key_spec_t
 * Identifies  the private key to load
 *
 * Contains only key Identity
 *
 * Should never contain:
 *  - passwords
 *  - passphrases
 *  - pins
 *  - provider configuration
 *  - moduele paths
 *
 *  Strings are owned by the caller and must remain valid until key_load()
 * returns
 *
 */

typedef struct {
  key_source_t source;

  union {
    /*
     * Local PEM/DER private key
     */
    struct {
      const char *path;
    } file;

    /*
     * PKCS#11-backed private key
     * URI identifies key object
     */
    struct {
      const char *uri;
    } pkcs11;
  } u;
} key_spec_t;

/*
 * key_secret_kind_t
 * Type of credential requested by key_loader
 */

typedef enum {
  KEY_SECRET_FILE_PASSPHRASE = 1,
  KEY_SECRET_PKCS11_PIN
} key_secret_kind_t;

/*
 * key_secret_reult_t
 * Result from a credential callback
 */

typedef enum {
  KEY_SECRET_OK = 0,
  KEY_SECRET_UNAVAILABLE = 1,
  KEY_SECRET_BUFFER_TOO_SMALL = 2,
  KEY_SECRET_ERROR = -1
} key_secret_result_t;

/*
 * Callback used to provide credentials on demand
 *
 * Buffer ownership:
 *  Buffer is owned by key-loading module
 *  callback recieves only temporary write access
 *  callback must not retain, free, resize or use the buffer after returning
 *
 *
 * On KEY_SECRET_OK
 * write the credential bytes into buffer
 * set secret_len to the number of bytes written
 * secret_len must not exceed buffer_len
 * No NULL terminator required
 *
 * The key-loading module is responsible for securely cleansing every temporary
 * credential buffer that it owns before releasing it.
 *
 * userdata is owned by the caller. The module does not copy, cleanse or free
 * it.
 */

typedef key_secret_result_t (*key_secret_callback_t)(
    key_secret_kind_t kind, const key_spec_t *spec, unsigned char *buffer,
    size_t buffer_len, size_t *secret_len, void *userdata);

/*
 * Credential provider used during key key loading.
 *
 * May be omitted when the selected key requires no authentication
 */

typedef struct {
  key_secret_callback_t callback;
  void *userdata;
} key_credentials_t;

/*
 * key_handle_t
 * Owns a successfully loaded private key.
 * The handle should be zero-initialized before use.
 *
 * A successful key_load() sets pkey to a valid EVP_PKEY
 *
 * Release the key using key_dispose()
 */

typedef struct {
  EVP_PKEY *pkey;
} key_handle_t;

/*
 * Creates a key loader and initializes the required backend state.
 *
 * Returns:
 *  non-NULL on success
 *  NULL on failure
 */
key_loader_t *key_loader_create(const key_loader_config_t *config);

/*
 * Destroys a key loader.
 *
 * All keys loaded through this loader must have been disposed before
 * this function is called.
 *
 * Safe to call with NULL.
 */
void key_loader_destroy(key_loader_t *loader);

/*
 * Loads the private key identified by spec.
 *
 * credentials may be NULL if authentication is not required.
 *
 * Output invariant:
 *
 *  success:
 *    return true
 *    out->pkey != NULL
 *
 *   failure:
 *     return false
 *     out->pkey == NULL
 *
 * No partially initialized key handle is returned.

 */
bool key_load(key_loader_t *loader, const key_spec_t *spec,
              const key_credentials_t *credentials, key_handle_t *out);

/*
 * Releases the private key owned by a key handle.
 *
 * Safe to call with:
 *
 *  - NULL
 *  - a zero-initialized handle
 *  - a handle from a failed key_load()
 *  - an already-disposed handle
 *
 * After returning:
 *
 *  handle == NULL
 *
 * or:
 *
 *  handle->pkey == NULL
 */
void key_dispose(key_handle_t *handle);

#ifdef __cplusplus
}
#endif
#endif
