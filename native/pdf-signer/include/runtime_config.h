#ifndef RUNTIME_CONFIG_H
#define RUNTIME_CONFIG_H

/* Non-secret runtime configuration. */
const char* runtime_config_pkcs11_provider(void);
const char* runtime_config_pkcs11_module(void);
const char* runtime_config_pkcs11_uri(void);

const char* runtime_config_signing_cert(void);
const char* runtime_config_tsa_cert(void);

const char* runtime_config_signer_socket(void);
const char* runtime_config_tsa_url(void);

unsigned short runtime_config_tsa_port(void);
long           runtime_config_tsa_timeout_ms(void);

#endif
