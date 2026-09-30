#!/usr/bin/env bash
set -euo pipefail

PREFIX="${HOME}/.local/openssl-3.3"
OPENSSL_VERSION="3.3.2"
SRC_URL="https://github.com/openssl/openssl/releases/download/openssl-${OPENSSL_VERSION}/openssl-${OPENSSL_VERSION}.tar.gz"

echo "=== Installing OpenSSL ${OPENSSL_VERSION} into ${PREFIX} ==="

TMP_DIR=$(mktemp -d)
trap 'rm -rf "${TMP_DIR}"' EXIT

cd "${TMP_DIR}"
echo "Downloading OpenSSL ${OPENSSL_VERSION}..."
wget -q "${SRC_URL}"

echo "Extracting..."
tar -xzf "openssl-${OPENSSL_VERSION}.tar.gz"
cd "openssl-${OPENSSL_VERSION}"

echo "Configuring..."
./config --prefix="${PREFIX}" --openssldir="${PREFIX}/ssl" no-docs no-tests shared

echo "Compiling with $(nproc) jobs..."
make -j"$(nproc)"

echo "Installing..."
make install_sw

echo "=== OpenSSL ${OPENSSL_VERSION} successfully installed to ${PREFIX} ==="
echo "To use with CMake, pass: -DOPENSSL_ROOT_DIR=${PREFIX}"
