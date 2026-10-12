#include "pdf_sign.h"
#include "key_load.h"
#include "cert_load.h"
#include <stdlib.h>
#include <string.h>
#include <openssl/cms.h>
#include <stdio.h>
#include "tsa_client.h"
#include "runtime_config.h"

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

static CMS_ContentInfo* create_cms(pdf_signer_t* signer, const pdf_sign_request_t* req) {
  if (!signer || !req) {
    return NULL;
  }

  CMS_ContentInfo* cms = CMS_sign(NULL, NULL, NULL, NULL, CMS_PARTIAL | CMS_DETACHED | CMS_BINARY);
  if (!cms) {
    return NULL;
  }

  CMS_SignerInfo* signer_info = CMS_add1_signer(cms, signer->cert.certificate, signer->key.pkey,
                                                EVP_sha256(), CMS_NOSMIMECAP | CMS_CADES);
  if (!signer_info) {
    CMS_ContentInfo_free(cms);
    return NULL;
  }

  if (CMS_final_digest(cms, req->digest, req->digest_len, NULL, CMS_DETACHED | CMS_BINARY) != 1) {
    CMS_ContentInfo_free(cms);
    return NULL;
  }

  return cms;
}

static unsigned char* cms_to_der(CMS_ContentInfo* cms, size_t* out_len) {
  if (!cms || !out_len) {
    return NULL;
  }

  int len = i2d_CMS_ContentInfo(cms, NULL);
  if (len <= 0) {
    return NULL;
  }

  unsigned char* der = malloc((size_t)len);
  if (!der) {
    return NULL;
  }

  unsigned char* p = der;
  if (i2d_CMS_ContentInfo(cms, &p) != len) {
    free(der);
    return NULL;
  }

  *out_len = (size_t)len;
  return der;
}

