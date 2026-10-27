#!/usr/bin/env bash
# Regression entry point: default CLI tests mock Docker; --native uses the compiler container.
set -euo pipefail
repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../../.." && pwd)
usage() {
  printf '%s\n' 'Usage: tools/syntheticDB/tests/run.sh [--cli|--native]' \
    '  --cli     CLI and terminal regressions with mocked Docker (default)' \
    '  --native  Identity, daily-interest and formatting tests in the pinned compiler container'
}
if (( $# > 1 )); then usage >&2; exit 2; fi
case "${1:---cli}" in
  --cli|--native) ;;
  --help|-h) usage; exit 0;;
  *) usage >&2; exit 2;;
esac
mkdir -p "${repo_root}/tmp"
export TMPDIR="${TMPDIR:-${repo_root}/tmp}"
test_dir=$(mktemp -d)
trap 'rm -rf -- "$test_dir"' EXIT
if [[ ${1:---cli} == --native ]]; then
  docker run --rm --platform linux/amd64 --network none \
    --user "$(id -u):$(id -g)" \
    -v "$repo_root/tools/syntheticDB:/src:ro" \
    -v "$test_dir:/test" \
    gcc@sha256:5e927c284bf55a7dc796262e311a0703344f62f41f5621eb56843111b1d37e15 \
    bash -c 'set -euo pipefail
      for test_name in identity interest ledger-format; do
        g++ -std=c++23 -O3 -I /src/include /src/src/generator.cpp \
          "/src/tests/native/$test_name.cpp" -o "/test/$test_name"
        "/test/$test_name"
        printf "PASS: %s\n" "$test_name"
      done'
  exit 0
fi
export SYNTHETIC_SESSION_MOCK="$repo_root/tools/syntheticDB/tests/helpers/psql-session-mock.py"
export TEST_REPO="$test_dir/repo" TEST_STATE="$test_dir/state"
mkdir -p "$TEST_REPO/infra/v2" "$TEST_REPO/tools/syntheticDB" "$TEST_STATE" "$test_dir/bin"
cp "$repo_root/tools/syntheticDB/nordiska-synthetic-linux-x86_64" "$TEST_REPO/tools/syntheticDB/manager"
cp "$repo_root/tools/syntheticDB/compose.override.yml" "$TEST_REPO/tools/syntheticDB/compose.override.yml"
cli="$TEST_REPO/tools/syntheticDB/manager"
touch "$TEST_REPO/infra/docker-compose.yml"
cat > "$test_dir/bin/docker" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
fixture_env="$TEST_REPO/infra/.env"
[[ -f "$fixture_env" ]] || fixture_env="$TEST_REPO/infra/v2/.env"
printf '%s\n' "$*" >> "$TEST_STATE/calls"
if [[ $1 == volume && $2 == ls ]]; then
  if [[ -f "$TEST_STATE/pdf-files" || -f "$TEST_STATE/synthetic-volume" ]]; then echo nordiska-v2_synthetic_report_documents; fi
  exit 0
fi
if [[ $1 == ps ]]; then
  if [[ " $* " == *'label=com.docker.compose.service=api'* ]]; then
    [[ ! -f "$TEST_STATE/legacy-api" ]] || echo fake-app
    exit 0
  fi
  [[ ! -f "$TEST_STATE/unknown-volume-user" ]] || echo foreign-container
  exit 0
fi
if [[ $1 == run ]]; then
  [[ " $* " == *'src=nordiska-v2_synthetic_report_documents,'* ]] || exit 1
  if [[ " $* " == *'rm -rf'* ]]; then
    [[ ! -f "$TEST_STATE/fail-pdf-cleanup" ]] || exit 1
    rm -f "$TEST_STATE/pdf-files"
  elif [[ -f "$TEST_STATE/pdf-files" ]]; then printf 1; else printf 0; fi
  exit 0
