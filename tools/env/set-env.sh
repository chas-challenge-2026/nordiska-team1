#!/usr/bin/env bash
# Nordiska Environment Manager shell wrapper
#
# Usage:
#   source tools/env/set-env.sh     # Interactive menu + exports variables directly to active shell session
#   ./tools/env/set-env.sh          # Runs interactive menu and saves variables to infra/.env

# Resolve script directory (following symlinks)
_source="${BASH_SOURCE[0]}"
while [ -h "$_source" ]; do
    _dir="$(cd -P "$(dirname "$_source")" && pwd)"
    _source="$(readlink "$_source")"
    [[ $_source != /* ]] && _source="$_dir/$_source"
done
_script_dir="$(cd -P "$(dirname "$_source")" && pwd)"
_repo_root="$(cd -P "$_script_dir/../.." && pwd)"
_binary="$_script_dir/nordiska-env"

# Auto-build binary if not present
if [[ ! -x "$_binary" ]]; then
    echo "nordiska-env binary not found. Building release binary..."
    bash "$_script_dir/build-linux-release.sh"
fi

# Run the tool
"$_binary" "$@"
_exit_code=$?

# If sourced into an interactive shell session, export the variables into the calling shell
if [[ "${BASH_SOURCE[0]}" != "${0}" ]]; then
    if [[ $_exit_code -eq 0 && -f "$_repo_root/infra/.env.export" ]]; then
        source "$_repo_root/infra/.env.export"
        echo ""
        echo " [OK] Environment variables exported into current shell session."
        if [[ -n "${NORDISKA_DATABASE:-}" ]]; then
            echo "      NORDISKA_DATABASE = ${NORDISKA_DATABASE}"
        fi
    fi
else
    if [[ $_exit_code -eq 0 ]]; then
        echo ""
        echo "Tip: To export these variables directly into your current shell, run:"
        echo "  source tools/env/set-env.sh"
    fi
fi

unset _source _dir _script_dir _repo_root _binary _exit_code
