CC = gcc

CFLAGS = -Wall -Wextra -Wpedantic -g \
         -fsanitize=address,undefined \
         -Iinclude

LDFLAGS = -fsanitize=address,undefined

LDLIBS = -lcrypto

TEST_BIN = test_key_load

.PHONY: all test clean

all: $(TEST_BIN)

$(TEST_BIN): src/key_load.c tests/test_key_load.c
	$(CC) $(CFLAGS) $^ -o $@ $(LDFLAGS) $(LDLIBS)

test: $(TEST_BIN)
	./$(TEST_BIN)

clean:
	rm -f $(TEST_BIN)
