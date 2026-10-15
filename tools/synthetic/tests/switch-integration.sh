#!/usr/bin/env bash
# Real Compose recreation and volume isolation in a uniquely named disposable stack.
# App/worker/signer are lightweight stand-ins; no development service is changed.
set -euo pipefail
repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../../.." && pwd)
real_docker=$(command -v docker)
test_dir=$(mktemp -d)
export SYNTHETIC_TEST_PROJECT="nordiska-synthetic-switch-test-$$"
export SYNTHETIC_TEST_DOCKER="$real_docker" SYNTHETIC_TEST_DIR="$test_dir"
infra="$test_dir/repo/infra"
compose() {
  "$real_docker" compose --project-name "$SYNTHETIC_TEST_PROJECT" --env-file "$infra/v2/.env" \
    -f "$infra/docker-compose.yml" -f "$infra/docker-compose.frontend-local.yml" "$@"
}
cleanup() {
  compose down --volumes > /dev/null 2>&1 || true
  "$real_docker" volume rm "${SYNTHETIC_TEST_PROJECT}_nordiska-v2_report_documents" \
    "${SYNTHETIC_TEST_PROJECT}_nordiska-v2_synthetic_report_documents" > /dev/null 2>&1 || true
  rm -rf -- "$test_dir"
}
trap cleanup EXIT
mkdir -p "$infra/v2" "$test_dir/repo/tools/synthetic" "$test_dir/bin"
cp "$repo_root/tools/synthetic/nordiska-synthetic-linux-x86_64" "$test_dir/repo/tools/synthetic/manager"
printf 'NORDISKA_DATABASE=nordiska_v2\n# preserved test comment\n' > "$infra/v2/.env"
cat > "$infra/docker-compose.yml" <<'COMPOSE'
services:
  db:
    image: postgres:15-bookworm
    environment:
      POSTGRES_USER: nordiska_bootstrap
      POSTGRES_PASSWORD: isolated-switch-test
      POSTGRES_DB: nordiska_v2
    tmpfs:
      - /var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U nordiska_bootstrap -d nordiska_v2"]
      interval: 1s
      timeout: 2s
      retries: 30
  app: &runtime
    image: postgres:15-bookworm
    entrypoint: ["sleep", "infinity"]
    stop_grace_period: 1s
    environment:
      ConnectionStrings__ReportingDatabase: Host=db;Database=${NORDISKA_DATABASE};Password=isolated-switch-test
    volumes:
      - report_documents:/var/lib/nordiska/report-documents
  reporting-worker: *runtime
  pdf-signer: *runtime
volumes:
  report_documents:
    name: ${SYNTHETIC_TEST_PROJECT}_${NORDISKA_REPORT_DOCUMENTS_VOLUME:-nordiska-v2_report_documents}
