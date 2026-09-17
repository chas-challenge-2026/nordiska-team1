#!/usr/bin/env bash

set -euo pipefail

PIN=1111
SO_PIN=0000

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

echo "Initializing SoftHSM token..."

softhsm2-util \
  --init-token \
  --free \
  --label key-load-dev \
  --so-pin "$SO_PIN" \
  --pin "$PIN"


echo "Generating test key and certificate..."

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
  -subj "/CN=PDF Signer Test"

openssl x509 \
  -in tests/data/signing_cert.pem \
  -outform DER \
  -out tests/data/signing_cert.der


echo "Importing private key into SoftHSM..."

softhsm2-util \
  --import tests/data/private_key_pkcs8.pem \
  --token key-load-dev \
  --label pdf-signer-test \
  --id 01 \
  --pin "$PIN"


echo "Generating PDF fixtures..."

cat > tests/data/input.pdf <<'EOF'
%PDF-1.4
1 0 obj
<< /Type /Catalog >>
endobj
EOF

XREF_OFFSET="$(wc -c < tests/data/input.pdf | tr -d ' ')"

cat >> tests/data/input.pdf <<EOF
xref
0 2
0000000000 65535 f 
0000000009 00000 n 
trailer
<< /Size 2 /Root 1 0 R >>
startxref
${XREF_OFFSET}
%%EOF
EOF


cat > tests/data/input_no_startxref.pdf <<'EOF'
%PDF-1.4
1 0 obj
<< /Type /Catalog >>
endobj
xref
0 1
0000000000 65535 f
trailer
<< /Size 1 >>
%%EOF
EOF


cp tests/data/input.pdf tests/data/input_oversized_startxref.pdf

cat >> tests/data/input_oversized_startxref.pdf <<'EOF'
startxref
999999999
%%EOF
EOF


cp tests/data/input.pdf tests/data/wrong_xref_offset.pdf

cat >> tests/data/wrong_xref_offset.pdf <<'EOF'
startxref
0
%%EOF
EOF

cat > tests/data/input_missing_size.pdf <<'EOF'
%PDF-1.4
1 0 obj
<< /Type /Catalog >>
endobj
EOF

XREF_OFFSET="$(wc -c < tests/data/input_missing_size.pdf | tr -d ' ')"

cat >> tests/data/input_missing_size.pdf <<EOF
xref
0 2
0000000000 65535 f
0000000009 00000 n
trailer
<< /Root 1 0 R >>
startxref
${XREF_OFFSET}
%%EOF
EOF


echo "SoftHSM objects:"

pkcs11-tool \
  --module /usr/lib/libsofthsm2.so \
  --token-label key-load-dev \
  --login \
  --pin "$PIN" \
  --list-objects


echo "Running tests..."

make clean
make test
