#include "tsa_server.h"

#include <openssl/ts.h>
#include <openssl/evp.h>
#include <openssl/asn1.h>

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <openssl/err.h>

#define CHECK(condition)                                                                           \
  do {                                                                                             \
    if (!(condition)) {                                                                            \
      fprintf(stderr, "CHECK failed: %s (%s:%d)\n", #condition, __FILE__, __LINE__);               \
      return 1;                                                                                    \
    }                                                                                              \
  } while (0)

static TS_REQ* create_test_request(void) {
  unsigned char digest[32];

  memset(digest, 0x42, sizeof(digest));

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

  X509_ALGOR* algorithm = X509_ALGOR_new();
  if (!algorithm) {
    TS_MSG_IMPRINT_free(imprint);
    TS_REQ_free(req);
    return NULL;
  }

  X509_ALGOR_set_md(algorithm, EVP_sha256());

  if (TS_MSG_IMPRINT_set_algo(imprint, algorithm) != 1) {
    X509_ALGOR_free(algorithm);
    TS_MSG_IMPRINT_free(imprint);
    TS_REQ_free(req);
    return NULL;
  }

  X509_ALGOR_free(algorithm);

  if (TS_MSG_IMPRINT_set_msg(imprint, digest, sizeof(digest)) != 1) {
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

static unsigned char* request_to_der(TS_REQ* req, size_t* out_len) {
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

static int test_create_response(void) {
  tsa_server_t* server = NULL;

  tsa_server_status_t status = tsa_server_create(&server);
  if (status != TSA_SERVER_OK) {
    fprintf(stderr, "tsa_server_create failed with status %d\n", status);
    ERR_print_errors_fp(stderr);
  }

  CHECK(status == TSA_SERVER_OK);
  CHECK(server != NULL);

  TS_REQ* req = create_test_request();
  CHECK(req != NULL);

  size_t request_der_len = 0;

  unsigned char* request_der = request_to_der(req, &request_der_len);

  CHECK(request_der != NULL);
  CHECK(request_der_len > 0);

  tsa_server_result_t result = {0};

  status = tsa_server_create_response(server, request_der, request_der_len, &result);

  CHECK(status == TSA_SERVER_OK);
  CHECK(result.response_der != NULL);
  CHECK(result.response_der_len > 0);

  const unsigned char* p = result.response_der;

  TS_RESP* response = d2i_TS_RESP(NULL, &p, (long)result.response_der_len);

  CHECK(response != NULL);
  CHECK((size_t)(p - result.response_der) == result.response_der_len);

  TS_STATUS_INFO* status_info = TS_RESP_get_status_info(response);

  CHECK(status_info != NULL);

  const ASN1_INTEGER* response_status = TS_STATUS_INFO_get0_status(status_info);

  CHECK(response_status != NULL);

  long status_code = ASN1_INTEGER_get(response_status);

  CHECK(status_code == TS_STATUS_GRANTED || status_code == TS_STATUS_GRANTED_WITH_MODS);

  PKCS7* token = TS_RESP_get_token(response);

  CHECK(token != NULL);

  TS_RESP_free(response);
  tsa_server_result_dispose(&result);
  free(request_der);
  TS_REQ_free(req);
  tsa_server_destroy(server);

  return 0;
}

static int test_invalid_arguments(void) {
  tsa_server_result_t result = {0};

  CHECK(tsa_server_create_response(NULL, (const unsigned char*)"x", 1, &result) ==
        TSA_SERVER_INVALID_ARGUMENT);

  CHECK(tsa_server_create_response((tsa_server_t*)1, NULL, 1, &result) ==
        TSA_SERVER_INVALID_ARGUMENT);

  CHECK(tsa_server_create_response((tsa_server_t*)1, (const unsigned char*)"x", 0, &result) ==
        TSA_SERVER_INVALID_ARGUMENT);

  CHECK(tsa_server_create_response((tsa_server_t*)1, (const unsigned char*)"x", 1, NULL) ==
        TSA_SERVER_INVALID_ARGUMENT);

  return 0;
}

static int test_result_dispose(void) {
  tsa_server_result_t result = {0};

  result.response_der = malloc(16);
  CHECK(result.response_der != NULL);

  result.response_der_len = 16;

  tsa_server_result_dispose(&result);

  CHECK(result.response_der == NULL);
  CHECK(result.response_der_len == 0);

  tsa_server_result_dispose(&result);
  tsa_server_result_dispose(NULL);

  return 0;
}

int main(void) {
  int failed = 0;

  failed += test_create_response();
  failed += test_invalid_arguments();
  failed += test_result_dispose();

  if (failed == 0) {
    printf("All tsa_server tests passed\n");
    return 0;
  }

  fprintf(stderr, "%d tsa_server test(s) failed\n", failed);

  return 1;
}
