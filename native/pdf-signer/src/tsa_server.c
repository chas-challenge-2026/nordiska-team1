#include "tsa_server.h"
#include "key_load.h"
#include "cert_load.h"

#include <openssl/ts.h>
#include <openssl/x509.h>

#include <stdlib.h>
#include <string.h>
#include <openssl/evp.h>
#include <openssl/objects.h>
#include <stdint.h>
#include <limits.h>


/*-------------------INTERNAL-------------------------*/


struct tsa_server
{
  key_loader_t* key_loader;
  key_handle_t  key;
  cert_handle_t cert;
  TS_RESP_CTX*  response_ctx;
  uint64_t      next_serial;
};

static key_secret_result_t tsa_secret_callback(key_secret_kind_t kind, const key_spec_t* spec,
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

static ASN1_INTEGER* tsa_serial_callback(TS_RESP_CTX* ctx, void* data) {
  (void)ctx;

  tsa_server_t* server = data;
  if (!server) {
    return NULL;
  }

  ASN1_INTEGER* serial = ASN1_INTEGER_new();
  if (!serial) {
    return NULL;
  }

  if (ASN1_INTEGER_set_uint64(serial, server->next_serial) != 1) {
    ASN1_INTEGER_free(serial);
    return NULL;
  }

  server->next_serial++;

  return serial;
}
/*--------------------------------------------------*/

tsa_server_status_t tsa_server_create(tsa_server_t** out) {
  if (!out) {
    return TSA_SERVER_INVALID_ARGUMENT;
  }

  *out = NULL;

  tsa_server_t* server = calloc(1, sizeof(*server));
  if (!server) {
    return TSA_SERVER_INTERNAL_ERROR;
  }

  key_loader_config_t loader_config = {
      .pkcs11_enabled       = true,
      .pkcs11_provider_name = "pkcs11",
      .pkcs11_module_path   = "/usr/lib/libsofthsm2.so",
      .pkcs11_no_deinit     = true,
  };

  server->key_loader = key_loader_create(&loader_config);

  if (!server->key_loader) {
    tsa_server_destroy(server);
    return TSA_SERVER_CRYPTO_ERROR;
  }

  key_spec_t key_spec = {
      .source       = KEY_SOURCE_PKCS11,
      .u.pkcs11.uri = "pkcs11:token=key-load-dev;"
                      "object=pdf-signer-test;"
                      "type=private",
  };

  key_credentials_t credentials = {
      .callback = tsa_secret_callback,
      .userdata = NULL,
  };

  key_status_t key_status = key_load(server->key_loader, &key_spec, &credentials, &server->key);

  if (key_status != KEY_STATUS_OK) {
    tsa_server_destroy(server);
    return TSA_SERVER_CRYPTO_ERROR;
  }

  cert_status_t cert_status = cert_load_file("tests/data/tsa_cert.pem", &server->cert);

  if (cert_status != CERT_STATUS_OK) {
    tsa_server_destroy(server);
    return TSA_SERVER_CERTIFICATE_ERROR;
  }

  if (X509_check_private_key(server->cert.certificate, server->key.pkey) != 1) {
    tsa_server_destroy(server);
    return TSA_SERVER_CERTIFICATE_ERROR;
  }

  server->response_ctx = TS_RESP_CTX_new();

  if (!server->response_ctx) {
    tsa_server_destroy(server);
    return TSA_SERVER_CONTEXT_ERROR;
  }

  server->next_serial = 1;

  TS_RESP_CTX_set_serial_cb(server->response_ctx, tsa_serial_callback, server);

  if (TS_RESP_CTX_set_signer_cert(server->response_ctx, server->cert.certificate) != 1) {
    tsa_server_destroy(server);
    return TSA_SERVER_CONTEXT_ERROR;
  }

  if (TS_RESP_CTX_set_signer_key(server->response_ctx, server->key.pkey) != 1) {
    tsa_server_destroy(server);
    return TSA_SERVER_CONTEXT_ERROR;
  }

  if (TS_RESP_CTX_set_signer_digest(server->response_ctx, EVP_sha256()) != 1) {
    tsa_server_destroy(server);
    return TSA_SERVER_CONTEXT_ERROR;
  }

  ASN1_OBJECT* policy = OBJ_txt2obj("1.3.6.1.4.1.55555.1.1", 1);

  if (!policy) {
    tsa_server_destroy(server);
    return TSA_SERVER_CONTEXT_ERROR;
  }

  if (TS_RESP_CTX_set_def_policy(server->response_ctx, policy) != 1) {
    ASN1_OBJECT_free(policy);
    tsa_server_destroy(server);
    return TSA_SERVER_CONTEXT_ERROR;
  }

  ASN1_OBJECT_free(policy);

  if (TS_RESP_CTX_add_md(server->response_ctx, EVP_sha256()) != 1) {
    tsa_server_destroy(server);
    return TSA_SERVER_CONTEXT_ERROR;
  }

  *out = server;

  return TSA_SERVER_OK;
}

tsa_server_status_t tsa_server_create_response(tsa_server_t*        server,
                                               const unsigned char* request_der,
                                               size_t               request_der_len,
                                               tsa_server_result_t* result) {
  if (!server || !request_der || request_der_len == 0 || !result) {
    return TSA_SERVER_INVALID_ARGUMENT;
  }

  result->response_der     = NULL;
  result->response_der_len = 0;

  if (request_der_len > INT_MAX) {
    return TSA_SERVER_INVALID_ARGUMENT;
  }

  BIO* request_bio = BIO_new_mem_buf(request_der, (int)request_der_len);

  if (!request_bio) {
    return TSA_SERVER_CONTEXT_ERROR;
  }

  TS_RESP* response = TS_RESP_create_response(server->response_ctx, request_bio);

  BIO_free(request_bio);

  if (!response) {
    return TSA_SERVER_CONTEXT_ERROR;
  }

  int der_len = i2d_TS_RESP(response, NULL);

  if (der_len <= 0) {
    TS_RESP_free(response);
    return TSA_SERVER_CONTEXT_ERROR;
  }

  unsigned char* der = malloc((size_t)der_len);

  if (!der) {
    TS_RESP_free(response);
    return TSA_SERVER_INTERNAL_ERROR;
  }

  unsigned char* p = der;

  if (i2d_TS_RESP(response, &p) != der_len) {
    free(der);
    TS_RESP_free(response);
    return TSA_SERVER_CONTEXT_ERROR;
  }

  TS_RESP_free(response);

  result->response_der     = der;
  result->response_der_len = (size_t)der_len;

  return TSA_SERVER_OK;
}

void tsa_server_result_dispose(tsa_server_result_t* result) {
  if (!result) {
    return;
  }

  free(result->response_der);

  result->response_der     = NULL;
  result->response_der_len = 0;
}

void tsa_server_destroy(tsa_server_t* server) {
  if (!server) {
    return;
  }

  TS_RESP_CTX_free(server->response_ctx);
  server->response_ctx = NULL;

  cert_dispose(&server->cert);
  key_dispose(&server->key);

  key_loader_destroy(server->key_loader);
  server->key_loader = NULL;

  free(server);
}
