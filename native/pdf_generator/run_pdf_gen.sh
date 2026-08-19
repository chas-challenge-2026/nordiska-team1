#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

PDF_GENERATOR="$SCRIPT_DIR/build/pdf_generator"

if [[ ! -x "$PDF_GENERATOR" ]]; then
    echo "PDF generator is not built yet. Run: cmake -S . -B build && cmake --build build" >&2
    exit 1
fi

"$PDF_GENERATOR" \
  "$SCRIPT_DIR/sample-input.json" \
  "$SCRIPT_DIR/report.pdf"
