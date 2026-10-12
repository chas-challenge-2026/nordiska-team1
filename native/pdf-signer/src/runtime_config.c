#include "runtime_config.h"

#include <stdlib.h>

static const char* env_or_default(const char* name, const char* fallback) {
  const char* value = getenv(name);

  if (!value || value[0] == '\0') {
    return fallback;
  }

  return value;
}

const char* runtime_config_pkcs11_provider(void) {
  return env_or_default("PDF_SIGNER_PKCS11_PROVIDER", "pkcs11");
}

const char* runtime_config_pkcs11_module(void) {
  return env_or_default("PDF_SIGNER_PKCS11_MODULE", "/usr/lib/softhsm/libsofthsm2.so");
}

const char* runtime_config_pkcs11_uri(void) {
  return env_or_default("PDF_SIGNER_PKCS11_URI",
                        "pkcs11:token=key-load-dev;object=pdf-signer-test;type=private");
}

const char* runtime_config_signing_cert(void) {
  return env_or_default("PDF_SIGNER_CERT_PATH", "tests/data/signing_cert.pem");
}

const char* runtime_config_tsa_cert(void) {
  return env_or_default("PDF_SIGNER_TSA_CERT_PATH", "tests/data/tsa_cert.pem");
}

const char* runtime_config_signer_socket(void) {
  return env_or_default("PDF_SIGNER_SOCKET_PATH", "/tmp/pdf-signer.sock");
}

const char* runtime_config_tsa_url(void) {
  return env_or_default("PDF_SIGNER_TSA_URL", "http://127.0.0.1:8081/");
}