fi
if [[ $1 == stop ]]; then touch "$TEST_STATE/stopped-${@: -1}"; exit 0; fi
if [[ $1 == start ]]; then rm -f "$TEST_STATE/stopped-${@: -1}"; exit 0; fi
if [[ $1 == compose ]]; then
  if [[ " $* " == *' ps --quiet db '* ]]; then echo fake-db
  elif [[ " $* " == *' ps --quiet app '* ]]; then [[ -f "$TEST_STATE/no-app" || -f "$TEST_STATE/legacy-api" ]] || echo fake-app
  elif [[ " $* " == *' ps --quiet api '* ]]; then [[ ! -f "$TEST_STATE/legacy-api" ]] || echo fake-app
  elif [[ " $* " == *' ps --quiet reporting-worker '* ]]; then
    echo fake-worker
    [[ ! -f "$TEST_STATE/workers-two" ]] || echo fake-worker-2
  elif [[ " $* " == *' ps --quiet pdf-signer '* ]]; then echo fake-signer
  elif [[ -f "$TEST_STATE/fail-up-once" ]]; then rm "$TEST_STATE/fail-up-once"; exit 1
  elif [[ -f "$TEST_STATE/fail-up-always" ]]; then exit 1
  fi
  if [[ " $* " == *' up '* ]]; then
    rm -f "$TEST_STATE/runtime-normal" "$TEST_STATE/runtime-synthetic" "$TEST_STATE/no-app"
    printf '%s\n' "$NORDISKA_DATABASE" > "$TEST_STATE/running-database"
    touch "$TEST_STATE/recreated"
    [[ ! -f "$TEST_STATE/slow-up" ]] || sleep 0.6
  fi
  [[ ${NORDISKA_DATABASE:?} == "$(sed -n 's/^NORDISKA_DATABASE=//p' "$fixture_env")" ]] || exit 1
  exit 0
fi
if [[ $1 == inspect ]]; then
  if [[ " $* " == *'{{.Image}}'* ]]; then echo sha256:synthetic-test-image; exit 0; fi
  if [[ " $* " == *'{{.State.Running}}'* ]]; then
    [[ -f "$TEST_STATE/stopped-${@: -1}" ]] && echo false || echo true
    exit 0
  fi
  if [[ " $* " == *'.Mounts'* ]]; then
    selection=$(sed -n 's/^NORDISKA_DATABASE=//p' "$fixture_env")
    [[ ! -f "$TEST_STATE/running-database" ]] || selection=$(cat "$TEST_STATE/running-database")
    [[ ! -f "$TEST_STATE/runtime-normal" ]] || selection=nordiska_v2
    [[ ! -f "$TEST_STATE/runtime-synthetic" ]] || selection=nordiska_synthetic
    volume=nordiska-v2_report_documents
    [[ $selection != nordiska_synthetic ]] || volume=nordiska-v2_synthetic_report_documents
    [[ ! -f "$TEST_STATE/wrong-volume" ]] || volume=nordiska-v2_report_documents
    [[ ! -f "$TEST_STATE/normal-uses-synthetic-volume" ]] || volume=nordiska-v2_synthetic_report_documents
    writable=true
    [[ ${@: -1} != fake-app ]] || writable=false
    printf 'volume\t%s\t/var/lib/docker/volumes/%s/_data\t/var/lib/nordiska/report-documents\t%s\n' "$volume" "$volume" "$writable"
    exit 0
  fi
  if [[ " $* " == *'Config.Env'* ]]; then
    selection=$(sed -n 's/^NORDISKA_DATABASE=//p' "$fixture_env")
    [[ ! -f "$TEST_STATE/running-database" ]] || selection=$(cat "$TEST_STATE/running-database")
    [[ ! -f "$TEST_STATE/runtime-normal" ]] || selection=nordiska_v2
    [[ ! -f "$TEST_STATE/runtime-synthetic" ]] || selection=nordiska_synthetic
    [[ ! -f "$TEST_STATE/stale-runtime-once" || ! -f "$TEST_STATE/recreated" ]] || { rm "$TEST_STATE/stale-runtime-once"; selection=nordiska_v2; }
    echo "ConnectionStrings__ReportingDatabase=Host=db;Database=$selection;Password=never-print-this"
    if [[ -f "$TEST_STATE/production" ]]; then echo ASPNETCORE_ENVIRONMENT=Production;
    else echo ASPNETCORE_ENVIRONMENT=Development; fi
    if [[ -f "$TEST_STATE/dotnet-production" ]]; then echo DOTNET_ENVIRONMENT=Production; fi
    exit 0
  fi
  if [[ -f "$TEST_STATE/frontend-local" ]]; then
    echo "$TEST_REPO/infra/docker-compose.yml,$TEST_REPO/infra/docker-compose.frontend-local.yml"
  else echo "$TEST_REPO/infra/docker-compose.yml,$TEST_REPO/infra/docker-compose.override.yml"; fi
  exit 0
fi
[[ $1 == exec ]] || exit 1
if [[ " $* " == *' pg_dump '* ]]; then
  [[ ! -f "$TEST_STATE/fail-dump" ]] || exit 1
  echo '-- mock schema/reference dump'; exit 0
