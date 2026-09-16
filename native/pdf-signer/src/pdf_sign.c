#include "pdf_sign.h"
#include <stdio.h>
#include <ctype.h>
#include <limits.h>

/*---------------------------INTERNAL-----------------------------*/

static pdf_sign_status_t copy_pdf_to_output(const char* input_path, const char* output_path) {
  if (!input_path || !output_path) {
    return PDF_SIGN_INVALID_ARGUMENT;
  }

  FILE* input = fopen(input_path, "rb");
  if (!input) {
    return PDF_SIGN_INVALID_PDF;
  }

  FILE* output = fopen(output_path, "wb");
  if (!output) {
    fclose(input);
    return PDF_SIGN_OUTPUT_ERROR;
  }

  unsigned char buffer[64 * 1024];
  size_t        bytes_read = 0;

  while ((bytes_read = fread(buffer, 1, sizeof(buffer), input)) > 0) {
    size_t total_written = 0;

    while (total_written < bytes_read) {
      size_t written = fwrite(buffer + total_written, 1, bytes_read - total_written, output);

      if (written == 0) {
        fclose(output);
        fclose(input);
        return PDF_SIGN_OUTPUT_ERROR;
      }

      total_written += written;
    }
  }

  if (ferror(input)) {
    fclose(output);
    fclose(input);
    return PDF_SIGN_INTERNAL_ERROR;
  }

  fclose(output);
  fclose(input);

  return PDF_SIGN_OK;
}

static pdf_sign_status_t find_startxref(const char* path, long* xref_offset) {

  if (!path || !xref_offset) {
    return PDF_SIGN_INVALID_ARGUMENT;
  }

  FILE* f = fopen(path, "rb");
  if (!f) {
    return PDF_SIGN_INVALID_PDF;
  }

  if (fseek(f, 0, SEEK_END) != 0) {
    fclose(f);
    return PDF_SIGN_INVALID_PDF;
  }

  long file_size = ftell(f);
  if (file_size <= 0) {
    fclose(f);
    return PDF_SIGN_INVALID_PDF;
  }
  long scan_size = file_size < 4096 ? file_size : 4096;

  if (fseek(f, -scan_size, SEEK_END) != 0) {
    fclose(f);
    return PDF_SIGN_INVALID_PDF;
  }

  unsigned char buffer[scan_size];
  size_t        total_read = 0;

  while (total_read < (size_t)scan_size) {
    size_t bytes_read = fread(buffer + total_read, 1, scan_size - total_read, f);

    if (bytes_read == 0) {
      fclose(f);
      return PDF_SIGN_INTERNAL_ERROR;
    }
    total_read += bytes_read;
  }

  if (ferror(f)) {
    fclose(f);
    return PDF_SIGN_INTERNAL_ERROR;
  }

  const char* target     = "startxref";
  size_t      target_len = strlen(target);

  long found = -1;
  for (long i = scan_size - (long)target_len; i >= 0; i--) {
    if (memcmp(buffer + i, target, target_len) == 0) {
      found = i;
      break;
    }
  }
  if (found < 0) {
    fclose(f);
    return PDF_SIGN_INVALID_PDF;
  }

  size_t pos = (size_t)found + strlen("startxref");

  while (pos < (size_t)scan_size && isspace(buffer[pos])) {
    pos++;
  }

  if (pos >= (size_t)scan_size || !isdigit(buffer[pos])) {
    fclose(f);
    return PDF_SIGN_INVALID_PDF;
  }

  *xref_offset = 0;

  while (pos < (size_t)scan_size && isdigit(buffer[pos])) {
    int digit = buffer[pos] - '0';

    if (*xref_offset > (LONG_MAX - digit) / 10) {
      fclose(f);
      return PDF_SIGN_INVALID_PDF;
    }
    *xref_offset = *xref_offset * 10 + digit;
    pos++;
  }

  if (*xref_offset >= file_size) {
    fclose(f);
    return PDF_SIGN_INVALID_PDF;
  }

  fclose(f);
  return PDF_SIGN_OK;
}


/*****************************************************************/


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

  if (strcmp(req->input_path, req->output_path) == 0) {
    fclose(original);
    return PDF_SIGN_INVALID_ARGUMENT;
  }

  if (X509_check_private_key(req->certificate, req->private_key) != 1) {
    fclose(original);
    return PDF_SIGN_CERTIFICATE_ERROR;
  }

  long              xref_offset = 0;
  pdf_sign_status_t status      = find_startxref(req->input_path, &xref_offset);
  if (status != PDF_SIGN_OK) {
    return status;
  }

  return copy_pdf_to_output(req->input_path, req->output_path);
}
