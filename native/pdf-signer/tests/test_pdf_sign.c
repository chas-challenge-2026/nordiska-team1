#include "key_load.h"
#include "pdf_sign.h"
#include "cert_load.h"
#include <stdio.h>
#include <string.h>

#define CHECK(condition)                                                                           \
  do {                                                                                             \
    if (!(condition)) {                                                                            \
      fprintf(stderr, "CHECK failed: %s:%d: %s\n", __FILE__, __LINE__, #condition);                \
      return 1;                                                                                    \
    }                                                                                              \
  } while (0)

static int files_equal(const char* path_a, const char* path_b) {
  FILE* a = fopen(path_a, "rb");
  if (!a)
    return 0;

  FILE* b = fopen(path_b, "rb");
  if (!b) {
    fclose(a);
    return 0;
  }

  unsigned char buf_a[4096];
  unsigned char buf_b[4096];

  while (1) {
    size_t read_a = fread(buf_a, 1, sizeof(buf_a), a);
    size_t read_b = fread(buf_b, 1, sizeof(buf_b), b);

    if (read_a != read_b) {
      fclose(a);
      fclose(b);
      return 0;
    }

    if (read_a == 0) {
      break;
    }

    if (memcmp(buf_a, buf_b, read_a) != 0) {
      fclose(a);
      fclose(b);
      return 0;
    }
  }

  int equal = !ferror(a) && !ferror(b);

  fclose(a);
  fclose(b);

  return equal;
}

static int test_pdf_sign_key_cert(void) {
  key_loader_t* loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source      = KEY_SOURCE_FILE,
      .u.file.path = "tests/data/private_key.pem",
  };

  key_handle_t key_handle = {0};

  key_status_t key_status = key_load(loader, &spec, NULL, &key_handle);

  CHECK(key_status == KEY_STATUS_OK);
  CHECK(key_handle.pkey != NULL);

  cert_handle_t cert_handle = {0};

  cert_status_t cert_status = cert_load_file("tests/data/signing_cert.pem", &cert_handle);

  CHECK(cert_status == CERT_STATUS_OK);
  CHECK(cert_handle.certificate != NULL);

  pdf_sign_request_t req = {
      .input_path        = "tests/data/input.pdf",
      .output_path       = "tests/data/output.pdf",
      .private_key       = key_handle.pkey,
      .certificate       = cert_handle.certificate,
      .certificate_chain = NULL,
  };

  pdf_sign_status_t sign_status = pdf_sign(&req);


  cert_dispose(&cert_handle);
  key_dispose(&key_handle);
  key_loader_destroy(loader);
  CHECK(sign_status == PDF_SIGN_OK);

  return 0;
}

static int test_pdf_sign_mismatched_key_cert(void) {
  key_loader_t* loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source      = KEY_SOURCE_FILE,
      .u.file.path = "tests/data/private_key.pem",
  };

  key_handle_t key_handle = {0};

  key_status_t key_status = key_load(loader, &spec, NULL, &key_handle);

  CHECK(key_status == KEY_STATUS_OK);
  CHECK(key_handle.pkey != NULL);

  cert_handle_t cert_handle = {0};

  cert_status_t cert_status = cert_load_file("tests/data/wrong_cert.pem", &cert_handle);

  CHECK(cert_status == CERT_STATUS_OK);
  CHECK(cert_handle.certificate != NULL);

  pdf_sign_request_t req = {
      .input_path        = "tests/data/input.pdf",
      .output_path       = "tests/data/output.pdf",
      .private_key       = key_handle.pkey,
      .certificate       = cert_handle.certificate,
      .certificate_chain = NULL,
  };

  pdf_sign_status_t sign_status = pdf_sign(&req);


  cert_dispose(&cert_handle);
  key_dispose(&key_handle);
  key_loader_destroy(loader);
  CHECK(sign_status == PDF_SIGN_CERTIFICATE_ERROR);

  return 0;
}

static int test_pdf_sign_same_input_output(void) {
  key_loader_t* loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source      = KEY_SOURCE_FILE,
      .u.file.path = "tests/data/private_key.pem",
  };

  key_handle_t key_handle = {0};

  key_status_t key_status = key_load(loader, &spec, NULL, &key_handle);

  CHECK(key_status == KEY_STATUS_OK);
  CHECK(key_handle.pkey != NULL);

  cert_handle_t cert_handle = {0};

  cert_status_t cert_status = cert_load_file("tests/data/signing_cert.pem", &cert_handle);

  CHECK(cert_status == CERT_STATUS_OK);
  CHECK(cert_handle.certificate != NULL);

  pdf_sign_request_t req = {
      .input_path        = "tests/data/input.pdf",
      .output_path       = "tests/data/input.pdf",
      .private_key       = key_handle.pkey,
      .certificate       = cert_handle.certificate,
      .certificate_chain = NULL,
  };

  pdf_sign_status_t sign_status = pdf_sign(&req);


  cert_dispose(&cert_handle);
  key_dispose(&key_handle);
  key_loader_destroy(loader);
  CHECK(sign_status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}

