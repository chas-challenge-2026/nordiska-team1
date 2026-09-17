#include "pdf_sign.h"
#include "key_load.h"
#include "cert_load.h"
#include <stdlib.h>
#include <string.h>

/*---------------------------INTERNAL-----------------------------*/
struct pdf_signer
{
  key_loader_t* key_loader;
  key_handle_t  key;
  cert_handle_t cert;
};

static key_secret_result_t signer_secret_callback(key_secret_kind_t kind, const key_spec_t* spec,
                                                  unsigned char* buffer, size_t buffer_len,
                                                  size_t* secret_len, void* userdata) {

  (void)spec;
  (void)userdata;

  if (kind != KEY_SECRET_PKCS11_PIN || !buffer || !secret_len) {
    return KEY_SECRET_ERROR;
  }

  const char* pin = getenv("PDF_SIGNER_PKCS11_PIN");
  if (!pin) {
    return KEY_SECRET_ERROR;
  }

  size_t len = strlen(pin);

  if (len == 0 || len > buffer_len) {
    return KEY_SECRET_ERROR;
  }

  memcpy(buffer, pin, len);
  *secret_len = len;

  return KEY_SECRET_OK;
}

/*****************************************************************/


pdf_sign_status_t pdf_signer_create(pdf_signer_t** out) {
  if (!out) {
    return PDF_SIGN_INVALID_ARGUMENT;
  }

  *out = NULL;

  pdf_signer_t* signer = calloc(1, sizeof(*signer));
  if (!signer) {
    return PDF_SIGN_INTERNAL_ERROR;
  }

  key_loader_config_t loader_config = {
      .pkcs11_enabled       = true,
      .pkcs11_provider_name = "pkcs11",
      .pkcs11_module_path   = "/usr/lib/libsofthsm2.so",
      .pkcs11_no_deinit     = true,
  };

  signer->key_loader = key_loader_create(&loader_config);
  if (!signer->key_loader) {
    pdf_signer_destroy(signer);
    return PDF_SIGN_CRYPTO_ERROR;
  }

  key_spec_t key_spec = {
      .source       = KEY_SOURCE_PKCS11,
      .u.pkcs11.uri = "pkcs11:token=key-load-dev;"
                      "object=pdf-signer-test;"
                      "type=private",
  };

  key_credentials_t credentials = {
      .callback = signer_secret_callback,
      .userdata = NULL,
  };

  key_status_t key_status = key_load(signer->key_loader, &key_spec, &credentials, &signer->key);

  if (key_status != KEY_STATUS_OK) {
    pdf_signer_destroy(signer);
    return PDF_SIGN_CRYPTO_ERROR;
  }

  cert_status_t cert_status = cert_load_file("tests/data/signing_cert.pem", &signer->cert);

  if (cert_status != CERT_STATUS_OK) {
    pdf_signer_destroy(signer);
    return PDF_SIGN_CERTIFICATE_ERROR;
  }

  if (X509_check_private_key(signer->cert.certificate, signer->key.pkey) != 1) {
    pdf_signer_destroy(signer);
    return PDF_SIGN_CERTIFICATE_ERROR;
  }

  *out = signer;

  return PDF_SIGN_OK;
}

pdf_sign_status_t pdf_signer_sign(pdf_signer_t* signer, const pdf_sign_request_t* req) {
  if (!signer || !req || !req->pdf) {
    return PDF_SIGN_INVALID_ARGUMENT;
  }

  if (req->pdf_len == 0) {
    return PDF_SIGN_INVALID_PDF;
  }

  if (req->contents_offset > req->pdf_len) {
    return PDF_SIGN_INVALID_PDF;
  }

  if (req->contents_hex_len > req->pdf_len - req->contents_offset) {
    return PDF_SIGN_INVALID_PDF;
  }

  return PDF_SIGN_OK;
}


void pdf_signer_destroy(pdf_signer_t* signer) {
  if (!signer) {
    return;
  }

  cert_dispose(&signer->cert);
  key_dispose(&signer->key);
  key_loader_destroy(signer->key_loader);
  signer->key_loader = NULL;

  free(signer);
}