fi
if [[ " $* " == *' -f '* ]]; then exit 0; fi
statement=''
persistent_session=false
[[ " $* " != *' -Atq'* ]] || persistent_session=true
query_database=nordiska_v2
[[ " $* " != *' nordiska_synthetic '* ]] || query_database=nordiska_synthetic
while (($#)); do
  if [[ $1 == -c ]]; then statement=$2; shift; fi
  shift
done
case "$statement" in
  *"datname = 'nordiska_v2'"*) echo 1;;
  *"datname = 'nordiska_synthetic'"*pg_stat_activity*) :;;
  *"datname = 'nordiska_synthetic'"*) [[ ! -f "$TEST_STATE/fail-query" ]] || exit 1; [[ ! -f "$TEST_STATE/exists" ]] || echo 1;;
  'DROP DATABASE'*) rm -f "$TEST_STATE/exists";;
  'CREATE DATABASE'*) touch "$TEST_STATE/exists" "$TEST_STATE/empty" "$TEST_STATE/no-documents";;
  *'DELETE FROM reporting.generated_documents;'*)
    [[ $query_database == nordiska_synthetic ]] || exit 1
    [[ ! -f "$TEST_STATE/fail-document-sql" ]] || exit 1
    touch "$TEST_STATE/no-documents";;
  *'SELECT EXISTS'*'FROM banking.customers c'*)
    if [[ -f "$TEST_STATE/no-login-customer" ]]; then echo f;
    elif [[ $query_database == nordiska_synthetic ]]; then [[ -f "$TEST_STATE/empty" ]] && echo f || echo t;
    elif [[ $statement == *anna@example.com* ]]; then echo t; else echo f; fi;;
  *'ORDER BY random() LIMIT 1;'*)
    [[ ! -f "$TEST_STATE/no-login-customer" ]] || exit 0
    if [[ $query_database == nordiska_synthetic ]]; then
      if [[ -f "$TEST_STATE/unicode-customer" ]]; then name='Gösta Åström'; else name='Sara Lindberg'; fi
      printf 'Customer ID: 42\nName: %s\nEmail: cust42@nordiska-demo.se\nPassword: password123\nPersonal number: 198202116050\n' "$name";
    elif [[ $statement == *anna@example.com* ]]; then
      printf 'Customer ID: 1\nName: Anna Smith\nEmail: anna@example.com\nPassword: password123\nPersonal number: 198202116050\n'; fi;;
  *'NormalizedName'*) echo 17;;
  *'MAX("Id")'*'COALESCE'*) echo '3 4 223';;
  *'COUNT(*)'*'reporting.generated_documents'*)
    if [[ $query_database == nordiska_synthetic && -f "$TEST_STATE/empty" ]]; then echo '0|0|0|0|8 MB';
    elif [[ $query_database == nordiska_synthetic ]]; then
      if [[ -f "$TEST_STATE/no-documents" ]]; then echo '1003|1522|70722|0|24 MB'; else echo '1003|1522|70722|5|24 MB'; fi
    else echo '3|4|29|0|10 MB'; fi;;
  *'COUNT(*)'*) echo '3|4|223|0|1 MB';;
  '')
    if [[ $persistent_session == true ]]; then exec python3 "$SYNTHETIC_SESSION_MOCK"; fi
    cat > /dev/null; echo restored >> "$TEST_STATE/restores";;
  *) :;;
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
"$cli" --version | grep -q '1.3.0'
"$cli" --benchmark --customers 10 --threads 2 --stress-text > "$test_dir/output" 2>&1
grep -Eq '\[Benchmark\].*100\.0%.*10 / 10' "$test_dir/output"
grep -Eq 'Generated +10 customers, +[0-9,]+ accounts and +[0-9,]+ transactions' "$test_dir/output"
[[ ! -e "$TEST_STATE/calls" ]]
for bad in 0 -1 invalid; do expect_failure --customers "$bad"; done
expect_failure --customers 37620001
grep -q 'Personal-number capacity exhausted' "$test_dir/output"
expect_failure --benchmark --customers 37620001
grep -q 'Personal-number capacity exhausted' "$test_dir/output"
expect_failure --year 1999 --customers 1
expect_failure --threads 0 --customers 1
expect_failure --threads 2x --customers 1
expect_failure --python --customers 1
expect_failure --append
expect_failure --switch unknown
expect_failure --get-random-customer --reset
expect_failure --get-random-customer --switch nordiska_synthetic
expect_failure --get-random-customer --benchmark
[[ ! -e "$TEST_STATE/calls" ]]
echo 'PASS: native CLI, dry run and validation before Docker access'

