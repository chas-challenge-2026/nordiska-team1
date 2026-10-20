#ifndef SIGNER_PROTOCOL_H
#define SIGNER_PROTOCOL_H
#include <stdint.h>

#define SIGNER_PROTOCOL_VERSION 2
#define SIGNER_DIGEST_SHA256 1
#define SIGNER_SHA256_DIGEST_LEN 32
#define SIGNER_REQUEST_HEADER_SIZE 16
#define SIGNER_RESPONSE_HEADER_SIZE 8
#define SIGNER_REQUEST_DIGEST_SIZE 32
#define SIGNER_RESPONSE_MAX_SIZE 65536
#define SIGNER_REQUEST_SIZE (SIGNER_REQUEST_HEADER_SIZE + SIGNER_REQUEST_DIGEST_SIZE)

/* Status values returned in the response header. */
typedef enum
{
  SIGNER_STATUS_OK                    = 0,
  SIGNER_STATUS_INVALID_REQUEST       = 1,
  SIGNER_STATUS_UNSUPPORTED_VERSION   = 2,
  SIGNER_STATUS_UNSUPPORTED_ALGORITHM = 3,
  SIGNER_STATUS_SIGNING_ERROR         = 4,
  SIGNER_STATUS_INTERNAL_ERROR        = 5
} signer_protocol_status_t;


/* Requested CMS signing mode. */
typedef enum
{
  SIGNER_MODE_PLAIN     = 0,
  SIGNER_MODE_TIMESTAMP = 1
} signer_mode_t;


/*
 * Protocol v2 request.
 *
 * Wire size: 48 bytes.
 */
typedef struct
{
  uint32_t      version;
  uint32_t      digest_algorithm;
  uint32_t      digest_len;
  uint32_t      sign_mode;
  unsigned char digest[SIGNER_SHA256_DIGEST_LEN];
} signer_request_t;


/*
 * Decoded response.
 *
 * contents_hex is present only when status is SIGNER_STATUS_OK.
 */
typedef struct
{
  uint32_t       status;
  uint32_t       contents_hex_len;
  unsigned char* contents_hex;
} signer_response_t;


#endif
