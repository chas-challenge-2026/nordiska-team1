#!/usr/bin/env bash
# Reproducible generation/ingestion measurements. Only test-owned PostgreSQL is written.
set -euo pipefail
repo_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
source_dir=${SYNTHETIC_BENCH_SOURCE:-$repo_root/native/synthetic_generator}
binary=${SYNTHETIC_BENCH_BINARY:-$repo_root/tools/synthetic/nordiska-synthetic-linux-x86_64}
output=${SYNTHETIC_BENCH_OUTPUT:-$(mktemp -d /tmp/nordiska-synthetic-benchmark.XXXXXX)}
mkdir -p "$output"
output=$(cd "$output" && pwd)
container="nordiska-synthetic-benchmark-$$"
trap 'docker rm -f "$container" >/dev/null 2>&1 || true' EXIT
compiler_image=gcc@sha256:5e927c284bf55a7dc796262e311a0703344f62f41f5621eb56843111b1d37e15
printf 'Results: %s\n' "$output"
sha256sum "$binary" > "$output/binary.sha256"
lscpu > "$output/cpu.txt"
for size in ${SYNTHETIC_BENCH_GENERATION_SIZES:-100000 1000000}; do
  for threads in ${SYNTHETIC_BENCH_THREADS:-1 4}; do
    /usr/bin/time -v "$binary" --benchmark --customers "$size" --threads "$threads" --seed 42 --year 2025 \
      > "$output/generation-$size-$threads.log" 2>&1
  done
done
cat > "$output/driver.cpp" <<'CPP'
#include "generation_progress.hpp"
#include <csignal>
int main(int argc, char** argv) { signal(SIGPIPE, SIG_IGN); return generate_main(argc, argv); }
CPP
docker run --rm --platform linux/amd64 --network none --user "$(id -u):$(id -g)" \
  -v "$source_dir:/src:ro" -v "$output:/work" "$compiler_image" \
  bash -c 'g++ -std=c++23 -O3 -DNDEBUG -static -pthread -I/src/include /src/src/generator.cpp /src/src/main.cpp /work/driver.cpp -o /work/driver'
source_db=$(docker ps --filter label=com.docker.compose.project=nordiska-v2 \
  --filter label=com.docker.compose.service=db --format '{{.ID}}')
[[ -n $source_db && $source_db != *$'\n'* ]] || { echo 'Expected one prepared V2 database' >&2; exit 1; }
# Only schema and two public fixture tables are read from the development database.
docker exec "$source_db" pg_dump -s -U nordiska_bootstrap nordiska_v2 > "$output/schema.sql"
docker exec "$source_db" pg_dump -a -U nordiska_bootstrap -t banking.account_type_configs \
  -t 'banking."AspNetRoles"' nordiska_v2 > "$output/reference.sql"
docker run -d --name "$container" -e POSTGRES_USER=nordiska_bootstrap \
  -e POSTGRES_PASSWORD=isolated-benchmark-password postgres:15-bookworm > /dev/null
ready=false
for attempt in {1..60}; do
  if docker exec "$container" pg_isready -h 127.0.0.1 -U nordiska_bootstrap >/dev/null 2>&1; then ready=true; break; fi
  sleep 1
done
[[ $ready == true ]] || { echo 'Disposable PostgreSQL startup failed' >&2; exit 1; }
docker exec "$container" psql -U nordiska_bootstrap -v ON_ERROR_STOP=1 -c \
  'CREATE ROLE nordiska_migrator; CREATE ROLE nordiska_api; CREATE ROLE nordiska_reporting_worker;' > /dev/null
