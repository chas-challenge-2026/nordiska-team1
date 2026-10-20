#!/usr/bin/env bash
set -euo pipefail

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
binary="$script_dir/nordiska-env"

echo "Building nordiska-env release binary..."
g++ -std=c++23 -O3 -DNDEBUG -pthread \
    -I "$script_dir/include" \
    "$script_dir/src/env_file.cpp" \
    "$script_dir/src/secret_generator.cpp" \
    "$script_dir/src/menu.cpp" \
    "$script_dir/src/main.cpp" \
    -o "$binary"

chmod 755 "$binary"
echo "Successfully built: $binary"
