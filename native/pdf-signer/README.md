# Key Loading Module

Standalone OpenSSL 3.x key-loading module used by the native signer.

The module loads private keys and exposes them as `EVP_PKEY *` through a small
application-facing API. It supports both local private-key files and
PKCS#11-backed keys such as SoftHSM/HSM keys.

## Responsibilities

The module is responsible for:

- creating and owning a dedicated `OSSL_LIB_CTX`
- explicitly loading the OpenSSL default provider
- optionally loading a PKCS#11 provider
- loading local PEM/DER private keys
- loading PKCS#11 private keys from RFC 7512 `pkcs11:` URIs
- requesting passphrases/PINs on demand through a caller-provided callback
- cleansing temporary credential buffers owned by the module
- returning loaded private keys as `EVP_PKEY *`
- cleaning up OpenSSL resources owned by the loader

## Main API concepts

### `key_loader_t`

Opaque loader object. It owns the OpenSSL library context and loaded providers.

```c
key_loader_t *loader = key_loader_create(NULL);
```

Destroy it with:

```c
key_loader_destroy(loader);
```

`key_loader_destroy(NULL)` is safe.

All keys loaded through a loader must be disposed before the loader itself is
destroyed.

### `key_spec_t`

Describes where the private key lives.

Local file:

```c
key_spec_t spec = {
    .source = KEY_SOURCE_FILE,
    .u.file.path = "tests/data/private_key.pem",
};
```

PKCS#11:

```c
key_spec_t spec = {
    .source = KEY_SOURCE_PKCS11,
    .u.pkcs11.uri =
        "pkcs11:token=key-load-dev;"
        "object=pdf-signer-test;"
        "type=private",
};
```

Credentials are intentionally not stored in `key_spec_t`.

### `key_handle_t`

Contains the loaded `EVP_PKEY *`.

```c
key_handle_t handle = {0};
```

Dispose it with:

```c
key_dispose(&handle);
```

After disposal, `handle.pkey == NULL`.

`key_dispose(NULL)` and disposing a zero-initialized handle are safe.

A handle should be empty before it is passed to `key_load()`.

## Loading a local private key

Unencrypted PEM or DER files can be loaded without credentials.

```c
key_loader_t *loader = key_loader_create(NULL);
if (!loader) {
    /* handle loader creation failure */
}

key_spec_t spec = {
    .source = KEY_SOURCE_FILE,
    .u.file.path = "private_key.pem",
};

key_handle_t handle = {0};

key_status_t status =
    key_load(loader, &spec, NULL, &handle);

if (status != KEY_STATUS_OK) {
    /* handle load failure */
}

EVP_PKEY *pkey = handle.pkey;

/* use pkey */

key_dispose(&handle);
key_loader_destroy(loader);
```

The file backend uses OpenSSL's decoder framework and supports PEM/DER
auto-detection.

## Loading an encrypted PEM key

Encrypted private keys use the credential callback.

```c
typedef struct {
    const unsigned char *secret;
    size_t secret_len;
} app_secret_ctx_t;

static key_secret_result_t secret_callback(
    key_secret_kind_t kind,
    const key_spec_t *spec,
    unsigned char *buffer,
    size_t buffer_len,
    size_t *secret_len,
    void *userdata)
{
    (void)spec;

    app_secret_ctx_t *ctx = userdata;

    if (!ctx || !buffer || !secret_len)
        return KEY_SECRET_ERROR;

    if (kind != KEY_SECRET_FILE_PASSPHRASE)
        return KEY_SECRET_ERROR;

    if (ctx->secret_len > buffer_len)
        return KEY_SECRET_ERROR;

    memcpy(buffer, ctx->secret, ctx->secret_len);
    *secret_len = ctx->secret_len;

    return KEY_SECRET_OK;
}
```

Use it with:

```c
static const unsigned char passphrase[] = "example-passphrase";

app_secret_ctx_t secret_ctx = {
    .secret = passphrase,
    .secret_len = sizeof(passphrase) - 1,
};

key_credentials_t credentials = {
    .callback = secret_callback,
    .userdata = &secret_ctx,
};

key_status_t status =
    key_load(loader, &spec, &credentials, &handle);
```

The callback receives temporary write access to a buffer owned by the
key-loading module. It must not retain, free, resize, or use that buffer after
returning.

The module cleanses its own temporary credential buffers before freeing them.

## PKCS#11 configuration

PKCS#11 support is enabled when creating the loader.

```c
key_loader_config_t config = {
    .pkcs11_enabled = true,
    .pkcs11_provider_name = "pkcs11",
    .pkcs11_module_path = "/usr/lib/libsofthsm2.so",
    .pkcs11_no_deinit = true,
};

key_loader_t *loader = key_loader_create(&config);
```

If `pkcs11_provider_name` is `NULL`, the provider name defaults to `pkcs11`.

`pkcs11_module_path` is required when PKCS#11 is enabled.

