#!/usr/bin/env bash
# Requires the prepared V2 db. Reads its schema/reference data only; all writes use an isolated container.
set -euo pipefail
repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../../.." && pwd)
real_docker=$(command -v docker)
test_dir=$(mktemp -d)
container="nordiska-synthetic-release-test-$$"
app_container="nordiska-synthetic-login-test-$$"
network="nordiska-synthetic-login-network-$$"
cleanup() {
  "$real_docker" rm -f "$app_container" > /dev/null 2>&1 || true
  "$real_docker" rm -f "$container" > /dev/null 2>&1 || true
  "$real_docker" network rm "$network" > /dev/null 2>&1 || true
  rm -rf -- "$test_dir"
}
trap cleanup EXIT
source_db=$("$real_docker" ps --filter label=com.docker.compose.project=nordiska-v2 \
  --filter label=com.docker.compose.service=db --format '{{.ID}}')
[[ -n "$source_db" && "$source_db" != *$'\n'* ]] || { echo 'Expected one prepared V2 database' >&2; exit 1; }
"$real_docker" exec "$source_db" pg_dump -s -U nordiska_bootstrap nordiska_v2 > "$test_dir/schema.sql"
"$real_docker" exec "$source_db" pg_dump -a -U nordiska_bootstrap \
  -t banking.account_type_configs -t 'banking."AspNetRoles"' nordiska_v2 > "$test_dir/reference.sql"
"$real_docker" run -d --name "$container" \
  -e POSTGRES_USER=nordiska_bootstrap -e POSTGRES_PASSWORD=isolated-test-password -e POSTGRES_DB=nordiska_v2 \
  -e NORDISKA_MIGRATOR_PASSWORD=isolated-migrator-password \
  -e NORDISKA_API_PASSWORD=isolated-api-password \
  -e NORDISKA_REPORTING_WORKER_PASSWORD=isolated-worker-password \
  -v "$repo_root/infra/v2/postgres/init:/docker-entrypoint-initdb.d:ro" \
  -v "$repo_root/infra/v2/postgres/permissions/post-migration-permissions.development.sql:/opt/nordiska/post-migration-permissions.sql:ro" \
  postgres:15-bookworm > /dev/null
sql() { "$real_docker" exec "$container" psql -h 127.0.0.1 -U nordiska_bootstrap -d "$1" -v ON_ERROR_STOP=1 -At -c "$2"; }
ready=false
for ((attempt=0; attempt<60; attempt++)); do
  if [[ $(sql nordiska_v2 "SELECT 1 FROM pg_roles WHERE rolname='nordiska_reporting_worker';" 2>/dev/null || true) == 1 ]]; then ready=true; break; fi
  sleep 1
done
[[ $ready == true ]] || { echo 'Test database did not initialize' >&2; exit 1; }
sql nordiska_v2 'DROP SCHEMA banking,faq,reporting,inbox CASCADE;' > /dev/null
"$real_docker" exec -i "$container" psql -U nordiska_bootstrap -d nordiska_v2 -v ON_ERROR_STOP=1 -q < "$test_dir/schema.sql"
"$real_docker" exec -i "$container" psql -U nordiska_bootstrap -d nordiska_v2 -v ON_ERROR_STOP=1 -q < "$test_dir/reference.sql"
sql nordiska_v2 'UPDATE banking."AspNetRoles" SET "Id"=17 WHERE "NormalizedName"='"'CUSTOMER';" > /dev/null

