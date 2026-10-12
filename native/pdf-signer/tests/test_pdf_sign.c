#include "pdf_sign.h"
#include "tsa_client.h"
#include <ctype.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <openssl/cms.h>
#include <openssl/ts.h>
#include <openssl/evp.h>

#define CHECK(condition)                                                                           \
  do {                                                                                             \
    if (!(condition)) {                                                                            \
      fprintf(stderr, "CHECK failed: %s:%d: %s\n", __FILE__, __LINE__, #condition);                \
      return 1;                                                                                    \
    }                                                                                              \
  } while (0)


static int is_hex_string(const char* data, size_t len) {
  for (size_t i = 0; i < len; i++) {
    if (!isxdigit((unsigned char)data[i])) {
      return 0;
    }
  }

  return 1;
}

static unsigned char* hex_to_bytes(const char* hex, size_t hex_len, size_t* out_len) {

  if (!hex || !out_len || hex_len % 2 != 0) {
    return NULL;
  }

  size_t byte_len = hex_len / 2;

  unsigned char* bytes = malloc(byte_len);
  if (!bytes) {
    return NULL;
  }

  for (size_t i = 0; i < byte_len; i++) {
    unsigned int value;

    if (sscanf(&hex[i * 2], "%2x", &value) != 1) {
      free(bytes);
      return NULL;
    }

    bytes[i] = (unsigned char)value;
  }

  *out_len = byte_len;
  return bytes;
}


static int test_pdf_signer_create_null_out(void) {
  pdf_sign_status_t status = pdf_signer_create(NULL);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_signer_destroy_null(void) {
  pdf_signer_destroy(NULL);

  return 0;
}


static int test_pdf_sign_null_signer(void) {
  unsigned char digest[32] = {0};

  pdf_sign_request_t req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = digest,
      .digest_len       = sizeof(digest),
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign(NULL, &req, &result);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_null_request(pdf_signer_t* signer) {
  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign(signer, NULL, &result);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_null_result(pdf_signer_t* signer) {
  unsigned char digest[32] = {0};

  pdf_sign_request_t req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = digest,
      .digest_len       = sizeof(digest),
  };

  pdf_sign_status_t status = pdf_signer_sign(signer, &req, NULL);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_null_digest(pdf_signer_t* signer) {
  pdf_sign_request_t req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = NULL,
      .digest_len       = 32,
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign(signer, &req, &result);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_invalid_digest_length(pdf_signer_t* signer) {
  unsigned char digest[31] = {0};

  pdf_sign_request_t req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = digest,
      .digest_len       = sizeof(digest),
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign(signer, &req, &result);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_invalid_algorithm(pdf_signer_t* signer) {
  unsigned char digest[32] = {0};

  pdf_sign_request_t req = {
      .digest_algorithm = (pdf_sign_digest_algorithm_t)999,
      .digest           = digest,
      .digest_len       = sizeof(digest),
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign(signer, &req, &result);

  CHECK(status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_returns_cms_hex(pdf_signer_t* signer) {
  unsigned char digest[32] = {0};

  pdf_sign_request_t req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = digest,
      .digest_len       = sizeof(digest),
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign(signer, &req, &result);

  CHECK(status == PDF_SIGN_OK);
  CHECK(result.contents_hex != NULL);
  CHECK(result.contents_hex_len > 0);
  CHECK(result.contents_hex_len % 2 == 0);
  CHECK(strlen(result.contents_hex) == result.contents_hex_len);
  CHECK(is_hex_string(result.contents_hex, result.contents_hex_len));

  pdf_sign_result_dispose(&result);

  CHECK(result.contents_hex == NULL);
  CHECK(result.contents_hex_len == 0);

  return 0;
}


static int test_pdf_sign_result_dispose_null(void) {
  pdf_sign_result_dispose(NULL);

  return 0;
}


static int test_pdf_sign_result_dispose_twice(void) {
  pdf_sign_result_t result = {0};

  result.contents_hex = malloc(16);
  CHECK(result.contents_hex != NULL);

  result.contents_hex_len = 15;

  pdf_sign_result_dispose(&result);
  pdf_sign_result_dispose(&result);

  CHECK(result.contents_hex == NULL);
  CHECK(result.contents_hex_len == 0);

  return 0;
}

static int test_pdf_sign_returns_valid_cms(pdf_signer_t* signer) {
  unsigned char digest[32] = {0};

  pdf_sign_request_t req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = digest,
      .digest_len       = sizeof(digest),
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign(signer, &req, &result);

  CHECK(status == PDF_SIGN_OK);
  CHECK(result.contents_hex != NULL);
  CHECK(result.contents_hex_len > 0);

  size_t der_len = 0;

  unsigned char* der = hex_to_bytes(result.contents_hex, result.contents_hex_len, &der_len);

  CHECK(der != NULL);
  CHECK(der_len > 0);

  const unsigned char* p = der;

  CMS_ContentInfo* cms = d2i_CMS_ContentInfo(NULL, &p, (long)der_len);

  CHECK(cms != NULL);
  FILE* f = fopen("tests/data/signature.der", "wb");
  CHECK(f != NULL);

  CHECK(fwrite(der, 1, der_len, f) == der_len);

  fclose(f);

  CMS_ContentInfo_free(cms);
  free(der);
  pdf_sign_result_dispose(&result);

  return 0;
}

static int test_pdf_sign_with_timestamp_extracts_signature(pdf_signer_t* signer) {
  unsigned char digest[32] = {0};

  pdf_sign_request_t req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = digest,
      .digest_len       = sizeof(digest),
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign_with_timestamp(signer, &req, &result);

  CHECK(status == PDF_SIGN_OK);

  pdf_sign_result_dispose(&result);

  return 0;
}

static int test_pdf_sign_with_timestamp_contains_timestamp_token(pdf_signer_t* signer) {
  unsigned char digest[32] = {0};

  pdf_sign_request_t req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = digest,
      .digest_len       = sizeof(digest),
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign_with_timestamp(signer, &req, &result);

  CHECK(status == PDF_SIGN_OK);
  CHECK(result.contents_hex != NULL);
  CHECK(result.contents_hex_len > 0);

  size_t der_len = 0;

  unsigned char* der = hex_to_bytes(result.contents_hex, result.contents_hex_len, &der_len);

  CHECK(der != NULL);

  const unsigned char* p = der;

  CMS_ContentInfo* cms = d2i_CMS_ContentInfo(NULL, &p, (long)der_len);

  CHECK(cms != NULL);

  STACK_OF(CMS_SignerInfo)* signer_infos = CMS_get0_SignerInfos(cms);

  CHECK(signer_infos != NULL);
  CHECK(sk_CMS_SignerInfo_num(signer_infos) == 1);

  CMS_SignerInfo* signer_info = sk_CMS_SignerInfo_value(signer_infos, 0);

  CHECK(signer_info != NULL);

  int attr_index = CMS_unsigned_get_attr_by_NID(signer_info, NID_id_smime_aa_timeStampToken, -1);

  CHECK(attr_index >= 0);

  X509_ATTRIBUTE* attr = CMS_unsigned_get_attr(signer_info, attr_index);

  CHECK(attr != NULL);

  CMS_ContentInfo_free(cms);
  free(der);
  pdf_sign_result_dispose(&result);

  return 0;
}

static int test_pdf_sign_timestamp_matches_signature(pdf_signer_t* signer) {
  unsigned char digest[32] = {0};

  pdf_sign_request_t req = {
      .digest_algorithm = PDF_SIGN_DIGEST_SHA256,
      .digest           = digest,
      .digest_len       = sizeof(digest),
  };

  pdf_sign_result_t result = {0};

  pdf_sign_status_t status = pdf_signer_sign_with_timestamp(signer, &req, &result);

  CHECK(status == PDF_SIGN_OK);
  CHECK(result.contents_hex != NULL);
  CHECK(result.contents_hex_len > 0);

  size_t der_len = 0;

  unsigned char* der = hex_to_bytes(result.contents_hex, result.contents_hex_len, &der_len);

  CHECK(der != NULL);

  const unsigned char* p = der;

  CMS_ContentInfo* cms = d2i_CMS_ContentInfo(NULL, &p, (long)der_len);

  CHECK(cms != NULL);
  CHECK(p == der + der_len);

  STACK_OF(CMS_SignerInfo)* signer_infos = CMS_get0_SignerInfos(cms);

  CHECK(signer_infos != NULL);
  CHECK(sk_CMS_SignerInfo_num(signer_infos) == 1);

  CMS_SignerInfo* signer_info = sk_CMS_SignerInfo_value(signer_infos, 0);

  CHECK(signer_info != NULL);

  /*
   * Get the actual CMS signature value.
   * RFC3161 signatureTimeStampToken must timestamp this value.
   */
  const ASN1_OCTET_STRING* signature = CMS_SignerInfo_get0_signature(signer_info);

  CHECK(signature != NULL);
  CHECK(signature->data != NULL);
  CHECK(signature->length > 0);

  unsigned char expected_imprint[EVP_MAX_MD_SIZE];
  unsigned int  expected_imprint_len = 0;

  CHECK(EVP_Digest(signature->data, (size_t)signature->length, expected_imprint,
                   &expected_imprint_len, EVP_sha256(), NULL) == 1);

  CHECK(expected_imprint_len == 32);

  /*
   * Find id-aa-signatureTimeStampToken in unsigned attributes.
   */
  int attr_index = CMS_unsigned_get_attr_by_NID(signer_info, NID_id_smime_aa_timeStampToken, -1);

  CHECK(attr_index >= 0);

  X509_ATTRIBUTE* attr = CMS_unsigned_get_attr(signer_info, attr_index);

  CHECK(attr != NULL);
  CHECK(X509_ATTRIBUTE_count(attr) == 1);

  ASN1_STRING* token_sequence = X509_ATTRIBUTE_get0_data(attr, 0, V_ASN1_SEQUENCE, NULL);

  CHECK(token_sequence != NULL);

  const unsigned char* token_der = ASN1_STRING_get0_data(token_sequence);

  int token_der_len = ASN1_STRING_length(token_sequence);

  CHECK(token_der != NULL);
  CHECK(token_der_len > 0);

  /*
   * Parse RFC3161 timestamp token.
   */
  const unsigned char* token_p = token_der;

  PKCS7* token = d2i_PKCS7(NULL, &token_p, token_der_len);

  CHECK(token != NULL);
  CHECK(token_p == token_der + token_der_len);

  TS_TST_INFO* tst_info = PKCS7_to_TS_TST_INFO(token);

  CHECK(tst_info != NULL);

  TS_MSG_IMPRINT* msg_imprint = TS_TST_INFO_get_msg_imprint(tst_info);

  CHECK(msg_imprint != NULL);

  /*
   * Verify that the TSA used SHA-256.
   */
  X509_ALGOR* imprint_algorithm = TS_MSG_IMPRINT_get_algo(msg_imprint);

  CHECK(imprint_algorithm != NULL);

  const ASN1_OBJECT* algorithm_object = NULL;

  X509_ALGOR_get0(&algorithm_object, NULL, NULL, imprint_algorithm);

  CHECK(algorithm_object != NULL);
  CHECK(OBJ_obj2nid(algorithm_object) == NID_sha256);

  /*
   * Compare token messageImprint with SHA256(CMS signature value).
   */
  ASN1_OCTET_STRING* imprint = TS_MSG_IMPRINT_get_msg(msg_imprint);

  CHECK(imprint != NULL);
  CHECK(imprint->length == (int)expected_imprint_len);

  CHECK(memcmp(imprint->data, expected_imprint, expected_imprint_len) == 0);

  TS_TST_INFO_free(tst_info);
  PKCS7_free(token);
  CMS_ContentInfo_free(cms);
  free(der);
  pdf_sign_result_dispose(&result);

  return 0;
}

int main(void) {
  int failed = 0;

  // Tests that do not require a valid signer.
  failed += test_pdf_signer_create_null_out();
  failed += test_pdf_signer_destroy_null();
  failed += test_pdf_sign_null_signer();
  failed += test_pdf_sign_result_dispose_null();
  failed += test_pdf_sign_result_dispose_twice();

  // Create one signer and reuse it for all remaining tests.
  pdf_signer_t* signer = NULL;

  pdf_sign_status_t create_status = pdf_signer_create(&signer);

  if (create_status != PDF_SIGN_OK || !signer) {
    fprintf(stderr, "Failed to create PDF signer: %d\n", create_status);
    return EXIT_FAILURE;
  }

  failed += test_pdf_sign_null_request(signer);
  failed += test_pdf_sign_null_result(signer);
  failed += test_pdf_sign_null_digest(signer);
  failed += test_pdf_sign_invalid_digest_length(signer);
  failed += test_pdf_sign_invalid_algorithm(signer);
  failed += test_pdf_sign_returns_cms_hex(signer);
  failed += test_pdf_sign_returns_valid_cms(signer);
  failed += test_pdf_sign_with_timestamp_extracts_signature(signer);
  failed += test_pdf_sign_with_timestamp_contains_timestamp_token(signer);
  failed += test_pdf_sign_timestamp_matches_signature(signer);

  pdf_signer_destroy(signer);

  if (failed != 0) {
    fprintf(stderr, "%d tests failed\n", failed);
    return EXIT_FAILURE;
  }

  printf("All pdf_sign tests passed\n");

  return EXIT_SUCCESS;
}