for size in ${SYNTHETIC_BENCH_INGESTION_SIZES:-10000 30000}; do
  for threads in ${SYNTHETIC_BENCH_THREADS:-1 4}; do
    for mode in ${SYNTHETIC_BENCH_INDEX_MODES:-optimized}; do
      index_options=()
      case "$mode" in
        optimized) ;;
        keep-indexes) index_options=(--keep-indexes);;
        *) echo "Unknown benchmark index mode: $mode" >&2; exit 1;;
      esac
      docker exec "$container" dropdb --if-exists -U nordiska_bootstrap synthetic_perf 2>/dev/null
      docker exec "$container" createdb -U nordiska_bootstrap synthetic_perf
      for sql_file in schema reference; do
        docker exec -i "$container" psql -q -U nordiska_bootstrap -d synthetic_perf -v ON_ERROR_STOP=1 \
          < "$output/$sql_file.sql" > /dev/null
      done
      /usr/bin/time -v "$output/driver" "${index_options[@]}" --customers "$size" --threads "$threads" --seed 42 --year 2025 \
        --container "$container" --db synthetic_perf > "$output/ingestion-$mode-$size-$threads.log" 2>&1
      docker exec "$container" psql -At -U nordiska_bootstrap -d synthetic_perf -v ON_ERROR_STOP=1 -c \
        'SELECT (SELECT COUNT(*) FROM banking.customers), (SELECT COUNT(*) FROM banking.savings_accounts), (SELECT COUNT(*) FROM banking.ledger_entries);
SELECT * FROM pg_stat_wal; SELECT * FROM pg_stat_bgwriter;' >> "$output/ingestion-$mode-$size-$threads.log"
    done
  done
done
# Session microbenchmark: identical SQL, 100 executions, startup included.
{ /usr/bin/time -p bash -c 'for i in {1..100}; do docker exec "$1" psql -X -Atq -U nordiska_bootstrap -c "SELECT 1;" >/dev/null; done' _ "$container"; } 2> "$output/session-restarted.log"
{ for i in {1..100}; do echo 'SELECT 1;'; done; } > "$output/select.sql"
{ /usr/bin/time -p docker exec -i "$container" psql -X -Atq -U nordiska_bootstrap < "$output/select.sql" > /dev/null; } 2> "$output/session-persistent.log"
docker exec -i "$container" sh -c 'cat > /tmp/select.sql' < "$output/select.sql"
{ /usr/bin/time -p docker exec "$container" pgbench -n -U nordiska_bootstrap postgres -c 1 -t 1 -f /tmp/select.sql; } > "$output/session-libpq.log" 2>&1
# Lower-bound server-side COPY/index experiment, deliberately excludes FK checks
# and transport. Never infer full generator speedup from these microbenchmarks.
docker exec -i "$container" psql -X -U nordiska_bootstrap -d synthetic_perf -v ON_ERROR_STOP=1 \
  > "$output/copy-index.log" 2>&1 <<'SQL'
CREATE SCHEMA perf;
CREATE TABLE perf.source AS SELECT * FROM banking.ledger_entries ORDER BY "Id" LIMIT 500000;
COPY perf.source TO '/tmp/ledger-text.copy';
COPY perf.source TO '/tmp/ledger-binary.copy' (FORMAT binary);
CREATE TABLE perf.text_load (LIKE banking.ledger_entries INCLUDING ALL);
CREATE TABLE perf.binary_load (LIKE banking.ledger_entries INCLUDING ALL);
CREATE TABLE perf.bulk AS SELECT * FROM perf.source WITH NO DATA;
\timing on
COPY perf.text_load FROM '/tmp/ledger-text.copy';
ANALYZE perf.text_load;
COPY perf.binary_load FROM '/tmp/ledger-binary.copy' (FORMAT binary);
ANALYZE perf.binary_load;
COPY perf.bulk FROM '/tmp/ledger-text.copy';
SELECT regexp_replace(indexdef, 'INDEX [^ ]+ ON banking.ledger_entries', 'INDEX ON perf.bulk')
FROM pg_indexes WHERE schemaname='banking' AND tablename='ledger_entries' ORDER BY indexname \gexec
ANALYZE perf.bulk;
SELECT (SELECT COUNT(*) FROM perf.text_load), (SELECT COUNT(*) FROM perf.binary_load), (SELECT COUNT(*) FROM perf.bulk);
SQL
printf 'Completed; results retained in %s\n' "$output"
