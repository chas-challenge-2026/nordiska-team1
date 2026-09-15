#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROFILE_DIR="$SCRIPT_DIR/config/workloads"
OUTPUT_ROOT="${1:-$SCRIPT_DIR/generated}"

shopt -s nullglob
profiles=("$PROFILE_DIR"/*.json)
if (( ${#profiles[@]} == 0 )); then
    echo "No workload profiles found in $PROFILE_DIR" >&2
    exit 1
fi

for profile_path in "${profiles[@]}"; do
    profile_name="$(basename "$profile_path" .json)"
    echo "Generating profile: $profile_name"
    python3 "$SCRIPT_DIR/generate_data.py" \
        --config "$SCRIPT_DIR/config/generator.json" \
        --profile "$profile_name" \
        --output "$OUTPUT_ROOT/$profile_name"
done

echo "Generated ${#profiles[@]} workload profiles under $OUTPUT_ROOT"
