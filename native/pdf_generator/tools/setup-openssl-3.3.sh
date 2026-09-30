#!/usr/bin/env bash
# ==============================================================================
# Script: setup-openssl-3.3.sh
# Purpose: Builds and installs modern OpenSSL (v3.3.2) locally for developer workstations.
#
# WHY THIS IS NEEDED:
# -------------------
# The Nordiska PDF signing subsystem requires OpenSSL >= 3.2.0 because it relies
# on the cryptographic function `CMS_final_digest()`, which was introduced in
# OpenSSL 3.2.0 (November 2023) to compute detached CMS SignedData digests.
#
# Most enterprise Linux LTS distributions (including Ubuntu 22.04/24.04 LTS and
# Debian 12 Bookworm) standardize on the older OpenSSL 3.0.x LTS series via `apt`.
# Without OpenSSL >= 3.2.0, native CMake builds and ctest unit test runs on host
# developer machines will fail with an error indicating an unsuitable OpenSSL version.
#
# WHAT THIS SCRIPT DOES TO YOUR SYSTEM:
# -------------------------------------
# 1. 100% Non-Destructive & Isolated:
#    - Does NOT touch system directories (/usr/lib, /usr/include, /etc/ssl).
#    - Does NOT require `sudo` or administrator privileges.
#    - Does NOT interfere with your distribution's package manager (`apt`).
# 2. Local User Space Target:
#    - Installs strictly into: ${HOME}/.local/openssl-3.3/
#    - Download and build artifacts are compiled in a temporary directory in /tmp
#      and automatically cleaned up on exit.
# 3. How the Project Uses It:
#    - CMake (`native/pdf_generator/CMakeLists.txt`) automatically detects this
#      directory if present and configures modern OpenSSL with runtime RPATH.
# 4. How to Uninstall / Revert:
#    - Simply remove the directory at any time: `rm -rf ~/.local/openssl-3.3`
# ==============================================================================
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
