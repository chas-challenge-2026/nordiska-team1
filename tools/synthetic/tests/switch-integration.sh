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
    environment:
      ConnectionStrings__ReportingDatabase: Host=db;Database=${NORDISKA_DATABASE};Password=isolated-switch-test
    volumes:
      - report_documents:/docs
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
exec "$SYNTHETIC_TEST_DOCKER" "$@"
PROXY
chmod +x "$test_dir/bin/docker"
compose up -d --wait --wait-timeout 60 --scale reporting-worker=2 > /dev/null
db=$(compose ps --quiet db)
"$real_docker" exec "$db" psql -U nordiska_bootstrap -d postgres -v ON_ERROR_STOP=1 \
  -c 'CREATE DATABASE nordiska_synthetic;' > /dev/null
compose exec -T app sh -c 'printf normal > /docs/normal-marker'
cli="$test_dir/repo/tools/synthetic/manager"
PATH="$test_dir/bin:$PATH" "$cli" --switch nordiska_synthetic > "$test_dir/switch.log" 2>&1 || { cat "$test_dir/switch.log"; exit 1; }
[[ $(compose exec -T app sh -c 'test ! -f /docs/normal-marker && printf isolated') == isolated ]]
[[ $(compose exec -T app printenv FRONTEND_LOCAL_TEST) == preserved ]]
[[ $(compose ps --quiet reporting-worker | wc -l) == 2 ]]
compose exec -T app sh -c 'printf synthetic > /docs/synthetic-marker'
cp "$infra/v2/.env" "$test_dir/before-switch"
touch "$test_dir/fail-up-once"
if PATH="$test_dir/bin:$PATH" "$cli" --switch nordiska_v2 > "$test_dir/rollback.log" 2>&1; then
  echo 'Expected a switch failure' >&2; exit 1
fi
cmp "$test_dir/before-switch" "$infra/v2/.env"
grep -q 'previous services restored' "$test_dir/rollback.log"
PATH="$test_dir/bin:$PATH" "$cli" --delete > "$test_dir/delete.log" 2>&1 || { cat "$test_dir/delete.log"; exit 1; }
[[ $(compose exec -T app cat /docs/normal-marker) == normal ]]
compose exec -T app test ! -f /docs/synthetic-marker
[[ $("$real_docker" exec "$db" psql -U nordiska_bootstrap -d postgres -At \
  -c "SELECT COUNT(*) FROM pg_database WHERE datname='nordiska_synthetic';") == 0 ]]
# The inactive synthetic volume is intentionally retained by the tool, so remove
# this test-owned volume explicitly after checking it still exists.
"$real_docker" volume inspect "${SYNTHETIC_TEST_PROJECT}_nordiska-v2_synthetic_report_documents" > /dev/null
"$real_docker" volume rm "${SYNTHETIC_TEST_PROJECT}_nordiska-v2_synthetic_report_documents" > /dev/null
echo 'PASS: real isolated Compose switching, two workers, frontend configuration, rollback, delete and normal PDF preservation'
