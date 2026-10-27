#!/usr/bin/env bash
# ==============================================================================
# Nordiska Benchmark Controller (Native TUI)
# ==============================================================================
set -euo pipefail

SOURCE_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
BENCH_BIN="$SOURCE_DIR/nordiska-benchmark"

if [[ ! -x "$BENCH_BIN" ]]; then
    "$SOURCE_DIR/build-linux-release.sh"
fi

exec "$BENCH_BIN" --menu "$@"