# The copied release resolves this disposable checkout. Compose discovery is mocked;
# its psql/pg_dump/COPY commands run against the real isolated PostgreSQL container.
mkdir -p "$test_dir/repo/infra/v2" "$test_dir/repo/tools/synthetic" "$test_dir/bin"
cp "$repo_root/tools/synthetic/nordiska-synthetic-linux-x86_64" "$test_dir/repo/tools/synthetic/manager"
touch "$test_dir/repo/infra/docker-compose.yml"
printf 'NORDISKA_DATABASE=nordiska_v2\n' > "$test_dir/repo/infra/v2/.env"
export SYNTHETIC_TEST_PROXY="$repo_root/tools/synthetic/tests/helpers/psql-session-proxy.py"
export SYNTHETIC_TEST_DOCKER="$real_docker" SYNTHETIC_TEST_CONTAINER="$container"
export SYNTHETIC_TEST_BATCH_LOG="$test_dir/complete-batches.log"
export SYNTHETIC_TEST_INDEX_LOG="$test_dir/committed-indexes.log"
cat > "$test_dir/bin/docker" <<'PROXY'
#!/usr/bin/env bash
set -euo pipefail
if [[ $1 == compose ]]; then
  if [[ " $* " == *' ps --quiet db '* ]]; then echo "$SYNTHETIC_TEST_CONTAINER";
  elif [[ " $* " == *' ps --quiet '* ]]; then echo synthetic-service-mock; fi
  exit 0
fi
if [[ $1 == inspect && ${@: -1} == synthetic-service-mock ]]; then
  if [[ " $* " == *'.Mounts'* ]]; then
    value=$(sed -n 's/^NORDISKA_DATABASE=//p' "${BASH_SOURCE[0]%/bin/docker}/repo/infra/v2/.env")
    volume=nordiska-v2_report_documents
    [[ $value != nordiska_synthetic ]] || volume=nordiska-v2_synthetic_report_documents
    printf 'volume\t%s\t/isolated/%s\t/var/lib/nordiska/report-documents\ttrue\n' "$volume" "$volume"
  fi
  if [[ " $* " == *'Config.Env'* ]]; then
    value=$(sed -n 's/^NORDISKA_DATABASE=//p' "${BASH_SOURCE[0]%/bin/docker}/repo/infra/v2/.env")
    echo "ConnectionStrings__ReportingDatabase=Host=db;Database=$value;Password=isolated"
    echo "ASPNETCORE_ENVIRONMENT=${SYNTHETIC_TEST_ENVIRONMENT:-Development}"
  fi
  exit 0
fi
# The single persistent psql process is observed at real commit acknowledgements.
if [[ $1 == exec && " $* " == *' -Atq'* ]]; then
  exec python3 "$SYNTHETIC_TEST_PROXY" "$@"
fi
exec "$SYNTHETIC_TEST_DOCKER" "$@"
PROXY
chmod +x "$test_dir/bin/docker"
cli="$test_dir/repo/tools/synthetic/manager"
PATH="$test_dir/bin:$PATH" "$cli" --customers 10000 --threads 4 --year 2025 --seed 42 > "$test_dir/seed.log" 2>&1 || { cat "$test_dir/seed.log"; exit 1; }
python3 - "$test_dir/seed.log" "$test_dir/complete-batches.log" "$test_dir/committed-indexes.log" <<'PYTEST'
from pathlib import Path
import re
import sys
text = re.sub(r'\x1b\[[0-?]*[ -/]*[@-~]', '', Path(sys.argv[1]).read_text()).replace('\r', '\n')
frames = [line for line in text.splitlines() if line.startswith('[Customers ready]') and re.search(r'ETA:\s*[0-9]', line)]
assert frames, 'Creation never produced a total completion estimate'
assert any('500 / 10,000' in line and '9,500' not in line for line in frames), frames
assert any('100.0%' in line and 'ETA: 0s' in line for line in frames), frames[-1:]
batches = [list(map(int, line.split('|'))) for line in Path(sys.argv[2]).read_text().splitlines()]
assert batches[0][0] == 0 and batches[1][0] == 500 and batches[-1][0] == 10000, batches
assert all(a[0] < b[0] for a, b in zip(batches[1:-2], batches[2:-1])), batches
assert len(batches) > 3, batches
assert all(b[0] - a[0] != 1000 for a, b in zip(batches[1:-2], batches[2:-1])), batches
for customers, roles, accounts, transactions in batches[1:]:
    assert roles == customers and accounts >= customers and transactions > accounts, batches
