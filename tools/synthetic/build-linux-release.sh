#!/usr/bin/env bash
# Rebuild the checked-in Linux x86_64 release using the pinned compiler image.
set -euo pipefail
repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
compiler_image=gcc@sha256:5e927c284bf55a7dc796262e311a0703344f62f41f5621eb56843111b1d37e15
release_dir="$repo_root/tools/synthetic"
binary=nordiska-synthetic-linux-x86_64
docker run --rm --platform linux/amd64 --network none \
  --user "$(id -u):$(id -g)" \
  -v "$repo_root/native/synthetic_generator:/src:ro" \
  -v "$release_dir:/release" \
  "$compiler_image" \
  bash -c 'set -euo pipefail
    g++ -std=c++23 -O3 -DNDEBUG -static -pthread -Wl,--build-id=none -s \
      -I /src/include /src/src/generator.cpp /src/src/main.cpp /src/src/manager.cpp \
      -o /release/nordiska-synthetic-linux-x86_64.next
    chmod 755 /release/nordiska-synthetic-linux-x86_64.next
    mv /release/nordiska-synthetic-linux-x86_64.next /release/nordiska-synthetic-linux-x86_64'
(cd "$release_dir" && sha256sum "$binary" > "$binary.sha256")
printf 'Built %s\n' "$release_dir/$binary"
