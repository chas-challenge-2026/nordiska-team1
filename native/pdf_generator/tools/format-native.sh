#!/usr/bin/env bash

set -euo pipefail

if command -v rg >/dev/null 2>&1; then
    mapfile -d '' files < <(
        rg --files --null include src tests -g '*.cpp' -g '*.hpp' -g '*.h'
    )
else
    mapfile -d '' files < <(
        find include src tests -type f \( -name '*.cpp' -o -name '*.hpp' -o -name '*.h' \) -print0
    )
fi

if ((${#files[@]} == 0)); then
    echo "No native C++ files found."
    exit 0
fi

clang-format -i --style=file "${files[@]}"