indexes = [int(line) for line in Path(sys.argv[3]).read_text().splitlines()]
assert indexes[:2] == [5, 5] and indexes[-1] == 5, indexes
assert all(count == 1 for count in indexes[2:-1]), indexes
print('PASS: initial sample keeps all indexes, bulk load retains PK, finalization restores all query indexes')
print('PASS: real committed first 500 customers, roles, accounts and transactions, then sustained workload-budget commits with total ETA')
print(next(line for line in frames if '500 / 10,000' in line))
PYTEST
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.customers;') == 10000 ]]
[[ $(sql nordiska_synthetic "SELECT md5(string_agg(pg_get_indexdef(indexrelid),'|' ORDER BY indexrelid::regclass::text)) FROM pg_index WHERE indrelid='banking.ledger_entries'::regclass;") == $(sql nordiska_v2 "SELECT md5(string_agg(pg_get_indexdef(indexrelid),'|' ORDER BY indexrelid::regclass::text)) FROM pg_index WHERE indrelid='banking.ledger_entries'::regclass;") ]]
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.savings_accounts;') == 15436 ]]
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.ledger_entries;') == 733920 ]]
check_interest() {
  local result
  result=$("$real_docker" exec -i "$container" psql -X -U nordiska_bootstrap -d nordiska_synthetic \
    -At -v ON_ERROR_STOP=1 -v fixture_year="$1" < "$repo_root/tools/synthetic/tests/helpers/verify-interest.sql")
  [[ $result == 0 ]] || { echo "Daily interest or tax disagrees with the independent SQL reference: $result months" >&2; exit 1; }
  [[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.ledger_entries WHERE "Type" IN ('"'interest','tax'"') AND "CreatedAt"::date<>(date_trunc('"'month'"',"CreatedAt")+interval '"'1 month - 1 day'"')::date;') == 0 ]]
  [[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.ledger_entries WHERE "Type"='"'interest'"';') == $((12 * $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.savings_accounts;'))) ]]
}
check_interest 2025
echo 'PASS: monthly payouts and tax agree with daily balances and actual calendar month ends'

[[ $(sql nordiska_synthetic "SELECT COUNT(*) FROM pg_stat_all_tables WHERE schemaname='banking' AND relname IN ('customers','AspNetUserRoles','savings_accounts','ledger_entries') AND last_analyze IS NOT NULL;") == 4 ]]
[[ $(sql nordiska_synthetic 'SELECT COUNT(DISTINCT "Name") FROM banking.customers;') -gt 100 ]]
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking."AspNetUserRoles" WHERE "RoleId"=17;') == 10000 ]]
PATH="$test_dir/bin:$PATH" "$cli" --switch nordiska_synthetic > /dev/null
grep -q '^NORDISKA_REPORT_DOCUMENTS_VOLUME=nordiska-v2_synthetic_report_documents$' "$test_dir/repo/infra/v2/.env"
PATH="$test_dir/bin:$PATH" "$cli" --customers 3 --threads 2 --year 2025 --seed 42 --append --stress-text > "$test_dir/append.log" 2>&1 || { cat "$test_dir/append.log"; exit 1; }
grep -q '^NORDISKA_DATABASE=nordiska_v2$' "$test_dir/repo/infra/v2/.env"
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.customers;') == 10003 ]]
[[ $(sql nordiska_synthetic 'SELECT COUNT(*)-COUNT(DISTINCT "PersonalNum") FROM banking.customers;') == 0 ]]
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.savings_accounts a WHERE ABS(a."Balance"-(SELECT SUM("Amount") FROM banking.ledger_entries WHERE "AccountId"=a."Id"))>0.001;') == 0 ]]
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.ledger_entries WHERE LENGTH("Label")>160;') -gt 0 ]]
[[ $(sql nordiska_synthetic "SELECT has_table_privilege('nordiska_api','banking.customers','INSERT') AND has_table_privilege('nordiska_reporting_worker','reporting.account_statement_jobs','UPDATE') AND NOT has_table_privilege('nordiska_reporting_worker','banking.customers','SELECT');") == t ]]
[[ $(sql nordiska_v2 'SELECT COUNT(*) FROM banking.customers;') == 0 ]]
counts=$(sql nordiska_synthetic 'SELECT (SELECT COUNT(*) FROM banking.customers)||'"' customers | '"'||(SELECT COUNT(*) FROM banking.savings_accounts)||'"' accounts | '"'||(SELECT COUNT(*) FROM banking.ledger_entries)||'"' ledger entries';")
sql nordiska_synthetic "INSERT INTO reporting.generated_documents (\"DocumentType\", \"ContentType\", \"FileName\", \"StorageKey\", \"ByteLength\", \"Sha256Hash\", \"CreatedAt\") VALUES ('AccountStatement', 'application/pdf', 'status-test.pdf', 'isolated/status-test.pdf', 5, repeat('a',64), NOW());" > /dev/null
PATH="$test_dir/bin:$PATH" "$cli" --status > "$test_dir/status.log"
grep -q 'Customers: 10,003 | Accounts: .* | Transactions: .* | Documents: 1' "$test_dir/status.log"
grep -q 'Customers: 0 | Accounts: 0 | Transactions: 0 | Documents: 0' "$test_dir/status.log"
# These demo records are inserted only in the disposable normal database.
"$real_docker" exec -i "$container" psql -U nordiska_bootstrap -d nordiska_v2 -v ON_ERROR_STOP=1 -q <<'SQL'
INSERT INTO banking.customers ("Id", "PersonalNum", "Name", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
  "EmailConfirmed", "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount", "CreatedAt")
