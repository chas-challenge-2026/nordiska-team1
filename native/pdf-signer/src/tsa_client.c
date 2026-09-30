#include "tsa_client.h"
#include <openssl/evp.h>
#include <openssl/ts.h>
#include <curl/curl.h>
#include <openssl/asn1.h>

/*-------------------INTERNAL----------------------------------*/
typedef struct
{
  unsigned char* data;
  size_t         len;
} http_buffer_t;

static TS_REQ* create_ts_request(const unsigned char* signature, size_t signature_len) {

  unsigned int  digest_len = 0;
  unsigned char digest[EVP_MAX_MD_SIZE];

  if (EVP_Digest(signature, signature_len, digest, &digest_len, EVP_sha256(), NULL) != 1) {
    return NULL;
  }

  if (digest_len != 32) {
    return NULL;
  }

  TS_REQ* req = TS_REQ_new();
  if (!req) {
    return NULL;
  }

  if (TS_REQ_set_version(req, 1) != 1) {
    TS_REQ_free(req);
    return NULL;
  }

  TS_MSG_IMPRINT* imprint = TS_MSG_IMPRINT_new();
  if (!imprint) {
    TS_REQ_free(req);
    return NULL;
  }

  X509_ALGOR* algo = X509_ALGOR_new();
  if (!algo) {
    TS_MSG_IMPRINT_free(imprint);
    TS_REQ_free(req);
    return NULL;
  }

  X509_ALGOR_set_md(algo, EVP_sha256());

  if (TS_MSG_IMPRINT_set_algo(imprint, algo) != 1) {
    X509_ALGOR_free(algo);
    TS_MSG_IMPRINT_free(imprint);
    TS_REQ_free(req);
    return NULL;
  }

  X509_ALGOR_free(algo);

  if (TS_MSG_IMPRINT_set_msg(imprint, digest, digest_len) != 1) {
    TS_MSG_IMPRINT_free(imprint);
    TS_REQ_free(req);
    return NULL;
  }

  if (TS_REQ_set_msg_imprint(req, imprint) != 1) {
    TS_MSG_IMPRINT_free(imprint);
    TS_REQ_free(req);
    return NULL;
  }

  TS_MSG_IMPRINT_free(imprint);

  if (TS_REQ_set_cert_req(req, 1) != 1) {
    TS_REQ_free(req);
    return NULL;
  }

  return req;
}

static unsigned char* ts_req_to_der(TS_REQ* req, size_t* out_len) {
  if (!req || !out_len) {
    return NULL;
  }

  int len = i2d_TS_REQ(req, NULL);
  if (len <= 0) {
    return NULL;
  }

  unsigned char* der = malloc((size_t)len);
  if (!der) {
    return NULL;
  }

  unsigned char* p = der;

  if (i2d_TS_REQ(req, &p) != len) {
    free(der);
    return NULL;
  }

  *out_len = (size_t)len;
  return der;
}

static size_t write_data(char* buffer, size_t size, size_t nmemb, void* userp) {
  http_buffer_t* response = userp;
  if (!response) {
    return 0;
  }

  if (size != 0 && nmemb > SIZE_MAX / size) {
    return 0;
  }

  size_t incomming_len = size * nmemb;
  if (incomming_len > SIZE_MAX - response->len) {
    return 0;
  }

  unsigned char* new_data = realloc(response->data, response->len + incomming_len);
  if (!new_data) {
    return 0;
  }

  response->data = new_data;

  memcpy(response->data + response->len, buffer, incomming_len);

  response->len += incomming_len;

  return incomming_len;
}

static tsa_status_t send_ts_request(const tsa_config_t* config, const unsigned char* request_der,
                                    size_t request_der_len, unsigned char** response,
                                    size_t* response_len) {
  if (!config || !request_der || request_der_len == 0 || !response || !response_len) {
    return TSA_STATUS_INVALID_ARGUMENT;
  }

  *response     = NULL;
  *response_len = 0;

  CURL* handle = curl_easy_init();
  if (!handle) {
    return TSA_STATUS_NETWORK_ERROR;
  }

  http_buffer_t curl_response = {0};

  struct curl_slist* headers = NULL;
  headers = curl_slist_append(headers, "Content-Type: application/timestamp-query");
  headers = curl_slist_append(headers, "Accept: application/timestamp-reply");

  curl_easy_setopt(handle, CURLOPT_URL, config->url);
  curl_easy_setopt(handle, CURLOPT_POST, 1L);
  curl_easy_setopt(handle, CURLOPT_POSTFIELDS, request_der);
  curl_easy_setopt(handle, CURLOPT_POSTFIELDSIZE_LARGE, (curl_off_t)request_der_len);
  curl_easy_setopt(handle, CURLOPT_HTTPHEADER, headers);
  curl_easy_setopt(handle, CURLOPT_WRITEFUNCTION, write_data);
  curl_easy_setopt(handle, CURLOPT_WRITEDATA, &curl_response);
  curl_easy_setopt(handle, CURLOPT_TIMEOUT_MS, config->timeout_ms);

  CURLcode result = curl_easy_perform(handle);

  long http_status = 0;

  if (result == CURLE_OK) {
    curl_easy_getinfo(handle, CURLINFO_RESPONSE_CODE, &http_status);
  }

  curl_slist_free_all(headers);
  curl_easy_cleanup(handle);


  if (result != CURLE_OK) {
    free(curl_response.data);
    return TSA_STATUS_NETWORK_ERROR;
  }

  if (http_status != 200) {
    free(curl_response.data);
    return TSA_STATUS_RESPONSE_ERROR;
  }

  if (curl_response.data && curl_response.len > 0) {
    *response     = curl_response.data;
    *response_len = curl_response.len;
    return TSA_STATUS_OK;
  }

  free(curl_response.data);
  return TSA_STATUS_INTERNAL_ERROR;
}

