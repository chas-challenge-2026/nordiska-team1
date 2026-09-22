#include "cert_load.h"
#include <openssl/bio.h>
#include <openssl/pem.h>
#include <openssl/x509.h>
#include <openssl/err.h>

cert_status_t cert_load_file(const char* path, cert_handle_t* out) {

  if (!path || !out || out->certificate) {
    return CERT_STATUS_INVALID_ARGUMENT;
  }

  BIO* bio_in = BIO_new_file(path, "rb");
  if (!bio_in) {
    return CERT_STATUS_LOAD_FAILED;
  }

  X509* cert = PEM_read_bio_X509(bio_in, NULL, NULL, NULL);

  if (!cert) {
    // Rewind and try DER
    ERR_clear_error();

    if (BIO_reset(bio_in) < 0) {
      BIO_free(bio_in);
      return CERT_STATUS_LOAD_FAILED;
    }
    cert = d2i_X509_bio(bio_in, NULL);
    if (!cert) {
      BIO_free(bio_in);
      return CERT_STATUS_LOAD_FAILED;
    }
  }
  BIO_free(bio_in);
  out->certificate = cert;

  return CERT_STATUS_OK;
}


void cert_dispose(cert_handle_t* handle) {
  if (!handle || !handle->certificate) {
    return;
  }

  X509_free(handle->certificate);
  handle->certificate = NULL;
}
