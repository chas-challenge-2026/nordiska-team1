# Overview

Key-load module should accept both local keys such as PEM/DER and PKSC#11 HSM.

A successful operation should produce a usable OpenSSL `EVP_PKEY`. On failure, no key is returned and the output key handle remains empty.

Three types of information is held separetly:

* Key identity - What key is being used
* Credentials - Used to authenticate access to key
* Backend config - Describes how PKCS#11/OpenSSL backend is configured





## Resposibility - What does the module do?

* Accept description of what private key is requested
* Decide what backend to use
* Get credentials with callback when needed
* Load key from local file or PKCS#11
* Return key as a EVP_PKEY
* Handle backend-specific resources
* Handle temporary credentials
* Securely clear module-owned bufferts after use
* Uphold ownership- & lifetime rules



Backend-specific details should be hidden from caller.

The module is not responsible for how the key is used after loading, but it defines how the returned key and related backend resources must be disposed.

## Inputs

Module has 3 types of input



### Backend-configuration

Backend-configuration describes how the key-loading environment works, not what key to use.

It belongs to a long-lifetime key_loader_t 

It can contain:

* OpenSSL library context
* What openSSL providers are used
* PKCS#11 provider config

key_loader_t should be opaque

Lifecycle example:

```
Program start
     |
     v
key_loader_create()
     |
     +--> initialize OpenSSL
     +--> initialize providers
     +--> configure PKCS#11
     |
     +--> key_load(...)
     |
     +--> key_load(...)
     |
     +--> key_load(...)
     |
     v
key_loader_destroy()
     |ain
     v
Program end
```



### Key-specification

key_spec_t decsribes what key the caller wants to use.

Will NOT contain credentials or backend-configuration



Example: (May change)

```c
typedef enum {
    KEY_SOURCE_FILE = 1,
    KEY_SOURCE_PKCS11
} key_source_t;

typedef struct {
    key_source_t source;
	union {
    	struct {
        	const char *path;
    	} file;
    	struct {
        	const char *uri;
    	} pkcs11;
	} u;
} key_spec_t;
```


### Credentials

Supplied separately through a credentials_t

Module-owned credential data should only remain in memory for as long as required by the key-loading operation

Credential callback is used to get a secret on demand

(May change)

```
typedef struct {
    key_secret_callback secret_callback;
    void *userdata;
} key_credentials_t;
```



## Output

A successful key_load operation should return a private key represented as an OpenSSL EVP_KEY.

typedef struct {    

​	EVP_PKEY *pkey;

 } key_handle_t;



### Success

```
status == KEY_LOAD_OK
out->pkey != NULL
```

### Failure

```
status != KEY_LOAD_OK
out->pkey == NULL
```

Module will never return a partially initialized key

Typical flow:

```
key_handle_t key = {0};

status = key_load(
    loader,
    &spec,
    &credentials,
    &key
);

if (status != KEY_LOAD_OK) {
    /* handle error */
}

/* Use key.pkey */

key_dispose(&key);
```



### Key-spec - What keys are supported?



```
      			key_spec_t
                      |
            +---------+---------+
            |                   |
            v                   v
     KEY_SOURCE_FILE     KEY_SOURCE_PKCS11
            |                   |
            v                   v
        file path          PKCS#11 URI
```

## 

### Local keys

Uses path to file

```
/etc/myapp/keys/signing-key.pem
```

```
/var/lib/myapp/keys/signing-key.der
```

key_spec only contains the path to the key it does NOT contain a passphrase. If a passphrase is required it needs to be fetched with the callback



### PKCS#11/HSM

Uses PKCS#11 uri

```
pkcs11:token=SigningHSM;object=signing-key;type=private
```

URI describes the object to be used. Should NOT contain PIN or backend-config



```
PKCS#11 URI
     |
     v
"What key?"
```

```
key_loader_t
     |
     v
"What PKCS#11 implementation?"
```

```
credentials
     |
     v
"How to authenticate?"
```

URI should be specific enough to identify the intended key

It may use one of the following:

- token
- token serial
- object
- object ID
- object type

If a key specification cannot **uniquely identify** the intended key, the operation should fail.



### Credentials 

Credential-handling should minimize how long PIN/Passphrases remain in memory, will be fetched on demand using a callback.



Example flow:

```
                 key_load()
                     |
                     v
              Need credential?
                 /       \
                no        yes
                |          |
                |          v
                |      callback
                |          |
                |          v
                |   +-------------+
                |   | temporary   |
                |   | secret buf  |
                |   +-------------+
                |          |
                |          v
                |       backend
                |          |
                |          v
                |    secure cleanse
                |          |
                +----------+
                     |
                     v
                  result
```



The temporary secret buffert is owned by the key_loading module and the callback just gets temporary write access.

The callback may NOT:

* Retain bufferpointer
* Cache bufferpointer
* Free the buffer
* Change the size of the buffer
* Use the buffer after returning



Cleanup should occur on:

* Success
* callback error
* incorrect credentials
* parsing failure
* key not found
* backend failure
* other errors after buffert has been created



Example:

```
allocate
   |
   v
obtain secret
   |
   v
use secret
   |
   v
cleanse entire capacity
   |
   v
free
```



### Ownership & Lifetime

Ownership needs to be explicit for all items that pass the module-line.



Ownership-table

| Item                    | Owner           | Lifetime                                                     |
| ----------------------- | --------------- | ------------------------------------------------------------ |
| `key_loader_t`          | Caller          | From`key_loader_create()` to `key_loader_destroy()`          |
| Backend-config          | Caller / loader | At least during loader-init, depending of if data is being copied |
| `key_spec_t`            | Caller          | Until `key_load()` returns                                   |
| Strings in `key_spec_t` | Caller          | Until `key_load()` returns                                   |
| `key_credentials_t`     | Caller          | Until `key_load()` returns                                   |
| Credential callback     | Caller          | Until `key_load()` returns                                   |
| Credential `userdata`   | Caller          | Defined by caller                                            |
| Temporär secret-buffer  | Key loader      | Only during credential operation                             |
| Backend/provider state  | Key loader      | Loader lifetime                                              |
| `key_handle_t`          | Caller          | Until `key_dispose()`                                        |
| `EVP_PKEY` i handle     | `key_handle_t`  | Until `key_dispose()`                                        |

## 

## Loader lifetime

> `key_loader_t` must live longer than all the keys loaded through it
>
> Correct order:

```
key_loader_create()
        |
        v
     key_load()
        |
        v
     use key
        |
        v
  key_dispose()
        |
        v
key_loader_destroy()
```

Incorrect order:

```
key_loader_create()
        |
        v
     key_load()
        |
        v
key_loader_destroy()
        |
        v
     use key       <- Not allowed
```



### Key handle lifetime

A key handle is zero-initlialized

`key_handle_t key = {0};`

After a successful load it owns a ``EVP_PKEY``

Should be free'd using:

`key_dispose(&key);`

``key_dispose()`  should be idempoitent and the following should be safe:

```
key_dispose(NULL);
key_handle_t key = {0};

key_dispose(&key);
key_dispose(&key);
key_dispose(&key);
```

After dispose:

```
key.pkey == NULL
```

Caller should not call:

```
EVP_PKEY_free(key.pkey);
```

as it's owned by `key_handle_t`





