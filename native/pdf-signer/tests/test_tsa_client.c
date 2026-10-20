#include "tsa_client.h"

#include <openssl/pkcs7.h>

#include <stdio.h>

#define CHECK(cond)                                                                                \
  do {                                                                                             \
    if (!(cond)) {                                                                                 \
      fprintf(stderr, "CHECK failed: %s (%s:%d)\n", #cond, __FILE__, __LINE__);                    \
      return 1;                                                                                    \
    }                                                                                              \
  } while (0)

static const tsa_config_t test_config = {
    .url        = "http://127.0.0.1:8081/",
    .timeout_ms = 5000,
};

static int test_tsa_invalid_arguments(void) {
  unsigned char signature[256] = {0};
  tsa_result_t  result         = {0};

  CHECK(tsa_request_timestamp(NULL, signature, sizeof(signature), &result) ==
        TSA_STATUS_INVALID_ARGUMENT);

  CHECK(tsa_request_timestamp(&test_config, NULL, sizeof(signature), &result) ==
        TSA_STATUS_INVALID_ARGUMENT);

  CHECK(tsa_request_timestamp(&test_config, signature, 0, &result) == TSA_STATUS_INVALID_ARGUMENT);

  CHECK(tsa_request_timestamp(&test_config, signature, sizeof(signature), NULL) ==
        TSA_STATUS_INVALID_ARGUMENT);

  return 0;
}

static int test_tsa_returns_valid_timestamp_token(void) {
  unsigned char signature[256] = {0};

  tsa_result_t result = {0};

  tsa_status_t status = tsa_request_timestamp(&test_config, signature, sizeof(signature), &result);

  CHECK(status == TSA_STATUS_OK);

  CHECK(result.token_der != NULL);
  CHECK(result.token_der_len > 0);

  const unsigned char* p = result.token_der;

  PKCS7* token = d2i_PKCS7(NULL, &p, (long)result.token_der_len);

  CHECK(token != NULL);

  /*
   * Ensure the entire DER buffer was consumed.
   */
  CHECK((size_t)(p - result.token_der) == result.token_der_len);

  PKCS7_free(token);
  tsa_result_dispose(&result);

  return 0;
}

static int test_tsa_result_dispose(void) {
  tsa_result_t result = {0};

  tsa_result_dispose(&result);

  CHECK(result.token_der == NULL);
  CHECK(result.token_der_len == 0);

  /*
   * Must also be safe to call twice.
   */
  tsa_result_dispose(&result);

  CHECK(result.token_der == NULL);
  CHECK(result.token_der_len == 0);

  /*
   * NULL must be accepted.
   */
  tsa_result_dispose(NULL);

  return 0;
}

int main(void) {
  int failed = 0;

  failed += test_tsa_invalid_arguments();
  failed += test_tsa_returns_valid_timestamp_token();
  failed += test_tsa_result_dispose();

  if (failed == 0) {
    printf("All tsa_client tests passed\n");
    return 0;
  }

  fprintf(stderr, "%d tsa_client test(s) failed\n", failed);
  return 1;
}