static char* der_to_hex(const unsigned char* der, size_t der_len, size_t* out_len) {
  if (!der || !out_len) {
    return NULL;
  }

  if (der_len > (SIZE_MAX - 1) / 2) {
    return NULL;
  }

  size_t hex_len = der_len * 2;

  char* hex = malloc(hex_len + 1);
  if (!hex) {
    return NULL;
  }

  static const char digits[] = "0123456789ABCDEF";

  for (size_t i = 0; i < der_len; i++) {
    // Convert the upper 4 bits of the byte to the first hex character.
    hex[i * 2] = digits[(der[i] >> 4) & 0x0F];

    // Convert the lower 4 bits of the byte to the second hex character.
    hex[i * 2 + 1] = digits[der[i] & 0x0F];
  }

  hex[hex_len] = '\0';
  *out_len     = hex_len;
  return hex;
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
      .pkcs11_provider_name = runtime_config_pkcs11_provider(),
      .pkcs11_module_path   = runtime_config_pkcs11_module(),
      .pkcs11_no_deinit     = true,
  };

  signer->key_loader = key_loader_create(&loader_config);
  if (!signer->key_loader) {
    pdf_signer_destroy(signer);
    return PDF_SIGN_CRYPTO_ERROR;
  }

  key_spec_t key_spec = {
      .source       = KEY_SOURCE_PKCS11,
      .u.pkcs11.uri = runtime_config_pkcs11_uri(),
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

  cert_status_t cert_status = cert_load_file(runtime_config_signing_cert(), &signer->cert);
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

pdf_sign_status_t pdf_signer_sign(pdf_signer_t* signer, const pdf_sign_request_t* req,
                                  pdf_sign_result_t* result) {
  if (!signer || !req || !result) {
    return PDF_SIGN_INVALID_ARGUMENT;
  }
  result->contents_hex     = NULL;
  result->contents_hex_len = 0;

  if (req->digest_algorithm != PDF_SIGN_DIGEST_SHA256) {
    return PDF_SIGN_INVALID_ARGUMENT;
  }

  if (!req->digest || req->digest_len != 32) {
    return PDF_SIGN_INVALID_ARGUMENT;
  }

  CMS_ContentInfo* cms = create_cms(signer, req);
  if (!cms) {
    return PDF_SIGN_CMS_ERROR;
  }

  size_t         der_len = 0;
  unsigned char* der     = cms_to_der(cms, &der_len);
  if (!der) {
    CMS_ContentInfo_free(cms);
    return PDF_SIGN_CMS_ERROR;
  }


  size_t hex_len = 0;
  char*  hex     = der_to_hex(der, der_len, &hex_len);

  free(der);
  CMS_ContentInfo_free(cms);

  if (!hex) {
    return PDF_SIGN_OUTPUT_ERROR;
  }

  result->contents_hex     = hex;
  result->contents_hex_len = hex_len;

  return PDF_SIGN_OK;
}

pdf_sign_status_t pdf_signer_sign_with_timestamp(pdf_signer_t*             signer,
                                                 const pdf_sign_request_t* req,
                                                 pdf_sign_result_t*        result) {
  if (!signer || !req || !result) {
    return PDF_SIGN_INVALID_ARGUMENT;
  }

  result->contents_hex     = NULL;
  result->contents_hex_len = 0;

  if (req->digest_algorithm != PDF_SIGN_DIGEST_SHA256) {
    return PDF_SIGN_INVALID_ARGUMENT;
  }

  if (!req->digest || req->digest_len != 32) {
    return PDF_SIGN_INVALID_ARGUMENT;
  }

  CMS_ContentInfo* cms = create_cms(signer, req);
  if (!cms) {
    return PDF_SIGN_CMS_ERROR;
  }

  STACK_OF(CMS_SignerInfo)* signer_infos = CMS_get0_SignerInfos(cms);
  if (!signer_infos || sk_CMS_SignerInfo_num(signer_infos) != 1) {
    CMS_ContentInfo_free(cms);
    return PDF_SIGN_CMS_ERROR;
  }

  CMS_SignerInfo* signer_info = sk_CMS_SignerInfo_value(signer_infos, 0);

  const ASN1_OCTET_STRING* signature = CMS_SignerInfo_get0_signature(signer_info);

  if (!signature || !signature->data || signature->length <= 0) {
    CMS_ContentInfo_free(cms);
    return PDF_SIGN_CMS_ERROR;
  }

  tsa_config_t tsa_config = {
      .url        = runtime_config_tsa_url(),
      .timeout_ms = 5000,
  };
  tsa_result_t tsa_result = {0};

  tsa_status_t tsa_status =
      tsa_request_timestamp(&tsa_config, signature->data, (size_t)signature->length, &tsa_result);

  if (tsa_status != TSA_STATUS_OK) {
    tsa_result_dispose(&tsa_result);
    CMS_ContentInfo_free(cms);
    return PDF_SIGN_CMS_ERROR;
  }

  const unsigned char* p = tsa_result.token_der;

  PKCS7* timestamp_token = d2i_PKCS7(NULL, &p, (long)tsa_result.token_der_len);

  if (!timestamp_token || p != tsa_result.token_der + tsa_result.token_der_len) {
    PKCS7_free(timestamp_token);
    tsa_result_dispose(&tsa_result);
    CMS_ContentInfo_free(cms);
    return PDF_SIGN_CMS_ERROR;
  }

  int token_len = i2d_PKCS7(timestamp_token, NULL);

  if (token_len <= 0) {
    PKCS7_free(timestamp_token);
    tsa_result_dispose(&tsa_result);
    CMS_ContentInfo_free(cms);
    return PDF_SIGN_CMS_ERROR;
  }

  unsigned char* token_der = malloc((size_t)token_len);
  if (!token_der) {
    PKCS7_free(timestamp_token);
    tsa_result_dispose(&tsa_result);
    CMS_ContentInfo_free(cms);
    return PDF_SIGN_INTERNAL_ERROR;
  }

  unsigned char* token_p = token_der;

  if (i2d_PKCS7(timestamp_token, &token_p) != token_len) {
    free(token_der);
    PKCS7_free(timestamp_token);
    tsa_result_dispose(&tsa_result);
    CMS_ContentInfo_free(cms);
    return PDF_SIGN_CMS_ERROR;
  }

  if (CMS_unsigned_add1_attr_by_NID(signer_info, NID_id_smime_aa_timeStampToken, V_ASN1_SEQUENCE,
                                    token_der, token_len) != 1) {
    free(token_der);
    PKCS7_free(timestamp_token);
    tsa_result_dispose(&tsa_result);
    CMS_ContentInfo_free(cms);
    return PDF_SIGN_CMS_ERROR;
  }

  free(token_der);
  PKCS7_free(timestamp_token);
  tsa_result_dispose(&tsa_result);

  size_t         der_len = 0;
  unsigned char* der     = cms_to_der(cms, &der_len);

  if (!der) {
    CMS_ContentInfo_free(cms);
    return PDF_SIGN_CMS_ERROR;
  }

  size_t hex_len = 0;
  char*  hex     = der_to_hex(der, der_len, &hex_len);

  free(der);
  CMS_ContentInfo_free(cms);

  if (!hex) {
    return PDF_SIGN_OUTPUT_ERROR;
  }

  result->contents_hex     = hex;
  result->contents_hex_len = hex_len;

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

void pdf_sign_result_dispose(pdf_sign_result_t* result) {
  if (!result)
    return;

  free(result->contents_hex);
  result->contents_hex     = NULL;
  result->contents_hex_len = 0;
}