VALUES (1, '198202116050', 'Anna Smith', 'anna@example.com', 'ANNA@EXAMPLE.COM', 'anna@example.com', 'ANNA@EXAMPLE.COM',
  false, false, false, true, 0, NOW());
INSERT INTO banking.savings_accounts ("CustomerId", "AccountNumber", "AccountName", "AccountType", "Balance", "InterestRate", "Status", "CreatedAt")
VALUES (1, 'isolated-demo-account', 'Demo savings', 'flex', 0, 0.035, 'active', NOW());
SQL
PATH="$test_dir/bin:$PATH" "$cli" --get-random-customer > "$test_dir/normal-login.log"
grep -q 'Random customer login from NORMAL DB' "$test_dir/normal-login.log"
grep -q 'Email: anna@example.com' "$test_dir/normal-login.log"
if SYNTHETIC_TEST_ENVIRONMENT=Production PATH="$test_dir/bin:$PATH" "$cli" --get-random-customer > /dev/null 2>&1; then
  echo 'Demo password must not be offered outside Development' >&2; exit 1
fi
sql nordiska_v2 "UPDATE banking.customers SET \"PasswordHash\"='changed-password' WHERE \"Id\"=1;" > /dev/null
if PATH="$test_dir/bin:$PATH" "$cli" --get-random-customer > /dev/null 2>&1; then
  echo 'Changed demo password must not be advertised as the default' >&2; exit 1
fi
echo 'PASS: active normal database demo login, production exclusion and changed password exclusion'
# Only the disposable stack's mocked app is switched; development stays untouched.
PATH="$test_dir/bin:$PATH" "$cli" --switch nordiska_synthetic > /dev/null
PATH="$test_dir/bin:$PATH" "$cli" --get-random-customer > "$test_dir/login.log"
grep -q 'Random customer login from SYNTHETIC DB' "$test_dir/login.log"
grep -q 'Email: cust[0-9]*@nordiska-demo.se' "$test_dir/login.log"
grep -q 'Password: password123' "$test_dir/login.log"
customer_id=$(sed -n 's/^Customer ID: //p' "$test_dir/login.log")
[[ $customer_id =~ ^[0-9]+$ ]]
[[ $(sql nordiska_synthetic "SELECT COUNT(*) FROM banking.customers WHERE \"Id\"=$customer_id AND \"Email\"='cust$customer_id@nordiska-demo.se';") == 1 ]]
[[ $(sql nordiska_v2 'SELECT COUNT(*) FROM banking.customers;') == 1 ]]