expect_failure --switch nordiska_synthetic
grep -q '^NORDISKA_DATABASE=nordiska_v2$' "$TEST_REPO/infra/v2/.env"
touch "$TEST_STATE/exists" "$TEST_STATE/frontend-local" "$TEST_STATE/workers-two"
NORDISKA_DATABASE=ignored-shell-override "$cli" --switch nordiska_synthetic > /dev/null
grep -q '^NORDISKA_DATABASE=nordiska_synthetic$' "$TEST_REPO/infra/v2/.env"
grep -q '^NORDISKA_API_PASSWORD=keep-this-value$' "$TEST_REPO/infra/v2/.env"
grep -q '^# keep this comment$' "$TEST_REPO/infra/v2/.env"
grep -q 'docker-compose.frontend-local.yml .*up .*app reporting-worker pdf-signer' "$TEST_STATE/calls"
grep -q -- '--scale reporting-worker=2' "$TEST_STATE/calls"
cp "$TEST_REPO/infra/v2/.env" "$test_dir/before-switch"
touch "$TEST_STATE/fail-up-once"
expect_failure --switch nordiska_v2
grep -q '^NORDISKA_DATABASE=nordiska_synthetic$' "$TEST_REPO/infra/v2/.env"
grep -q 'previous services restored' "$test_dir/output"
cmp "$test_dir/before-switch" "$TEST_REPO/infra/v2/.env"
grep -q '^NORDISKA_REPORT_DOCUMENTS_VOLUME=nordiska-v2_synthetic_report_documents$' "$TEST_REPO/infra/v2/.env"
! grep -q never-print-this "$test_dir/output"
echo 'PASS: current Compose discovery, frontend workflow, .env preservation and switch rollback'

: > "$TEST_STATE/calls"
"$cli" --delete > /dev/null
up_line=$(grep -n 'up -d' "$TEST_STATE/calls" | head -1 | cut -d: -f1)
drop_line=$(grep -n 'DROP DATABASE' "$TEST_STATE/calls" | cut -d: -f1)
[[ "$up_line" -lt "$drop_line" ]]
[[ ! -e "$TEST_STATE/exists" ]]
touch "$TEST_STATE/fail-query"
expect_failure --status
rm "$TEST_STATE/fail-query"
: > "$TEST_STATE/calls"
touch "$TEST_STATE/exists" "$TEST_STATE/fail-dump"
expect_failure --reset
[[ -e "$TEST_STATE/exists" ]]
! grep -q "DROP DATABASE" "$TEST_STATE/calls"
[[ ! -e "$TEST_STATE/restores" ]]
echo 'PASS: deletion ordering and query/dump failure reporting'

reset_fixture
touch "$TEST_STATE/slow-copy"
"$cli" --customers 3 --threads 2 > "$test_dir/output" 2>&1 || { cat "$test_dir/output"; exit 1; }
grep -Eq '\[Customers ready\].*100\.0%.*3 / 3' "$test_dir/output"
rm "$TEST_STATE/slow-copy"
[[ $(wc -l < "$TEST_STATE/restores") == 2 ]]
grep -q 'post-migration-permissions.sql' "$TEST_STATE/calls"
grep -q 'App database NOW' "$test_dir/output"
touch "$TEST_STATE/fail-copy"
expect_failure --customers 3 --threads 2
grep -q 'Native ingestion failed' "$test_dir/output"
echo 'PASS: native seeding orchestration and COPY failure reporting'

reset_fixture
"$cli" --customers 1200 --threads 1 --seed 37619998 > "$test_dir/output" 2>&1
cut -f1,2 "$TEST_STATE/customer-rows" | sort -n > "$test_dir/identities-one-thread"
! grep -q 'SELECT "PersonalNum" FROM banking.customers' "$TEST_STATE/calls"
reset_fixture
"$cli" --customers 1200 --threads 4 --seed 37619998 > "$test_dir/output" 2>&1
cut -f1,2 "$TEST_STATE/customer-rows" | sort -n > "$test_dir/identities-four-threads"
cmp "$test_dir/identities-one-thread" "$test_dir/identities-four-threads"
python3 - "$test_dir/identities-four-threads" <<'PYTEST'
from pathlib import Path
import sys
values = [line.split('\t')[1] for line in Path(sys.argv[1]).read_text().splitlines()]
assert len(values) == len(set(values)) == 1200
assert [value[:11] for value in values[:4]] == ['20071231998', '20071231999', '19050101000', '19050101001']
PYTEST
echo 'PASS: deterministic identities across threads and scheduling blocks, wraparound and no creation identity query'

reset_fixture
touch "$TEST_STATE/slow-second-batch"
"$cli" --customers 2750 --threads 2 > "$test_dir/output" 2>&1
[[ $(grep -c -- ' -Atq' "$TEST_STATE/calls") == 1 ]]
python3 - "$TEST_STATE/batch-events" "$test_dir/output" <<'PYTEST'
from pathlib import Path
import re, sys
events = Path(sys.argv[1]).read_text().splitlines()
groups = []
current = {}
for event in events:
    if event == 'sequences': continue
    if event == 'commit':
        groups.append(current); current = {}; continue
    table, count = event.split()
    current[table] = int(count)
assert len(groups) >= 3, groups
assert groups[0]['customers'] == 500, groups
assert sum(g['customers'] for g in groups) == 2750
for group in groups:
    assert group['roles'] == group['customers']
    assert group['accounts'] >= group['customers']
    assert group['ledger'] > group['accounts']
