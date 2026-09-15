#include "pdf_sign.h"
#include <stdio.h>

pdf_sign_status_t pdf_sign(const pdf_sign_request_t* req) {
  if (!req) {
    return PDF_SIGN_INVALID_ARGUMENT;
  }

  if (!req->certificate || !req->input_path || !req->output_path || !req->private_key) {
    return PDF_SIGN_INVALID_ARGUMENT;
  }

  FILE* original = fopen(req->input_path, "r");
  if (!original) {
    return PDF_SIGN_INVALID_ARGUMENT;
  }

  if (req->input_path == req->output_path) {
    fclose(original);
    return PDF_SIGN_INVALID_ARGUMENT;
  }

  if (X509_check_private_key(req->certificate, req->private_key) != 1) {
    fclose(original);
    return PDF_SIGN_CERTIFICATE_ERROR;
  }


  return PDF_SIGN_OK;
}