# If an app image is already built, verify the real frontend login endpoint.
# Its five module connections point only at this disposable database.
app_image=${SYNTHETIC_TEST_APP_IMAGE:-nordiska-v2-app:latest}
if "$real_docker" image inspect "$app_image" > /dev/null 2>&1; then
  "$real_docker" network create "$network" > /dev/null
  "$real_docker" network connect --alias db "$network" "$container"
  connection='Host=db;Port=5432;Database=nordiska_synthetic;Username=nordiska_api;Password=isolated-api-password'
  "$real_docker" run -d --name "$app_container" --network "$network" -p 127.0.0.1::8080 \
    -e ASPNETCORE_ENVIRONMENT=Development \
    -e "ConnectionStrings__DefaultConnection=$connection" \
    -e "ConnectionStrings__BankingDatabase=$connection" \
    -e "ConnectionStrings__FaqDatabase=$connection" \
    -e "ConnectionStrings__ReportingDatabase=$connection" \
    -e "ConnectionStrings__InboxDatabase=$connection" \
    -e Jwt__SecretKey=isolated-login-test-signing-key-at-least-32-characters \
    -e AuditLogging__SigningKey=isolated-login-test-audit-key-at-least-32-characters \
    -e ActiveLogin__BankId__Environment=Simulated "$app_image" > /dev/null
  address=$("$real_docker" port "$app_container" 8080/tcp)
  python3 "$repo_root/tools/synthetic/tests/helpers/login-api.py" "http://$address" "$test_dir/login.log" || {
    "$real_docker" logs --tail 40 "$app_container" >&2
    exit 1
  }
  "$real_docker" rm -f "$app_container" > /dev/null
else
  echo "SKIP: frontend login verification requires a built app image ($app_image)"
fi
# The database rejects exactly one index build after all data commits. A fresh
# session must restore indexes, but the original operation must still fail.
if SYNTHETIC_TEST_FAIL_INDEX_REBUILD=1 PATH="$test_dir/bin:$PATH" "$cli" --customers 10000 --threads 4 > "$test_dir/failed-index.log" 2>&1; then
  echo 'Expected index rebuild failure' >&2; exit 1
fi
grep -q 'isolated index rebuild failure' "$test_dir/failed-index.log"
grep -q 'Ledger query indexes restored' "$test_dir/failed-index.log"
! grep -q '100.0%' "$test_dir/failed-index.log"
[[ $(sql nordiska_synthetic "SELECT COUNT(*) FROM pg_index WHERE indrelid='banking.ledger_entries'::regclass AND indisvalid AND indisready;") == 5 ]]
echo 'PASS: index rebuild failure restores indexes through a fresh session and never reports completion'

# Include a real leap-year February and activities on days 29..31.
PATH="$test_dir/bin:$PATH" "$cli" --customers 100 --threads 4 --year 2024 --seed 42 > "$test_dir/leap-year.log" 2>&1
check_interest 2024
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.ledger_entries WHERE "Type"='"'interest'"' AND "CreatedAt"::date='"'2024-02-29'"';') -gt 0 ]]
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.ledger_entries WHERE "Type" IN ('"'deposit','withdrawal'"') AND EXTRACT(DAY FROM "CreatedAt")>28;') -gt 0 ]]
echo 'PASS: leap-year daily accrual, February 29 payouts and full-month activity dates'

# Deterministic rows and reserved IDs must survive scheduling changes.
for threads in 1 4; do
  PATH="$test_dir/bin:$PATH" "$cli" --customers 1000 --threads "$threads" --seed 42 > "$test_dir/deterministic-$threads.log" 2>&1
  sql nordiska_synthetic 'SELECT md5(string_agg(row_to_json(t)::text, '"'|'"' ORDER BY "Id")) FROM banking.customers t;