# Sustained commits are based on variable ledger workload, never 1k cohorts.
assert all(g['customers'] != 1000 for g in groups[1:]), groups
assert events.count('sequences') == 2, events  # initial sample and finalization
text = re.sub(r'\x1b\[[0-?]*[ -/]*[@-~]', '', Path(sys.argv[2]).read_text())
assert re.search(r'500 / 2,750 .*ETA:\s*\d', text), text
assert re.search(r'100\.0%.*2,750 / 2,750.*ETA:\s*0s', text)
rates = [int(rate.replace(',', '')) for rate in re.findall(r'/ 2,750 \([ ]*([0-9,]+) customers/s\)', text)]
positive = [rate for rate in rates if rate]
assert min(positive) < positive[0], positive  # rolling sampling observes stall before commit
print('PASS: initial 500, sustained workload-budget commits and independently sampled slowdown ETA')
PYTEST
reset_fixture
touch "$TEST_STATE/fail-sequences"
expect_failure --customers 500 --threads 2
grep -q 'Sequence reset failed' "$test_dir/output"
! grep -q '100.0%' "$test_dir/output"
echo 'PASS: failed finalization never marks customers ready or claims completion'

reset_fixture
touch "$TEST_STATE/fail-final-sequences"
expect_failure --customers 600 --threads 2
grep -q '600 / 600' "$test_dir/output"
grep -q 'Sequence reset failed' "$test_dir/output"
! grep -q '100.0%' "$test_dir/output"
! grep -q '| Complete' "$test_dir/output"
echo 'PASS: committed totals remain visible after finalization failure without claiming Complete/100%'

reset_fixture
expect_failure --keep-indexes
"$cli" --customers 10000 --threads 2 --keep-indexes > "$test_dir/output" 2>&1
[[ ! -f "$TEST_STATE/index-discovery" ]]
reset_fixture
"$cli" --customers 10000 --threads 2 > "$test_dir/output" 2>&1
[[ -f "$TEST_STATE/index-discovery" ]]
reset_fixture
"$cli" --customers 10000 --threads 2 --append > "$test_dir/output" 2>&1
[[ ! -f "$TEST_STATE/index-discovery" ]]
echo 'PASS: bulk creation discovers query indexes; --keep-indexes and append retain them'




reset_fixture
touch "$TEST_STATE/exists" "$TEST_STATE/stale-runtime-once"
expect_failure --switch nordiska_synthetic
[[ $(sed -n 's/^NORDISKA_DATABASE=//p' "$TEST_REPO/infra/v2/.env") == nordiska_v2 ]]
grep -q 'previous services restored' "$test_dir/output"
echo 'PASS: running-container mismatch triggers switch rollback'

reset_fixture
printf 'NORDISKA_DATABASE=nordiska_synthetic\n' > "$TEST_REPO/infra/v2/.env"
touch "$TEST_STATE/exists" "$TEST_STATE/runtime-normal"
cp "$TEST_REPO/infra/v2/.env" "$test_dir/before-status"
"$cli" --status > "$test_dir/output"
grep -q 'App database NOW: NORMAL (nordiska_v2)' "$test_dir/output"
grep -q 'Saved database selection.*SYNTHETIC.*NOT APPLIED' "$test_dir/output"
grep -q 'Saved selection and running services are not aligned' "$test_dir/output"
grep -q 'app: NORMAL.*PDF volume: nordiska-v2_report_documents (read-only)' "$test_dir/output"
grep -q 'PDFs saved inside container: /var/lib/nordiska/report-documents' "$test_dir/output"
grep -q 'PDF directory on Docker host: /var/lib/docker/volumes/nordiska-v2_report_documents/_data' "$test_dir/output"
grep -q 'NORMAL DB (nordiska_v2)' "$test_dir/output"
grep -q 'SYNTHETIC DB (nordiska_synthetic)' "$test_dir/output"
grep -q 'Customers: 3 | Accounts: 4 | Transactions: 29 | Documents: 0 | Size: 10 MB' "$test_dir/output"
grep -q 'Customers: 1,003 | Accounts: 1,522 | Transactions: 70,722 | Documents: 5 | Size: 24 MB' "$test_dir/output"
! grep -q never-print-this "$test_dir/output"
! grep -q ' up ' "$TEST_STATE/calls"
cmp "$test_dir/before-status" "$TEST_REPO/infra/v2/.env"
printf '5\n' | "$cli" --menu > "$test_dir/output"
grep -q 'Active DB: NORMAL' "$test_dir/output"
grep -q 'Switch to SYNTHETIC DB' "$test_dir/output"
! grep -q 'Saved database selection' "$test_dir/output"
! grep -q 'PDF directory' "$test_dir/output"
rm "$TEST_STATE/runtime-normal"
touch "$TEST_STATE/wrong-volume"
"$cli" --status > "$test_dir/output"
grep -q 'App database NOW: SYNTHETIC (nordiska_synthetic)' "$test_dir/output"
grep -q 'Running PDF volume differs' "$test_dir/output"
echo 'PASS: actual app database, pending selection, actual PDF mounts and read-only status'

