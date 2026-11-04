#!/usr/bin/env bash
set -euo pipefail

fuzz_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
project_root=$(cd -- "$fuzz_root/../.." && pwd)
build_dir=${NORDISKA_FUZZ_BUILD_DIR:-"$project_root/build/fuzz"}
seconds=${1:-300}
output_dir=${2:-"$build_dir/findings"}

if [[ ${1:-} == --help || ${1:-} == -h ]]; then
    echo 'Usage: tests/fuzz/run.sh [seconds=300] [output-directory=build/fuzz/findings]'
    echo 'Use 0 for unlimited time. Ctrl+C stops safely; the same output directory resumes.'
    echo 'Set NORDISKA_FUZZ_BUILD_DIR to use another instrumented build.'
    exit 0
fi
if (( $# > 2 )) || [[ ! $seconds =~ ^[0-9]+$ ]]; then
    echo 'Expected a nonnegative duration in seconds and an optional output directory.' >&2
    exit 2
fi
if ! command -v afl-fuzz > /dev/null; then
    echo 'afl-fuzz is missing. See tests/fuzz/README.md for build instructions.' >&2
    exit 2
fi
binary="$build_dir/tests/fuzz/nordiska_fuzz_json_to_pdf"
if [[ ! -x $binary ]]; then
    echo "Missing fuzz target: $binary. Build it using the AFL++ compiler first." >&2
    exit 2
fi
max_input_bytes=$("$binary" --max-input-bytes)
if [[ ! $max_input_bytes =~ ^[1-9][0-9]*$ ]]; then
    echo 'Cannot query the input ceiling. Rebuild the fuzz target first.' >&2
    exit 2
fi

# AFL++ needs sanitizer failures to abort, rather than exit successfully or recover.
export ASAN_OPTIONS=${ASAN_OPTIONS:-'abort_on_error=1:detect_leaks=0:symbolize=0'}
export UBSAN_OPTIONS=${UBSAN_OPTIONS:-'halt_on_error=1:abort_on_error=1:print_stacktrace=1'}
input_dir="$fuzz_root/seeds"
if [[ -f $output_dir/default/fuzzer_stats ]]; then
    input_dir=-
fi
args=(-i "$input_dir" -o "$output_dir" -x "$fuzz_root/json.dict" -m none -t 5000 -G "$max_input_bytes")
if [[ $seconds != 0 ]]; then
    args+=(-V "$seconds")
fi
# exec preserves AFL++'s interactive dashboard and Ctrl+C handling.
exec afl-fuzz "${args[@]}" -- "$binary" --afl
