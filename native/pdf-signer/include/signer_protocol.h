#ifndef SIGNER_PROTOCOL_H
#define SIGNER_PROTOCOL_H
#include <stdint.h>

#define SIGNER_PROTOCOL_VERSION 1
#define SIGNER_DIGEST_SHA256 1
#define SIGNER_SHA256_DIGEST_LEN 32
#define SIGNER_REQUEST_HEADER_SIZE 12
#define SIGNER_RESPONSE_HEADER_SIZE 8
#define SIGNER_REQUEST_DIGEST_SIZE 32
#define SIGNER_RESPONSE_MAX_SIZE 65536
#define SIGNER_REQUEST_SIZE (SIGNER_REQUEST_HEADER_SIZE + SIGNER_REQUEST_DIGEST_SIZE)

typedef enum
{
  SIGNER_STATUS_OK                    = 0,
  SIGNER_STATUS_INVALID_REQUEST       = 1,
  SIGNER_STATUS_UNSUPPORTED_VERSION   = 2,
  SIGNER_STATUS_UNSUPPORTED_ALGORITHM = 3,
  SIGNER_STATUS_SIGNING_ERROR         = 4,
  SIGNER_STATUS_INTERNAL_ERROR        = 5
} signer_protocol_status_t;

typedef struct
{
  uint32_t      version;
  uint32_t      digest_algorithm;
  uint32_t      digest_len;
  unsigned char digest[SIGNER_SHA256_DIGEST_LEN];
} signer_request_t;

typedef struct
{
  uint32_t       status;
  uint32_t       contents_hex_len;
  unsigned char* contents_hex;
} signer_response_t;


#endif