reset_fixture
printf '3\n' | "$cli" --menu > "$test_dir/output"
grep -q 'Create synthetic data' "$test_dir/output"
! grep -q 'Switch to SYNTHETIC DB' "$test_dir/output"
! grep -q 'Empty synthetic database' "$test_dir/output"
! grep -q 'Delete synthetic DB' "$test_dir/output"
grep -q 'Show random customer login' "$test_dir/output"
! grep -qi append "$test_dir/output"
! grep -q 'DROP DATABASE' "$TEST_STATE/calls"
printf '1\n8\n0\nabc\n50\n2\n4\n' | "$cli" --menu > "$test_dir/output"
grep -q 'OK: created 50 synthetic customers' "$test_dir/output"
grep -q 'Enter a whole number from 1 to 37,620,000' "$test_dir/output"
! grep -q 'CPU threads' "$test_dir/output"
! grep -q 'Reporting year' "$test_dir/output"
# Creation refreshes to the existing-database menu; exit without another DROP.
! grep -q 'overwrites existing' "$test_dir/output"
[[ $(grep -c 'DROP DATABASE' "$TEST_STATE/calls") == 1 ]]
"$cli" --menu < /dev/null > "$test_dir/output"
grep -q 'Cancelled' "$test_dir/output"
echo 'PASS: missing-database menu, validated custom count, no advanced prompts or append, and EOF'

reset_fixture
printf '1\n1\n2\n4\n' | "$cli" --menu > "$test_dir/output"
grep -q 'OK: created 100 synthetic customers' "$test_dir/output"
! grep -q 'Custom amount:' "$test_dir/output"
echo 'PASS: customer-count preset opens settings and starts with the default seed'

reset_fixture
printf '1\n1\n1\n-1\n9223372036854775808\n31415\n2\n4\n' | "$cli" --menu > "$test_dir/output"
grep -q 'Enter a whole number from 0 to' "$test_dir/output"
grep -q 'Seed: 31415' "$test_dir/output"
grep -q 'OK: created 100 synthetic customers (seed 31415)' "$test_dir/output"
echo 'PASS: seed editing validates range and uses the chosen seed for creation'

reset_fixture
touch "$TEST_STATE/exists" "$TEST_STATE/empty"
printf '4\n' | "$cli" --menu > "$test_dir/output"
grep -q 'Switch to SYNTHETIC DB' "$test_dir/output"
! grep -q 'Empty synthetic database' "$test_dir/output"
grep -q 'Show random customer login' "$test_dir/output"
! grep -q 'Create synthetic data' "$test_dir/output"
grep -q 'Delete synthetic DB and return to NORMAL' "$test_dir/output"
echo 'PASS: existing database with zero customers offers switching and deletion, not creation'

reset_fixture
touch "$TEST_STATE/exists"
printf '1\n5\n' | "$cli" --menu > "$test_dir/output"
grep -q 'Switching DB: NORMAL -> SYNTHETIC' "$test_dir/output"
grep -q 'Active DB: SYNTHETIC' "$test_dir/output"
grep -q 'Switch back to NORMAL DB' "$test_dir/output"
echo 'PASS: one-step switch and refreshed direction'

reset_fixture
touch "$TEST_STATE/exists" "$TEST_STATE/fail-up-once"
printf '1\n5\n' | "$cli" --menu > "$test_dir/output"
grep -q 'Error: Database switch failed' "$test_dir/output"
! grep -q 'OK: switched' "$test_dir/output"
grep -q 'Active DB: NORMAL' "$test_dir/output"
echo 'PASS: failed switch never displays success'

reset_fixture
printf 'NORDISKA_DATABASE=nordiska_synthetic\n' > "$TEST_REPO/infra/v2/.env"
touch "$TEST_STATE/exists" "$TEST_STATE/runtime-normal" "$TEST_STATE/fail-up-once"
cp "$TEST_REPO/infra/v2/.env" "$test_dir/before-stale-switch"
printf '1\n5\n' | "$cli" --menu > "$test_dir/output"
grep -q 'Error: Database switch failed' "$test_dir/output"
! grep -q 'OK: switched' "$test_dir/output"
[[ $(cat "$TEST_STATE/running-database") == nordiska_v2 ]]
cmp "$test_dir/before-stale-switch" "$TEST_REPO/infra/v2/.env"
echo 'PASS: stale-selection rollback restores the actual previous service DB and saved file'


