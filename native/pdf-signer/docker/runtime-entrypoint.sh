#!/usr/bin/env bash

set -euo pipefail

cd /app/pdf-signer

PIN="${PDF_SIGNER_PKCS11_PIN:-1111}"
SO_PIN="${PDF_SIGNER_PKCS11_SO_PIN:-0000}"

export PDF_SIGNER_PKCS11_PIN="$PIN"

mkdir -p .dev/softhsm/tokens
mkdir -p tests/data

rm -rf .dev/softhsm/tokens/*

cat > .dev/softhsm/softhsm2.conf <<EOF
directories.tokendir = /app/pdf-signer/.dev/softhsm/tokens
objectstore.backend = file
log.level = ERROR
slots.removable = false
EOF

export SOFTHSM2_CONF=/app/pdf-signer/.dev/softhsm/softhsm2.conf

echo "Initializing SoftHSM..."

softhsm2-util \
  --init-token \
  --free \
  --label key-load-dev \
  --so-pin "$SO_PIN" \
  --pin "$PIN"

echo "Generating signer certificates..."

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

openssl x509 \
  -in tests/data/signing_cert.pem \
  -outform DER \
  -out tests/data/signing_cert.der

export SSL_CERT_FILE=/app/pdf-signer/tests/data/tsa_cert.pem

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

i=0

while [ ! -S /tmp/pdf-signer.sock ]; do
  i=$((i + 1))

  if [ "$i" -ge 50 ]; then
    echo "PDF signer service failed to start"
    kill -TERM "$signer_pid" "$tsa_pid" 2>/dev/null || true
    wait "$signer_pid" 2>/dev/null || true
    wait "$tsa_pid" 2>/dev/null || true
    exit 1
  fi

  sleep 0.1
done

echo "Starting Nordiska API..."

dotnet /app/Nordiska.FrontendApi.dll &
app_pid=$!

shutdown() {
  echo "Shutting down services..."

  kill -TERM "$app_pid" "$signer_pid" "$tsa_pid" 2>/dev/null || true

  wait "$app_pid" 2>/dev/null || true
  wait "$signer_pid" 2>/dev/null || true
  wait "$tsa_pid" 2>/dev/null || true
}

trap shutdown SIGTERM SIGINT

set +e
wait -n "$app_pid" "$signer_pid" "$tsa_pid"
status=$?
set -e

shutdown

exit "$status"