static unsigned char* ts_token_to_der(PKCS7* token, size_t* out_len) {

  if (!token || !out_len) {
    return NULL;
  }

  int len = i2d_PKCS7(token, NULL);
  if (len <= 0) {
    return NULL;
  }

  unsigned char* der = malloc((size_t)len);
  if (!der) {
    return NULL;
  }

  unsigned char* p = der;

  if (i2d_PKCS7(token, &p) != len) {
    free(der);
    return NULL;
  }

  *out_len = (size_t)len;
  return der;
}

/*------------------------------------------------------------*/


tsa_status_t tsa_request_timestamp(const tsa_config_t* config, const unsigned char* signature,
                                   size_t signature_len, tsa_result_t* res) {
  if (!config || !signature || signature_len == 0 || !res) {
    return TSA_STATUS_INVALID_ARGUMENT;
  }

  res->token_der     = NULL;
  res->token_der_len = 0;

  TS_REQ* req = create_ts_request(signature, signature_len);
  if (!req) {
    return TSA_STATUS_REQUEST_ERROR;
  }

  size_t         req_der_len = 0;
  unsigned char* req_der     = ts_req_to_der(req, &req_der_len);
  if (!req_der) {
    TS_REQ_free(req);
    return TSA_STATUS_REQUEST_ERROR;
  }

  unsigned char* tsa_resp     = NULL;
  size_t         tsa_resp_len = 0;

  tsa_status_t status = send_ts_request(config, req_der, req_der_len, &tsa_resp, &tsa_resp_len);

  free(req_der);

  if (status != TSA_STATUS_OK) {
    free(tsa_resp);
    TS_REQ_free(req);
    return status;
  }

  const unsigned char* p = tsa_resp;

  TS_RESP* ts_response = d2i_TS_RESP(NULL, &p, (long)tsa_resp_len);

  if (!ts_response) {
    free(tsa_resp);
    TS_REQ_free(req);
    return TSA_STATUS_RESPONSE_ERROR;
  }

  TS_STATUS_INFO* status_info = TS_RESP_get_status_info(ts_response);
  if (!status_info) {
    free(tsa_resp);
    TS_RESP_free(ts_response);
    TS_REQ_free(req);
    return TSA_STATUS_RESPONSE_ERROR;
  }

  const ASN1_INTEGER* ans1_status = TS_STATUS_INFO_get0_status(status_info);
  if (!ans1_status) {
    free(tsa_resp);
    TS_RESP_free(ts_response);
    TS_REQ_free(req);
    return TSA_STATUS_RESPONSE_ERROR;
  }

  long status_code = ASN1_INTEGER_get(ans1_status);

  if (status_code != TS_STATUS_GRANTED && status_code != TS_STATUS_GRANTED_WITH_MODS) {
    free(tsa_resp);
    TS_RESP_free(ts_response);
    TS_REQ_free(req);
    return TSA_STATUS_RESPONSE_ERROR;
  }


  X509_STORE* store = X509_STORE_new();
  if (!store) {
    free(tsa_resp);
    TS_RESP_free(ts_response);
    TS_REQ_free(req);
    return TSA_STATUS_VERIFY_ERROR;
  }

  if (X509_STORE_set_default_paths(store) != 1) {
    free(tsa_resp);
    TS_RESP_free(ts_response);
    TS_REQ_free(req);
    X509_STORE_free(store);
    return TSA_STATUS_VERIFY_ERROR;
  }

  TS_VERIFY_CTX* verify_ctx = TS_REQ_to_TS_VERIFY_CTX(req, NULL);

  if (!verify_ctx) {
    free(tsa_resp);
    TS_RESP_free(ts_response);
    TS_REQ_free(req);
    X509_STORE_free(store);
    return TSA_STATUS_VERIFY_ERROR;
  }

  TS_VERIFY_CTX_add_flags(verify_ctx, TS_VFY_SIGNATURE);

  if (TS_VERIFY_CTX_set0_store(verify_ctx, store) != 1) {
    TS_VERIFY_CTX_free(verify_ctx);
    X509_STORE_free(store);
    TS_RESP_free(ts_response);
    TS_REQ_free(req);
    free(tsa_resp);
    return TSA_STATUS_VERIFY_ERROR;
  }

  if (TS_RESP_verify_response(verify_ctx, ts_response) != 1) {
    free(tsa_resp);
    TS_RESP_free(ts_response);
    TS_REQ_free(req);
    TS_VERIFY_CTX_free(verify_ctx);
    return TSA_STATUS_VERIFY_ERROR;
  }

  PKCS7* token = TS_RESP_get_token(ts_response);
  if (!token) {
    TS_VERIFY_CTX_free(verify_ctx);
    TS_RESP_free(ts_response);
    TS_REQ_free(req);
    free(tsa_resp);
    return TSA_STATUS_RESPONSE_ERROR;
  }

  size_t         token_der_len = 0;
  unsigned char* token_der     = ts_token_to_der(token, &token_der_len);

  if (!token_der) {
    TS_VERIFY_CTX_free(verify_ctx);
    TS_RESP_free(ts_response);
    TS_REQ_free(req);
    free(tsa_resp);
    return TSA_STATUS_RESPONSE_ERROR;
  }

  res->token_der     = token_der;
  res->token_der_len = token_der_len;

  TS_VERIFY_CTX_free(verify_ctx);
  TS_RESP_free(ts_response);
  TS_REQ_free(req);
  free(tsa_resp);

  return TSA_STATUS_OK;
}

void tsa_result_dispose(tsa_result_t* res) {
  if (!res) {
    return;
  }

  free(res->token_der);
  res->token_der     = NULL;
  res->token_der_len = 0;
}