static int test_pdf_sign_null_input(void) {
  key_loader_t* loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source      = KEY_SOURCE_FILE,
      .u.file.path = "tests/data/private_key.pem",
  };

  key_handle_t key_handle = {0};

  key_status_t key_status = key_load(loader, &spec, NULL, &key_handle);

  CHECK(key_status == KEY_STATUS_OK);
  CHECK(key_handle.pkey != NULL);

  cert_handle_t cert_handle = {0};

  cert_status_t cert_status = cert_load_file("tests/data/signing_cert.pem", &cert_handle);

  CHECK(cert_status == CERT_STATUS_OK);
  CHECK(cert_handle.certificate != NULL);

  pdf_sign_request_t req = {
      .input_path        = "",
      .output_path       = "tests/data/output_path.pdf",
      .private_key       = key_handle.pkey,
      .certificate       = cert_handle.certificate,
      .certificate_chain = NULL,
  };

  pdf_sign_status_t sign_status = pdf_sign(&req);


  cert_dispose(&cert_handle);
  key_dispose(&key_handle);
  key_loader_destroy(loader);
  CHECK(sign_status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}

static int test_pdf_sign_null_output(void) {
  key_loader_t* loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source      = KEY_SOURCE_FILE,
      .u.file.path = "tests/data/private_key.pem",
  };

  key_handle_t key_handle = {0};

  key_status_t key_status = key_load(loader, &spec, NULL, &key_handle);

  CHECK(key_status == KEY_STATUS_OK);
  CHECK(key_handle.pkey != NULL);

  cert_handle_t cert_handle = {0};

  cert_status_t cert_status = cert_load_file("tests/data/signing_cert.pem", &cert_handle);

  CHECK(cert_status == CERT_STATUS_OK);
  CHECK(cert_handle.certificate != NULL);

  pdf_sign_request_t req = {
      .input_path        = "tests/data/input_path.pdf",
      .output_path       = "",
      .private_key       = key_handle.pkey,
      .certificate       = cert_handle.certificate,
      .certificate_chain = NULL,
  };

  pdf_sign_status_t sign_status = pdf_sign(&req);


  cert_dispose(&cert_handle);
  key_dispose(&key_handle);
  key_loader_destroy(loader);
  CHECK(sign_status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}

static int test_pdf_sign_null_pkey(void) {
  cert_handle_t cert_handle = {0};

  cert_status_t cert_status = cert_load_file("tests/data/signing_cert.pem", &cert_handle);

  CHECK(cert_status == CERT_STATUS_OK);
  CHECK(cert_handle.certificate != NULL);

  pdf_sign_request_t req = {
      .input_path        = "tests/data/input_path.pdf",
      .output_path       = "tests/data/output_path.pdf",
      .private_key       = NULL,
      .certificate       = cert_handle.certificate,
      .certificate_chain = NULL,
  };

  pdf_sign_status_t sign_status = pdf_sign(&req);


  cert_dispose(&cert_handle);
  CHECK(sign_status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_null_cert(void) {
  key_loader_t* loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source      = KEY_SOURCE_FILE,
      .u.file.path = "tests/data/private_key.pem",
  };

  key_handle_t key_handle = {0};

  key_status_t key_status = key_load(loader, &spec, NULL, &key_handle);

  CHECK(key_status == KEY_STATUS_OK);
  CHECK(key_handle.pkey != NULL);


  pdf_sign_request_t req = {
      .input_path        = "tests/data/input_path.pdf",
      .output_path       = "tests/data/output_path.pdf",
      .private_key       = key_handle.pkey,
      .certificate       = NULL,
      .certificate_chain = NULL,
  };

  pdf_sign_status_t sign_status = pdf_sign(&req);


  key_dispose(&key_handle);
  key_loader_destroy(loader);
  CHECK(sign_status == PDF_SIGN_INVALID_ARGUMENT);

  return 0;
}


static int test_pdf_sign_copy_pdf(void) {
  key_loader_t* loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source      = KEY_SOURCE_FILE,
      .u.file.path = "tests/data/private_key.pem",
  };

  key_handle_t key_handle = {0};

  key_status_t key_status = key_load(loader, &spec, NULL, &key_handle);

  CHECK(key_status == KEY_STATUS_OK);
  CHECK(key_handle.pkey != NULL);

  cert_handle_t cert_handle = {0};

  cert_status_t cert_status = cert_load_file("tests/data/signing_cert.pem", &cert_handle);

  CHECK(cert_status == CERT_STATUS_OK);
  CHECK(cert_handle.certificate != NULL);

  pdf_sign_request_t req = {
      .input_path        = "tests/data/input.pdf",
      .output_path       = "tests/data/output.pdf",
      .private_key       = key_handle.pkey,
      .certificate       = cert_handle.certificate,
      .certificate_chain = NULL,
  };

  pdf_sign_status_t sign_status = pdf_sign(&req);


  cert_dispose(&cert_handle);
  key_dispose(&key_handle);
  key_loader_destroy(loader);
  CHECK(sign_status == PDF_SIGN_OK);
  CHECK(files_equal("tests/data/input.pdf", "tests/data/output.pdf"));

  return 0;
}

