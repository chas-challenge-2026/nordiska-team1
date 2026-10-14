#!/usr/bin/env bash
# Requires the prepared V2 db. Reads its schema/reference data only; all writes use an isolated container.
set -euo pipefail
repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../../.." && pwd)
real_docker=$(command -v docker)
test_dir=$(mktemp -d)
container="nordiska-synthetic-release-test-$$"
cleanup() {
  "$real_docker" rm -f "$container" > /dev/null 2>&1 || true
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
sql() { "$real_docker" exec "$container" psql -U nordiska_bootstrap -d "$1" -v ON_ERROR_STOP=1 -At -c "$2"; }
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
export SYNTHETIC_TEST_DOCKER="$real_docker" SYNTHETIC_TEST_CONTAINER="$container"
cat > "$test_dir/bin/docker" <<'PROXY'
#!/usr/bin/env bash
set -euo pipefail
if [[ $1 == compose ]]; then
  if [[ " $* " == *' ps --quiet db '* ]]; then echo "$SYNTHETIC_TEST_CONTAINER";
  elif [[ " $* " == *' ps --quiet '* ]]; then echo synthetic-service-mock; fi
  exit 0
fi
if [[ $1 == inspect && ${@: -1} == synthetic-service-mock ]]; then
  if [[ " $* " == *'Config.Env'* ]]; then
    value=$(sed -n 's/^NORDISKA_DATABASE=//p' "${BASH_SOURCE[0]%/bin/docker}/repo/infra/v2/.env")
    echo "ConnectionStrings__ReportingDatabase=Host=db;Database=$value;Password=isolated"
  fi
  exit 0
fi
exec "$SYNTHETIC_TEST_DOCKER" "$@"
PROXY
chmod +x "$test_dir/bin/docker"
cli="$test_dir/repo/tools/synthetic/manager"
PATH="$test_dir/bin:$PATH" "$cli" --customers 10000 --threads 4 --year 2025 --seed 42 > "$test_dir/seed.log" 2>&1 || { cat "$test_dir/seed.log"; exit 1; }
[[ $(sql nordiska_synthetic 'SELECT COUNT(*) FROM banking.customers;') == 10000 ]]
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
PATH="$test_dir/bin:$PATH" "$cli" --delete > /dev/null
[[ $(sql postgres "SELECT COUNT(*) FROM pg_database WHERE datname='nordiska_synthetic';") == 0 ]]
echo "PASS: isolated native 10k seed + append: $counts; roles, foreign keys, balances, long labels and runtime permissions verified"
