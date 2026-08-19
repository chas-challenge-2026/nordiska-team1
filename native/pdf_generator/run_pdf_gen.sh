#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

PDF_GENERATOR="$SCRIPT_DIR/build/pdf_generator_single"

if [[ ! -x "$PDF_GENERATOR" ]]; then
    echo "PDF generator is not built yet. Set VCPKG_ROOT and run: cmake --preset default && cmake --build --preset default" >&2
    exit 1
fi

"$PDF_GENERATOR" "$SCRIPT_DIR/sample-input.json"
