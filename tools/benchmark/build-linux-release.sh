#!/usr/bin/env bash
set -euo pipefail

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
binary="$script_dir/nordiska-benchmark"

echo "Building nordiska-benchmark release binary..."
g++ -std=c++23 -O3 -DNDEBUG -pthread \
    -I "$script_dir/include" \
    "$script_dir/src/database.cpp" \
    "$script_dir/src/benchmark.cpp" \
    "$script_dir/src/controller.cpp" \
    "$script_dir/src/main.cpp" \
    -o "$binary"

chmod 755 "$binary"
echo "Successfully built: $binary"