static int test_pdf_sign_valid_startxref(void) {
  key_loader_t* loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source      = KEY_SOURCE_FILE,
      .u.file.path = "tests/data/private_key.pem",
  };

  key_handle_t key_handle = {0};

  key_status_t key_status = key_load(loader, &spec, NULL, &key_handle);

  CHECK(key_status == KEY_STATUS_OK);

  cert_handle_t cert_handle = {0};

  cert_status_t cert_status = cert_load_file("tests/data/signing_cert.pem", &cert_handle);

  CHECK(cert_status == CERT_STATUS_OK);

  pdf_sign_request_t req = {
      .input_path        = "tests/data/input.pdf",
      .output_path       = "tests/data/output.pdf",
      .private_key       = key_handle.pkey,
      .certificate       = cert_handle.certificate,
      .certificate_chain = NULL,
  };

  pdf_sign_status_t status = pdf_sign(&req);

  cert_dispose(&cert_handle);
  key_dispose(&key_handle);
  key_loader_destroy(loader);

  CHECK(status == PDF_SIGN_OK);

  return 0;
}


static int test_pdf_sign_missing_startxref(void) {
  key_loader_t* loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source      = KEY_SOURCE_FILE,
      .u.file.path = "tests/data/private_key.pem",
  };

  key_handle_t key_handle = {0};

  key_status_t key_status = key_load(loader, &spec, NULL, &key_handle);

  CHECK(key_status == KEY_STATUS_OK);

  cert_handle_t cert_handle = {0};

  cert_status_t cert_status = cert_load_file("tests/data/signing_cert.pem", &cert_handle);

  CHECK(cert_status == CERT_STATUS_OK);

  pdf_sign_request_t req = {
      .input_path        = "tests/data/input_no_startxref.pdf",
      .output_path       = "tests/data/output.pdf",
      .private_key       = key_handle.pkey,
      .certificate       = cert_handle.certificate,
      .certificate_chain = NULL,
  };

  pdf_sign_status_t status = pdf_sign(&req);

  cert_dispose(&cert_handle);
  key_dispose(&key_handle);
  key_loader_destroy(loader);

  CHECK(status == PDF_SIGN_INVALID_PDF);

  return 0;
}


static int test_pdf_sign_oversized_startxref(void) {
  key_loader_t* loader = key_loader_create(NULL);
  CHECK(loader != NULL);

  key_spec_t spec = {
      .source      = KEY_SOURCE_FILE,
      .u.file.path = "tests/data/private_key.pem",
  };

  key_handle_t key_handle = {0};

  key_status_t key_status = key_load(loader, &spec, NULL, &key_handle);

  CHECK(key_status == KEY_STATUS_OK);

  cert_handle_t cert_handle = {0};

  cert_status_t cert_status = cert_load_file("tests/data/signing_cert.pem", &cert_handle);

  CHECK(cert_status == CERT_STATUS_OK);

  pdf_sign_request_t req = {
      .input_path        = "tests/data/input_oversized_startxref.pdf",
      .output_path       = "tests/data/output.pdf",
      .private_key       = key_handle.pkey,
      .certificate       = cert_handle.certificate,
      .certificate_chain = NULL,
  };

  pdf_sign_status_t status = pdf_sign(&req);

  cert_dispose(&cert_handle);
  key_dispose(&key_handle);
  key_loader_destroy(loader);

  CHECK(status == PDF_SIGN_INVALID_PDF);

  return 0;
}

int main(void) {
  int failed = 0;

  failed += test_pdf_sign_key_cert();
  failed += test_pdf_sign_mismatched_key_cert();
  failed += test_pdf_sign_same_input_output();
  failed += test_pdf_sign_null_input();
  failed += test_pdf_sign_null_output();
  failed += test_pdf_sign_null_pkey();
  failed += test_pdf_sign_null_cert();
  failed += test_pdf_sign_copy_pdf();
  failed += test_pdf_sign_valid_startxref();
  failed += test_pdf_sign_missing_startxref();
  failed += test_pdf_sign_oversized_startxref();

  if (failed != 0) {
    fprintf(stderr, "%d tests failed\n", failed);
    return EXIT_FAILURE;
  }

  printf("All tests passed\n");

  return EXIT_SUCCESS;
}
