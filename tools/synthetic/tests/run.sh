#!/usr/bin/env bash
# Native CLI/management regression tests. Docker is mocked; no real DB is touched.
set -euo pipefail
repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../../.." && pwd)
test_dir=$(mktemp -d)
trap 'rm -rf -- "$test_dir"' EXIT
export TEST_REPO="$test_dir/repo" TEST_STATE="$test_dir/state"
mkdir -p "$TEST_REPO/infra/v2" "$TEST_REPO/tools/synthetic" "$TEST_STATE" "$test_dir/bin"
cp "$repo_root/tools/synthetic/nordiska-synthetic-linux-x86_64" "$TEST_REPO/tools/synthetic/manager"
cli="$TEST_REPO/tools/synthetic/manager"
touch "$TEST_REPO/infra/v2/docker-compose.yml"
cat > "$test_dir/bin/docker" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "$TEST_STATE/calls"
if [[ $1 == compose ]]; then
  if [[ " $* " == *' ps --quiet db '* ]]; then echo fake-db
  elif [[ " $* " == *' ps --quiet api '* ]]; then echo fake-api
  elif [[ -f "$TEST_STATE/fail-up-once" ]]; then rm "$TEST_STATE/fail-up-once"; exit 1
  fi
  [[ ${NORDISKA_DATABASE:?} == "$(sed -n 's/^NORDISKA_DATABASE=//p' "$TEST_REPO/infra/v2/.env")" ]] || exit 1
  exit 0
fi
if [[ $1 == inspect ]]; then
  if [[ -f "$TEST_STATE/frontend-local" ]]; then
    echo "$TEST_REPO/infra/v2/docker-compose.yml,$TEST_REPO/infra/v2/docker-compose.frontend-local.yml"
  else echo "$TEST_REPO/infra/v2/docker-compose.yml,$TEST_REPO/infra/v2/docker-compose.override.yml"; fi
  exit 0
fi
[[ $1 == exec ]] || exit 1
if [[ " $* " == *' pg_dump '* ]]; then
  [[ ! -f "$TEST_STATE/fail-dump" ]] || exit 1
  echo '-- mock schema/reference dump'; exit 0
fi
if [[ " $* " == *' -f '* ]]; then exit 0; fi
statement=''
while (($#)); do
  if [[ $1 == -c ]]; then statement=$2; shift; fi
  shift
done
case "$statement" in
  *"datname = 'nordiska_synthetic'"*pg_stat_activity*) :;;
  *"datname = 'nordiska_synthetic'"*) [[ ! -f "$TEST_STATE/fail-query" ]] || exit 1; [[ ! -f "$TEST_STATE/exists" ]] || echo 1;;
  'DROP DATABASE'*) rm -f "$TEST_STATE/exists";;
  'CREATE DATABASE'*) touch "$TEST_STATE/exists";;
  *'NormalizedName'*) echo 17;;
  *'MAX("Id")'*'COALESCE'*) echo '3 4 223';;
  *'COUNT(*)'*) echo '3 customers | 4 accounts | 223 ledger entries | 1 MB';;
  COPY*) cat > /dev/null; [[ ! -f "$TEST_STATE/slow-copy" ]] || sleep 0.35; [[ ! -f "$TEST_STATE/fail-copy" ]] || exit 1;;
  '') cat > /dev/null; echo restored >> "$TEST_STATE/restores";;
  *) cat > /dev/null;;