SELECT md5(string_agg(row_to_json(t)::text, '"'|'"' ORDER BY "Id")) FROM banking.savings_accounts t;
SELECT md5(string_agg(row_to_json(t)::text, '"'|'"' ORDER BY "Id")) FROM banking.ledger_entries t;' > "$test_dir/rows-$threads"
done
cmp "$test_dir/rows-1" "$test_dir/rows-4"
echo 'PASS: exact customer, account and ledger rows (including IDs) reproduce across thread counts'
# Fail partway through the disposable clone's initial transaction. All four
# tables must roll back; no parent rows or progress can escape the failed commit.
"$real_docker" exec -i "$container" psql -U nordiska_bootstrap -d nordiska_v2 -v ON_ERROR_STOP=1 -q <<'SQL'
CREATE FUNCTION banking.synthetic_ingest_failure() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF NEW."Id" > 10000 THEN RAISE EXCEPTION 'isolated staged ingestion failure'; END IF;
  RETURN NEW;
END;
$$;
CREATE TRIGGER synthetic_ingest_failure BEFORE INSERT ON banking.ledger_entries
FOR EACH ROW EXECUTE FUNCTION banking.synthetic_ingest_failure();
SQL
if PATH="$test_dir/bin:$PATH" "$cli" --customers 500 --threads 2 > "$test_dir/failed-ingest.log" 2>&1; then
  echo 'Expected a staged ingestion failure after the first batch' >&2; exit 1
fi
grep -q 'isolated staged ingestion failure' "$test_dir/failed-ingest.log"
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.customers;') == 0 ]]
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.savings_accounts;') == 0 ]]
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.ledger_entries;') == 0 ]]
! grep -q '100.0%' "$test_dir/failed-ingest.log"
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking."AspNetUserRoles";') == 0 ]]
echo 'PASS: failed sample rolls back customers, roles, accounts and ledger and preserves PostgreSQL errors'
"$real_docker" exec -i "$container" psql -U nordiska_bootstrap -d nordiska_v2 -v ON_ERROR_STOP=1 -q <<'SQL'
CREATE OR REPLACE FUNCTION banking.synthetic_ingest_failure() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF EXISTS (SELECT 1 FROM banking.savings_accounts WHERE "Id"=NEW."AccountId" AND "CustomerId">500)
  THEN RAISE EXCEPTION 'isolated later customer batch failure'; END IF;
  RETURN NEW;
END;
$$;
SQL
if PATH="$test_dir/bin:$PATH" "$cli" --customers 10000 --threads 2 > "$test_dir/failed-later-batch.log" 2>&1; then
  echo 'Expected a failure in a sustained data transaction' >&2; exit 1
fi
grep -q 'isolated later customer batch failure' "$test_dir/failed-later-batch.log"
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.customers;') == 500 ]]
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.ledger_entries;') -gt 0 ]]
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.ledger_entries l JOIN banking.savings_accounts a ON a."Id"=l."AccountId" WHERE a."CustomerId">500;') == 0 ]]
grep -q '500 / 10,000' "$test_dir/failed-later-batch.log"
! grep -q '1,500 / 10,000' "$test_dir/failed-later-batch.log"
! grep -q '100.0%' "$test_dir/failed-later-batch.log"
grep -q 'Ledger query indexes restored' "$test_dir/failed-later-batch.log"
[[ $(sql nordiska_synthetic "SELECT COUNT(*) FROM pg_index WHERE indrelid='banking.ledger_entries'::regclass AND indisvalid AND indisready;") == 5 ]]
echo 'PASS: later ingestion failure retains the first completed 500, rolls back the failing transaction and never claims completion'

PATH="$test_dir/bin:$PATH" "$cli" --delete > /dev/null
[[ $(sql postgres "SELECT COUNT(*) FROM pg_database WHERE datname='nordiska_synthetic';") == 0 ]]
echo "PASS: isolated native 10k seed + append: $counts; roles, foreign keys, balances, long labels and runtime permissions verified"
