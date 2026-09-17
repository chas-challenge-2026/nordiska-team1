CC = gcc

CFLAGS = -Wall -Wextra -Wpedantic -g -Iinclude
SAN_FLAGS = -fsanitize=address,undefined

LDLIBS = -lcrypto

KEY_LOAD_BIN = test_key_load
PKCS11_BIN = test_key_load_pkcs11
CERT_LOAD_BIN = test_cert_load
PDF_SIGN_BIN = test_pdf_sign

.PHONY: all test test-key-load test-cert-load test-pdf-sign test-pkcs11 test-docker clean 

all: $(KEY_LOAD_BIN) $(PKCS11_BIN) $(CERT_LOAD_BIN) $(PDF_SIGN_BIN)

$(KEY_LOAD_BIN): src/key_load.c src/debug_log.c tests/test_key_load.c
	$(CC) $(CFLAGS) $(SAN_FLAGS) $^ -o $@ $(SAN_FLAGS) $(LDLIBS)

$(PKCS11_BIN): src/key_load.c src/debug_log.c tests/test_key_load_pkcs11.c
	$(CC) $(CFLAGS) $^ -o $@ $(LDLIBS)

$(CERT_LOAD_BIN): src/cert_load.c src/debug_log.c tests/test_cert_load.c
	$(CC) $(CFLAGS) $(SAN_FLAGS) $^ -o $@ $(SAN_FLAGS) $(LDLIBS)

$(PDF_SIGN_BIN): src/pdf_sign.c src/key_load.c src/cert_load.c src/debug_log.c tests/test_pdf_sign.c
	$(CC) $(CFLAGS) $^ -o $@ $(LDLIBS)

test: test-key-load test-cert-load test-pdf-sign test-pkcs11

test-key-load: $(KEY_LOAD_BIN)
	./$(KEY_LOAD_BIN)

test-cert-load: $(CERT_LOAD_BIN)
	./$(CERT_LOAD_BIN)

test-pdf-sign: $(PDF_SIGN_BIN)
	SOFTHSM2_CONF=$(CURDIR)/.dev/softhsm/softhsm2.conf ./$(PDF_SIGN_BIN)

test-pkcs11: $(PKCS11_BIN)
	SOFTHSM2_CONF=$(CURDIR)/.dev/softhsm/softhsm2.conf ./$(PKCS11_BIN)

test-docker:
	docker build -f docker/Dockerfile.test -t pdf-signer-test .
	docker run --rm pdf-signer-test

test-docker-debug:
	docker build -f docker/Dockerfile.test -t pdf-signer-test .
	docker run --rm -e PDF_SIGNER_DEBUG=1 pdf-signer-test

clean:
	rm -f $(KEY_LOAD_BIN) $(PKCS11_BIN) $(CERT_LOAD_BIN) $(PDF_SIGN_BIN) /tests/data/output.pdf
