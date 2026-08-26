#!/usr/bin/env bash

set -euo pipefail

mapfile -d '' files < <(
    rg --files --null include src tests -g '*.cpp' -g '*.hpp' -g '*.h'
)

if ((${#files[@]} == 0)); then
    echo "No native C++ files found."
    exit 0
fi

clang-format --dry-run --Werror --style=file "${files[@]}"