`pkcs11_no_deinit` is an optional compatibility workaround for environments
where provider/module teardown causes problems.

The loader configures the PKCS#11 module for early loading so invalid module
paths fail during loader creation.

## Loading a PKCS#11 private key

The key is identified using an RFC 7512 PKCS#11 URI.

```c
key_spec_t spec = {
    .source = KEY_SOURCE_PKCS11,
    .u.pkcs11.uri =
        "pkcs11:token=key-load-dev;"
        "object=pdf-signer-test;"
        "type=private",
};
```

PIN delivery uses the same credential API as encrypted file passphrases.

In the callback:

```c
if (kind != KEY_SECRET_PKCS11_PIN)
    return KEY_SECRET_ERROR;
```

Then:

```c
key_status_t status =
    key_load(loader, &spec, &credentials, &handle);

if (status == KEY_STATUS_OK) {
    EVP_PKEY *pkey = handle.pkey;
    /* provider-backed private key */
}
```

The PKCS#11 backend uses `OSSL_STORE` and an OpenSSL `UI_METHOD` bridge so the
provider can request a PIN on demand.

Do not put PIN values in the PKCS#11 URI.

## Ownership and lifecycle

Recommended lifecycle:

```text
key_loader_create()
        |
        v
key_load()
        |
        v
key_handle_t / EVP_PKEY
        |
        v
key_dispose()
        |
        v
key_loader_destroy()
```

Important rules:

- the loader owns its `OSSL_LIB_CTX` and providers
- `key_handle_t` owns the returned `EVP_PKEY *`
- `key_dispose()` releases that `EVP_PKEY`
- dispose all keys before destroying the loader
- the credential callback does not own the temporary secret buffer
- the module does not own `credentials->userdata`

## Error handling

`key_load()` returns a `key_status_t`.

Success:

```c
KEY_STATUS_OK
```

Current high-level errors distinguish categories such as:

- invalid arguments
- unsupported key source
- backend unavailable
- key not found
- load failure
- internal failure

The status API is intentionally high-level. OpenSSL/provider implementation
details should not become part of the public API unless callers need to act on
them.

Temporary development diagnostics are currently written to `stderr`. Logging
is expected to move to an application-owned logging interface later.

## Building

From `native/pdf-signer`:

```bash
make
```

Clean:

```bash
make clean
```

## Running tests

Run all tests:

```bash
make test
```

Run unit tests only:

```bash
make test-unit
```

Run PKCS#11 integration tests only:

```bash
make test-pkcs11
```

### Unit tests

The unit-test target is built with AddressSanitizer and
UndefinedBehaviorSanitizer.

It covers:

- loader creation/destruction
- handle disposal
- unencrypted PEM
- unencrypted DER
- encrypted PEM
- correct passphrase
- incorrect passphrase
- missing credentials
- credential callback failure
- missing files
- unsupported key sources

### PKCS#11 integration tests

PKCS#11 tests use the real OpenSSL PKCS#11 provider and SoftHSM.

They intentionally run without ASan/UBSan because the provider/SoftHSM dynamic
module-loading path is not compatible with the sanitizer setup used by the
unit tests.

The Makefile should execute the integration test with the project-local
SoftHSM configuration:

```makefile
test-pkcs11: $(PKCS11_BIN)
	SOFTHSM2_CONF=$(CURDIR)/.dev/softhsm/softhsm2.conf ./$(PKCS11_BIN)
```

This prevents the test from depending on an exported shell variable.

## SoftHSM development environment

The development environment uses:

```text
SoftHSM token label: key-load-dev
test key label:      pdf-signer-test
key ID:              01
```

Example URI:

```text
pkcs11:token=key-load-dev;object=pdf-signer-test;type=private
```

Verify the token is visible:

```bash
SOFTHSM2_CONF="$PWD/.dev/softhsm/softhsm2.conf" softhsm2-util --show-slots
```

Verify objects interactively:

```bash
SOFTHSM2_CONF="$PWD/.dev/softhsm/softhsm2.conf" pkcs11-tool   --module /usr/lib/libsofthsm2.so   --token-label key-load-dev   --login   --list-objects
```

Enter the user PIN interactively instead of placing it in command history.

## Security notes

- never log PINs or private-key passphrases
- do not put PIN values in PKCS#11 URIs
- do not keep credentials in `key_spec_t`
- temporary secret buffers owned by this module are cleansed before release
- provider/OpenSSL internals may make their own copies of credentials; the
  module can only guarantee cleansing of buffers it owns
- PKCS#11 private keys remain provider-backed; the module does not extract
  private key material from the token
- keep the `key_loader_t` alive while keys loaded through it are still in use

## Current scope



```text
Local PEM private keys
Local DER private keys
Encrypted PEM private keys
PKCS#11 private keys
Credential callbacks
SoftHSM integration tests
OpenSSL provider/libctx lifecycle
```
