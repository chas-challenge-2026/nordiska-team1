CC = gcc

CFLAGS = -Wall -Wextra -Wpedantic -g -Iinclude
SAN_FLAGS = -fsanitize=address,undefined

LDLIBS = -lcrypto

UNIT_BIN = test_key_load
PKCS11_BIN = test_key_load_pkcs11

.PHONY: all test test-unit test-pkcs11 clean

all: $(UNIT_BIN) $(PKCS11_BIN)

$(UNIT_BIN): src/key_load.c tests/test_key_load.c
	$(CC) $(CFLAGS) $(SAN_FLAGS) $^ -o $@ $(SAN_FLAGS) $(LDLIBS)

$(PKCS11_BIN): src/key_load.c tests/test_key_load_pkcs11.c
	$(CC) $(CFLAGS) $^ -o $@ $(LDLIBS)

test: test-unit test-pkcs11

test-unit: $(UNIT_BIN)
	./$(UNIT_BIN)

test-pkcs11: $(PKCS11_BIN)
	SOFTHSM2_CONF=$(CURDIR)/.dev/softhsm/softhsm2.conf ./$(PKCS11_BIN)

clean:
	rm -f $(UNIT_BIN) $(PKCS11_BIN)
