CC = gcc

CFLAGS = -Wall -Wextra -Wpedantic -g -Iinclude
SAN_FLAGS = -fsanitize=address,undefined


LDLIBS = -lcrypto -lcurl -pthread

KEY_LOAD_BIN = test_key_load
PKCS11_BIN = test_key_load_pkcs11
CERT_LOAD_BIN = test_cert_load
PDF_SIGN_BIN = test_pdf_sign
TSA_CLIENT_BIN = test_tsa_client
SIGNER_SERVICE_BIN = pdf_signer_service
SIGNER_CLIENT_BIN = test_signer_client
TSA_SERVER_BIN = test_tsa_server
TSA_SERVICE_BIN = tsa_server_service

.PHONY: all test test-key-load test-cert-load test-pdf-sign test-pkcs11 test-tsa-client test-docker test-signer-service test-tsa-server clean 
all: $(KEY_LOAD_BIN) $(PKCS11_BIN) $(CERT_LOAD_BIN) $(PDF_SIGN_BIN) $(TSA_CLIENT_BIN) $(SIGNER_SERVICE_BIN) $(SIGNER_CLIENT_BIN) $(TSA_SERVER_BIN) $(TSA_SERVICE_BIN)

$(KEY_LOAD_BIN): src/key_load.c src/debug_log.c tests/test_key_load.c
	$(CC) $(CFLAGS) $(SAN_FLAGS) $^ -o $@ $(SAN_FLAGS) $(LDLIBS)

$(PKCS11_BIN): src/key_load.c src/debug_log.c tests/test_key_load_pkcs11.c
	$(CC) $(CFLAGS) $^ -o $@ $(LDLIBS)

$(CERT_LOAD_BIN): src/cert_load.c src/debug_log.c tests/test_cert_load.c
	$(CC) $(CFLAGS) $(SAN_FLAGS) $^ -o $@ $(SAN_FLAGS) $(LDLIBS)

$(PDF_SIGN_BIN): src/pdf_sign.c src/key_load.c src/cert_load.c src/debug_log.c tests/test_pdf_sign.c src/tsa_client.c
	$(CC) $(CFLAGS) $^ -o $@ $(LDLIBS)

$(TSA_CLIENT_BIN): src/tsa_client.c tests/test_tsa_client.c
	$(CC) $(CFLAGS) $^ -o $@ $(LDLIBS)

$(SIGNER_SERVICE_BIN): src/main.c src/signer_server.c src/pdf_sign.c src/key_load.c src/cert_load.c src/debug_log.c src/tsa_client.c
	$(CC) $(CFLAGS) $^ -o $@ $(LDLIBS)

$(SIGNER_CLIENT_BIN): tests/test_signer_client.c
	$(CC) $(CFLAGS) $^ -o $@

$(TSA_SERVER_BIN): src/tsa_server.c src/key_load.c src/cert_load.c src/debug_log.c tests/test_tsa_server.c
	$(CC) $(CFLAGS) $^ -o $@ $(LDLIBS)

$(TSA_SERVICE_BIN): src/tsa_main.c src/tsa_http_server.c src/tsa_server.c src/key_load.c src/cert_load.c src/debug_log.c
	$(CC) $(CFLAGS) $^ -o $@ $(LDLIBS)

test: test-key-load test-cert-load test-pdf-sign test-pkcs11 test-tsa-client test-signer-service test-tsa-server

test-key-load: $(KEY_LOAD_BIN)
	./$(KEY_LOAD_BIN)

test-cert-load: $(CERT_LOAD_BIN)
	./$(CERT_LOAD_BIN)

test-pdf-sign: $(PDF_SIGN_BIN)
	SOFTHSM2_CONF=$(CURDIR)/.dev/softhsm/softhsm2.conf ./$(PDF_SIGN_BIN)

test-pkcs11: $(PKCS11_BIN)
	SOFTHSM2_CONF=$(CURDIR)/.dev/softhsm/softhsm2.conf ./$(PKCS11_BIN)

test-signer-service: $(SIGNER_SERVICE_BIN) $(SIGNER_CLIENT_BIN)
	@set -e; \
	SOFTHSM2_CONF=$(CURDIR)/.dev/softhsm/softhsm2.conf ./$(SIGNER_SERVICE_BIN) & \
	service_pid=$$!; \
	i=0; \
	while [ ! -S /tmp/pdf-signer.sock ]; do \
		i=$$((i + 1)); \
		if [ $$i -ge 50 ]; then \
			echo "Signer service socket was not created"; \
			kill -TERM $$service_pid 2>/dev/null || true; \
			exit 1; \
		fi; \
		sleep 0.1; \
	done; \
	./$(SIGNER_CLIENT_BIN); \
	kill -TERM $$service_pid; \
	wait $$service_pid

test-docker:
	docker build -f docker/Dockerfile.test -t pdf-signer-test .
	docker run --rm pdf-signer-test

test-docker-debug:
	docker build -f docker/Dockerfile.test -t pdf-signer-test .
	docker run --rm -e PDF_SIGNER_DEBUG=1 pdf-signer-test

test-tsa-server: $(TSA_SERVER_BIN)
	SOFTHSM2_CONF=$(CURDIR)/.dev/softhsm/softhsm2.conf ./$(TSA_SERVER_BIN)

test-tsa-client: $(TSA_SERVICE_BIN) $(TSA_CLIENT_BIN)
	@set -e; \
	SOFTHSM2_CONF=$(CURDIR)/.dev/softhsm/softhsm2.conf ./$(TSA_SERVICE_BIN) & \
	service_pid=$$!; \
	trap 'kill -TERM $$service_pid 2>/dev/null || true; wait $$service_pid 2>/dev/null || true' EXIT; \
	i=0; \
	while ! ./$(TSA_CLIENT_BIN) >/dev/null 2>&1; do \
		i=$$((i + 1)); \
		if [ $$i -ge 50 ]; then \
			echo "TSA HTTP service did not become ready"; \
			exit 1; \
		fi; \
		sleep 0.1; \
	done; \
	./$(TSA_CLIENT_BIN); \
	kill -TERM $$service_pid; \
	wait $$service_pid; \
	trap - EXIT

clean:
	rm -f $(KEY_LOAD_BIN) $(PKCS11_BIN) $(CERT_LOAD_BIN) $(PDF_SIGN_BIN) $(TSA_CLIENT_BIN) $(SIGNER_SERVICE_BIN) $(SIGNER_CLIENT_BIN) $(TSA_SERVER_BIN) $(TSA_SERVICE_BIN) tests/data/output.pdf