esac
MOCK
chmod +x "$test_dir/bin/docker"
export PATH="$test_dir/bin:$PATH"
reset_fixture() {
  rm -f "$TEST_STATE"/*
  printf 'NORDISKA_DATABASE=nordiska_v2\nNORDISKA_API_PASSWORD=keep-this-value\n# keep this comment\n' > "$TEST_REPO/infra/v2/.env"
}
expect_failure() {
  if "$cli" "$@" > "$test_dir/output" 2>&1; then
    echo "Expected failure: $*" >&2; exit 1
  fi
}
reset_fixture
"$cli" --help > /dev/null
"$cli" --version | grep -q '1.0.0'
"$cli" --benchmark --customers 10 --threads 2 --stress-text > "$test_dir/output" 2>&1
grep -Eq '\[Customers\].*100\.0%.*10 / 10' "$test_dir/output"
grep -Eq '\[Ledger Tx\].*100\.0%.*[0-9]+ rows generated' "$test_dir/output"
! grep -Eq '\[Ledger Tx\].*/ *650' "$test_dir/output"
[[ ! -e "$TEST_STATE/calls" ]]
for bad in 0 -1 invalid; do expect_failure --customers "$bad"; done
expect_failure --year 1999 --customers 1
expect_failure --threads 0 --customers 1
expect_failure --threads 2x --customers 1
expect_failure --python --customers 1
expect_failure --append
expect_failure --switch unknown
[[ ! -e "$TEST_STATE/calls" ]]
echo 'PASS: native CLI, dry run and validation before Docker access'

expect_failure --switch nordiska_synthetic
grep -q '^NORDISKA_DATABASE=nordiska_v2$' "$TEST_REPO/infra/v2/.env"
touch "$TEST_STATE/exists" "$TEST_STATE/frontend-local"
NORDISKA_DATABASE=ignored-shell-override "$cli" --switch nordiska_synthetic > /dev/null
grep -q '^NORDISKA_DATABASE=nordiska_synthetic$' "$TEST_REPO/infra/v2/.env"
grep -q '^NORDISKA_API_PASSWORD=keep-this-value$' "$TEST_REPO/infra/v2/.env"
grep -q '^# keep this comment$' "$TEST_REPO/infra/v2/.env"
grep -q 'docker-compose.frontend-local.yml up .*api reporting-worker pdf-signer' "$TEST_STATE/calls"
touch "$TEST_STATE/fail-up-once"
expect_failure --switch nordiska_v2
grep -q '^NORDISKA_DATABASE=nordiska_synthetic$' "$TEST_REPO/infra/v2/.env"
grep -q 'previous services restored' "$test_dir/output"
echo 'PASS: V2 discovery, frontend workflow, .env preservation and switch rollback'

: > "$TEST_STATE/calls"
"$cli" --delete > /dev/null
up_line=$(grep -n 'up -d' "$TEST_STATE/calls" | head -1 | cut -d: -f1)
drop_line=$(grep -n 'DROP DATABASE' "$TEST_STATE/calls" | cut -d: -f1)
[[ "$up_line" -lt "$drop_line" ]]
[[ ! -e "$TEST_STATE/exists" ]]
touch "$TEST_STATE/fail-query"
expect_failure --status
rm "$TEST_STATE/fail-query"
touch "$TEST_STATE/fail-dump"
expect_failure --reset
[[ ! -e "$TEST_STATE/restores" ]]
echo 'PASS: deletion ordering and query/dump failure reporting'

reset_fixture
touch "$TEST_STATE/slow-copy"
"$cli" --customers 3 --threads 2 > "$test_dir/output" 2>&1 || { cat "$test_dir/output"; exit 1; }
grep -q '\[Saving customers\]' "$test_dir/output"
grep -q '\[Saving accounts/ledger\]' "$test_dir/output"
grep -Eq '\[Customers\].*100\.0%.*3 / 3' "$test_dir/output"
rm "$TEST_STATE/slow-copy"
[[ $(wc -l < "$TEST_STATE/restores") == 2 ]]
grep -q 'post-migration-permissions.sql' "$TEST_STATE/calls"
grep -q 'Configured V2 database' "$test_dir/output"
touch "$TEST_STATE/fail-copy"
expect_failure --customers 3 --threads 2
grep -q 'Native ingestion failed' "$test_dir/output"
echo 'PASS: native seeding orchestration and COPY failure reporting'