COMPOSE
printf 'services: {}\n' > "$infra/docker-compose.override.yml"
printf 'services:\n  app:\n    environment:\n      FRONTEND_LOCAL_TEST: preserved\n' > "$infra/docker-compose.frontend-local.yml"
cat > "$test_dir/bin/docker" <<'PROXY'
#!/usr/bin/env bash
set -euo pipefail
if [[ $1 == compose ]]; then
  if [[ " $* " == *' up '* && -f "$SYNTHETIC_TEST_DIR/fail-up-once" ]]; then
    rm "$SYNTHETIC_TEST_DIR/fail-up-once"
    exit 1
  fi
  args=("$@")
  for ((i=0; i<${#args[@]}; i++)); do
    if [[ ${args[i]} == --project-name ]]; then args[i+1]=$SYNTHETIC_TEST_PROJECT; fi
  done
  exec "$SYNTHETIC_TEST_DOCKER" "${args[@]}"
fi
if [[ $1 == volume && $2 == ls ]]; then
  "$SYNTHETIC_TEST_DOCKER" volume ls --quiet | while IFS= read -r volume; do
    [[ $volume != "${SYNTHETIC_TEST_PROJECT}_"* ]] || printf '%s\n' "${volume#${SYNTHETIC_TEST_PROJECT}_}"
  done
  exit 0
fi
if [[ $1 == inspect && " $* " == *'.Mounts'* ]]; then
  "$SYNTHETIC_TEST_DOCKER" "$@" | sed "s/${SYNTHETIC_TEST_PROJECT}_//g"
  exit 0
fi
args=("$@")
for ((i=0; i<${#args[@]}; i++)); do
  if [[ ${args[i]} == volume=nordiska-v2_synthetic_report_documents ]]; then
    args[i]="volume=${SYNTHETIC_TEST_PROJECT}_nordiska-v2_synthetic_report_documents"
  elif [[ ${args[i]} == type=volume,src=nordiska-v2_synthetic_report_documents,* ]]; then
    args[i]=${args[i]/src=nordiska-v2_synthetic_report_documents,/src=${SYNTHETIC_TEST_PROJECT}_nordiska-v2_synthetic_report_documents,}
  fi
done
exec "$SYNTHETIC_TEST_DOCKER" "${args[@]}"
PROXY
chmod +x "$test_dir/bin/docker"
compose up -d --wait --wait-timeout 60 --scale reporting-worker=2 > /dev/null
db=$(compose ps --quiet db)
"$real_docker" exec "$db" psql -U nordiska_bootstrap -d postgres -v ON_ERROR_STOP=1 \
  -c 'CREATE DATABASE nordiska_synthetic;' > /dev/null
compose exec -T app sh -c 'printf normal > /var/lib/nordiska/report-documents/normal-marker'
cli="$test_dir/repo/tools/synthetic/manager"
# Saved selection differs from the actual app. Failed switching must restore
# actual NORMAL services while retaining the exact saved selection.
sed -i 's/^NORDISKA_DATABASE=.*/NORDISKA_DATABASE=nordiska_synthetic/' "$infra/v2/.env"
cp "$infra/v2/.env" "$test_dir/before-stale-switch"
touch "$test_dir/fail-up-once"
if PATH="$test_dir/bin:$PATH" "$cli" --switch nordiska_synthetic > "$test_dir/stale-rollback.log" 2>&1; then
  echo 'Expected a stale-selection switch failure' >&2; exit 1
fi
cmp "$test_dir/before-stale-switch" "$infra/v2/.env"
compose exec -T app sh -c 'test "$ConnectionStrings__ReportingDatabase" = "Host=db;Database=nordiska_v2;Password=isolated-switch-test"'
[[ $(compose exec -T app cat /var/lib/nordiska/report-documents/normal-marker) == normal ]]
PATH="$test_dir/bin:$PATH" "$cli" --switch nordiska_synthetic > "$test_dir/switch.log" 2>&1 || { cat "$test_dir/switch.log"; exit 1; }
[[ $(compose exec -T app sh -c 'test ! -f /var/lib/nordiska/report-documents/normal-marker && printf isolated') == isolated ]]
[[ $(compose exec -T app printenv FRONTEND_LOCAL_TEST) == preserved ]]
[[ $(compose ps --quiet reporting-worker | wc -l) == 2 ]]
compose exec -T app sh -c 'printf synthetic > /var/lib/nordiska/report-documents/synthetic-marker'
cp "$infra/v2/.env" "$test_dir/before-switch"
touch "$test_dir/fail-up-once"
if PATH="$test_dir/bin:$PATH" "$cli" --switch nordiska_v2 > "$test_dir/rollback.log" 2>&1; then
  echo 'Expected a switch failure' >&2; exit 1
fi
cmp "$test_dir/before-switch" "$infra/v2/.env"
grep -q 'previous services restored' "$test_dir/rollback.log"
# Real constrained metadata in both disposable DBs; normal data must survive.
for database in nordiska_v2 nordiska_synthetic; do
  "$real_docker" exec -i "$db" psql -U nordiska_bootstrap -d "$database" -v ON_ERROR_STOP=1 -q <<'SQL'
CREATE SCHEMA reporting;
CREATE SCHEMA banking;
CREATE TABLE banking.customers ("Id" bigint PRIMARY KEY);
INSERT INTO banking.customers VALUES (1);
CREATE TABLE reporting.generated_documents ("Id" bigint PRIMARY KEY);
CREATE TABLE reporting.account_statement_documents ("DocumentId" bigint REFERENCES reporting.generated_documents("Id"));
CREATE TABLE reporting.tax_report_documents ("DocumentId" bigint REFERENCES reporting.generated_documents("Id"));
CREATE TABLE reporting.account_statement_jobs ("Id" bigint PRIMARY KEY);
CREATE TABLE reporting.tax_report_jobs ("Id" bigint PRIMARY KEY);
INSERT INTO reporting.generated_documents VALUES (1);
INSERT INTO reporting.account_statement_documents VALUES (1);
INSERT INTO reporting.tax_report_documents VALUES (1);
INSERT INTO reporting.account_statement_jobs VALUES (1);
INSERT INTO reporting.tax_report_jobs VALUES (1);
SQL
done
# Cleanup must preserve the running SYNTHETIC DB despite stale saved NORMAL.
sed -i 's/^NORDISKA_DATABASE=.*/NORDISKA_DATABASE=nordiska_v2/' "$infra/v2/.env"
cp "$infra/v2/.env" "$test_dir/before-documents"
compose exec -T app sh -c 'mkdir -p /var/lib/nordiska/report-documents/old; printf pdf > /var/lib/nordiska/report-documents/old/report.pdf; printf orphan > /var/lib/nordiska/report-documents/.orphan; ln -s /etc /var/lib/nordiska/report-documents/outside-link'
PATH="$test_dir/bin:$PATH" "$cli" --delete-documents > "$test_dir/documents.log" 2>&1 || { cat "$test_dir/documents.log"; exit 1; }
cmp "$test_dir/before-documents" "$infra/v2/.env"
[[ $(compose ps --quiet reporting-worker | wc -l) == 2 ]]
compose exec -T app sh -c 'test "$ConnectionStrings__ReportingDatabase" = "Host=db;Database=nordiska_synthetic;Password=isolated-switch-test"'
compose exec -T app test ! -e /var/lib/nordiska/report-documents/synthetic-marker
compose exec -T app test ! -e /var/lib/nordiska/report-documents/.orphan
compose exec -T app test ! -e /var/lib/nordiska/report-documents/old
compose exec -T app test ! -L /var/lib/nordiska/report-documents/outside-link
compose exec -T app test -f /etc/passwd
for table in generated_documents account_statement_documents tax_report_documents account_statement_jobs tax_report_jobs; do
  [[ $("$real_docker" exec "$db" psql -U nordiska_bootstrap -d nordiska_synthetic -At -c "SELECT COUNT(*) FROM reporting.$table;") == 0 ]]
  [[ $("$real_docker" exec "$db" psql -U nordiska_bootstrap -d nordiska_v2 -At -c "SELECT COUNT(*) FROM reporting.$table;") == 1 ]]
done
[[ $("$real_docker" exec "$db" psql -U nordiska_bootstrap -d nordiska_synthetic -At -c 'SELECT COUNT(*) FROM banking.customers;') == 1 ]]
echo 'PASS: synthetic PDF/metadata cleanup, safe symlinks, unchanged normal data, runtime DB and worker replicas'

# A stale NORMAL selection must not let Delete skip switching live services.
sed -i 's/^NORDISKA_DATABASE=.*/NORDISKA_DATABASE=nordiska_v2/' "$infra/v2/.env"
PATH="$test_dir/bin:$PATH" "$cli" --delete > "$test_dir/delete.log" 2>&1 || { cat "$test_dir/delete.log"; exit 1; }
[[ $(compose exec -T app cat /var/lib/nordiska/report-documents/normal-marker) == normal ]]
compose exec -T app test ! -f /var/lib/nordiska/report-documents/synthetic-marker
[[ $("$real_docker" exec "$db" psql -U nordiska_bootstrap -d postgres -At \
  -c "SELECT COUNT(*) FROM pg_database WHERE datname='nordiska_synthetic';") == 0 ]]
# Leftover PDF files remain cleanable after the synthetic database is dropped.
"$real_docker" run --rm --network none --entrypoint sh \
  -v "${SYNTHETIC_TEST_PROJECT}_nordiska-v2_synthetic_report_documents:/synthetic" \
  postgres:15-bookworm -c 'printf orphan > /synthetic/orphan.pdf'
PATH="$test_dir/bin:$PATH" "$cli" --delete-documents > "$test_dir/orphan-documents.log" 2>&1 || { cat "$test_dir/orphan-documents.log"; exit 1; }
[[ $(compose exec -T app cat /var/lib/nordiska/report-documents/normal-marker) == normal ]]
"$real_docker" run --rm --network none --entrypoint sh \
  -v "${SYNTHETIC_TEST_PROJECT}_nordiska-v2_synthetic_report_documents:/synthetic:ro" \
  postgres:15-bookworm -c 'test ! -e /synthetic/orphan.pdf'
echo 'PASS: orphan PDF cleanup after DB deletion preserves normal app storage'

# The inactive synthetic volume is intentionally retained by the tool, so remove
# this test-owned volume explicitly after checking it still exists.
"$real_docker" volume inspect "${SYNTHETIC_TEST_PROJECT}_nordiska-v2_synthetic_report_documents" > /dev/null
"$real_docker" volume rm "${SYNTHETIC_TEST_PROJECT}_nordiska-v2_synthetic_report_documents" > /dev/null
echo 'PASS: real isolated Compose switching, two workers, frontend configuration, rollback, delete and normal PDF preservation'