reset_fixture
touch "$TEST_STATE/exists"
cp "$TEST_REPO/infra/v2/.env" "$test_dir/before-confirm"
printf '2\nn\n5\n' | "$cli" --menu > "$test_dir/output"
grep -q 'Delete synthetic DB and return to NORMAL?' "$test_dir/output"
! grep -q 'Create synthetic data' "$test_dir/output"
! grep -q 'Empty synthetic database' "$test_dir/output"
! grep -q 'DROP DATABASE' "$TEST_STATE/calls"
cmp "$test_dir/before-confirm" "$TEST_REPO/infra/v2/.env"
touch "$TEST_STATE/runtime-synthetic"
printf '2\ny\n3\n' | "$cli" --menu > "$test_dir/output"
grep -q 'OK: synthetic DB deleted; NORMAL selected' "$test_dir/output"
grep -q 'Active DB: NORMAL' "$test_dir/output"
grep -q 'Create synthetic data' "$test_dir/output"
[[ ! -f "$TEST_STATE/exists" ]]
[[ $(cat "$TEST_STATE/running-database") == nordiska_v2 ]]
up_line=$(grep -n 'up -d' "$TEST_STATE/calls" | head -1 | cut -d: -f1)
drop_line=$(grep -n 'DROP DATABASE' "$TEST_STATE/calls" | cut -d: -f1)
[[ "$up_line" -lt "$drop_line" ]]
echo 'PASS: deletion confirmation, return to normal before DROP, and creation reappears after deletion'

reset_fixture
touch "$TEST_STATE/exists" "$TEST_STATE/runtime-synthetic" "$TEST_STATE/fail-up-once"
printf '2\ny\n5\n' | "$cli" --menu > "$test_dir/output"
grep -q 'Error: Database switch failed' "$test_dir/output"
! grep -q 'OK: synthetic DB deleted' "$test_dir/output"
! grep -q 'DROP DATABASE' "$TEST_STATE/calls"
[[ -f "$TEST_STATE/exists" ]]
echo 'PASS: failed return to normal prevents database deletion'

reset_fixture
touch "$TEST_STATE/exists" "$TEST_STATE/runtime-synthetic"
# .env says NORMAL but a service is actually on SYNTHETIC: switch before DROP.
: > "$TEST_STATE/calls"
"$cli" --delete > /dev/null
up_line=$(grep -n 'up -d' "$TEST_STATE/calls" | head -1 | cut -d: -f1)
drop_line=$(grep -n 'DROP DATABASE' "$TEST_STATE/calls" | cut -d: -f1)
[[ "$up_line" -lt "$drop_line" ]]
echo 'PASS: deletion checks running services even with stale normal selection'

reset_fixture
touch "$TEST_STATE/exists"
python3 "$repo_root/tools/syntheticDB/tests/menu-pty.py" "$cli"

reset_fixture
cp "$TEST_REPO/infra/v2/.env" "$test_dir/before-login"
"$cli" --get-random-customer > "$test_dir/output"
grep -q 'Random customer login from NORMAL DB' "$test_dir/output"
grep -q 'Email: anna@example.com' "$test_dir/output"
grep -q 'Password: password123' "$test_dir/output"
# Saved selection can be stale: the running app wins in either direction.
printf 'NORDISKA_DATABASE=nordiska_synthetic\n' > "$TEST_REPO/infra/v2/.env"
touch "$TEST_STATE/runtime-normal"
"$cli" --get-random-customer > "$test_dir/output"
grep -q 'Random customer login from NORMAL DB' "$test_dir/output"
grep -q 'Email: anna@example.com' "$test_dir/output"
cp "$test_dir/before-login" "$TEST_REPO/infra/v2/.env"
rm "$TEST_STATE/runtime-normal"
touch "$TEST_STATE/exists" "$TEST_STATE/runtime-synthetic"
"$cli" --get-random-customer > "$test_dir/output"
grep -q 'Random customer login from SYNTHETIC DB' "$test_dir/output"
grep -q 'Name: Sara Lindberg' "$test_dir/output"
grep -q 'Email: cust42@nordiska-demo.se' "$test_dir/output"
grep -q 'Password: password123' "$test_dir/output"
! grep -q 'Switch to nordiska_synthetic before' "$test_dir/output"
cmp "$test_dir/before-login" "$TEST_REPO/infra/v2/.env"
! grep -q ' up ' "$TEST_STATE/calls"
! grep -q 'DROP DATABASE' "$TEST_STATE/calls"
! grep -q never-print-this "$test_dir/output"
printf '4\n\n5\n' | "$cli" --menu > "$test_dir/output"
grep -q 'Email: cust42@nordiska-demo.se' "$test_dir/output"
touch "$TEST_STATE/no-login-customer"
expect_failure --get-random-customer
grep -q 'No customer with known default login credentials' "$test_dir/output"
rm "$TEST_STATE/no-login-customer" "$TEST_STATE/runtime-synthetic"
touch "$TEST_STATE/production"
expect_failure --get-random-customer
rm "$TEST_STATE/production"
touch "$TEST_STATE/dotnet-production"
expect_failure --get-random-customer
rm "$TEST_STATE/dotnet-production"
touch "$TEST_STATE/no-app"
expect_failure --get-random-customer
grep -q 'App is not running' "$test_dir/output"
echo 'PASS: active DB login, stale selections, demo environment checks, offline app and read-only behavior'


