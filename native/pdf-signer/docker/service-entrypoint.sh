#!/usr/bin/env bash

set -euo pipefail

cd /app

export OPENSSL_MODULES=/usr/lib/x86_64-linux-gnu/ossl-modules

PIN_FILE=/run/secrets/pdf_signer_pkcs11_pin
SO_PIN_FILE=/run/secrets/pdf_signer_pkcs11_so_pin

if [ ! -r "$PIN_FILE" ]; then
  echo "Missing secret: $PIN_FILE" >&2
  exit 1
fi

if [ ! -r "$SO_PIN_FILE" ]; then
  echo "Missing secret: $SO_PIN_FILE" >&2
  exit 1
fi

PIN="$(cat "$PIN_FILE")"
SO_PIN="$(cat "$SO_PIN_FILE")"

if [ -z "$PIN" ] || [ -z "$SO_PIN" ]; then
  echo "PKCS#11 secrets must not be empty" >&2
  exit 1
fi

export PDF_SIGNER_PKCS11_PIN="$PIN"


mkdir -p .dev/softhsm/tokens
mkdir -p tests/data

rm -rf .dev/softhsm/tokens/*

cat > .dev/softhsm/softhsm2.conf <<EOF
directories.tokendir = /app/.dev/softhsm/tokens
objectstore.backend = file
log.level = ERROR
slots.removable = false
EOF

export SOFTHSM2_CONF=/app/.dev/softhsm/softhsm2.conf

mkdir -p /run/pdf-signer

echo "Initializing SoftHSM..."

softhsm2-util \
  --init-token \
  --free \
  --label key-load-dev \
  --so-pin "$SO_PIN" \
  --pin "$PIN"

echo "Generating signer key and certificates..."

openssl genpkey \
  -algorithm RSA \
  -pkeyopt rsa_keygen_bits:2048 \
  -out tests/data/private_key.pem

openssl pkcs8 \
  -topk8 \
  -nocrypt \
  -in tests/data/private_key.pem \
  -out tests/data/private_key_pkcs8.pem

openssl req \
  -new \
  -x509 \
  -key tests/data/private_key.pem \
  -out tests/data/signing_cert.pem \
  -days 365 \
  -subj "/CN=PDF Signer Demo"

openssl req \
  -new \
  -x509 \
  -key tests/data/private_key.pem \
  -out tests/data/tsa_cert.pem \
  -days 365 \
  -subj "/CN=PDF TSA Demo" \
  -addext "keyUsage = critical,digitalSignature" \
  -addext "extendedKeyUsage = critical,timeStamping"

export SSL_CERT_FILE=/app/tests/data/tsa_cert.pem

echo "Importing signer key..."

softhsm2-util \
  --import tests/data/private_key_pkcs8.pem \
  --token key-load-dev \
  --label pdf-signer-test \
  --id 01 \
  --pin "$PIN"

echo "Starting TSA service..."

./tsa_server_service &
tsa_pid=$!

echo "Starting PDF signer service..."

./pdf_signer_service &
signer_pid=$!

shutdown() {
  echo "Shutting down signer services..."

  kill -TERM "$signer_pid" "$tsa_pid" 2>/dev/null || true

  wait "$signer_pid" 2>/dev/null || true
  wait "$tsa_pid" 2>/dev/null || true
}

trap shutdown SIGTERM SIGINT

set +e
wait -n "$signer_pid" "$tsa_pid"
status=$?
set -e

shutdown

exit "$status"