reset_fixture
touch "$TEST_STATE/exists" "$TEST_STATE/pdf-files" "$TEST_STATE/runtime-synthetic" "$TEST_STATE/workers-two"
cp "$TEST_REPO/infra/v2/.env" "$test_dir/before-document-cleanup"
"$cli" --delete-documents > "$test_dir/output"
grep -q 'Synthetic PDFs, document metadata and PDF jobs deleted' "$test_dir/output"
[[ -f "$TEST_STATE/exists" && -f "$TEST_STATE/no-documents" && ! -f "$TEST_STATE/pdf-files" ]]
[[ $(grep -c '^stop --time 30' "$TEST_STATE/calls") == 4 ]]
[[ $(grep -c '^start ' "$TEST_STATE/calls") == 4 ]]
for marker in "$TEST_STATE"/stopped-*; do [[ ! -e "$marker" ]]; done
cmp "$test_dir/before-document-cleanup" "$TEST_REPO/infra/v2/.env"
! grep -q 'DROP DATABASE' "$TEST_STATE/calls"
! grep -q 'DELETE FROM banking' "$TEST_STATE/calls"
! grep -q 'src=nordiska-v2_report_documents,' "$TEST_STATE/calls"
echo 'PASS: document cleanup stops/restores synthetic replicas, keeps configuration and never targets normal data'

for failure in fail-document-sql fail-pdf-cleanup; do
  reset_fixture
  touch "$TEST_STATE/exists" "$TEST_STATE/pdf-files" "$TEST_STATE/runtime-synthetic" "$TEST_STATE/$failure"
  expect_failure --delete-documents
  [[ -f "$TEST_STATE/pdf-files" ]]
  for marker in "$TEST_STATE"/stopped-*; do [[ ! -e "$marker" ]]; done
  rm "$TEST_STATE/$failure"
  "$cli" --delete-documents > /dev/null
  [[ ! -f "$TEST_STATE/pdf-files" ]]
done
echo 'PASS: cleanup failures restore containers and permit safe retry after partial metadata cleanup'

for unsafe in normal-uses-synthetic-volume unknown-volume-user; do
  reset_fixture
  touch "$TEST_STATE/exists" "$TEST_STATE/pdf-files" "$TEST_STATE/$unsafe"
  expect_failure --delete-documents
  [[ ! -f "$TEST_STATE/no-documents" && -f "$TEST_STATE/pdf-files" ]]
  ! grep -q '^stop ' "$TEST_STATE/calls"
done
echo 'PASS: cleanup refuses normal services or unknown containers using synthetic storage'

reset_fixture
touch "$TEST_STATE/pdf-files"
"$cli" --delete-documents > /dev/null
[[ ! -f "$TEST_STATE/pdf-files" ]]
! grep -q '^stop ' "$TEST_STATE/calls"
! grep -q 'DELETE FROM reporting' "$TEST_STATE/calls"
expect_failure --delete-documents --delete
expect_failure --delete-documents --switch nordiska_synthetic
expect_failure --delete-documents --benchmark
! grep -q 'DROP DATABASE' "$TEST_STATE/calls"
echo 'PASS: orphan-only cleanup leaves normal services running and rejects ambiguous CLI actions'

reset_fixture
touch "$TEST_STATE/exists" "$TEST_STATE/frontend-local" "$TEST_STATE/legacy-api"
"$cli" --switch nordiska_synthetic > /dev/null
grep -q 'up .*api reporting-worker pdf-signer' "$TEST_STATE/calls"
echo 'PASS: develop frontend-local api service is discovered and recreated'

reset_fixture
cp "$TEST_REPO/infra/v2/.env" "$TEST_REPO/infra/.env"
touch "$TEST_STATE/exists"
"$cli" --switch nordiska_synthetic > /dev/null
grep -q '^NORDISKA_DATABASE=nordiska_synthetic$' "$TEST_REPO/infra/.env"
grep -q '^NORDISKA_DATABASE=nordiska_v2$' "$TEST_REPO/infra/v2/.env"
echo 'PASS: develop infra/.env takes precedence over the legacy environment file'
