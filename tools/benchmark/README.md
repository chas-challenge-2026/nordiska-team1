# Nordiska Reporting Pipeline Benchmark

Standalone Linux console application (written in C++23) to benchmark the end-to-end whole-database reporting batch pipeline against the production .NET worker container.

## Quick Start

### Build the Tool
Run the build script from the repository root:
```bash
./tools/benchmark/build-linux-release.sh
```
This produces the standalone binary `tools/benchmark/nordiska-benchmark`.

### Interactive Controller (TUI)
Launch the unified benchmark controller to configure settings and run benchmarks:
```bash
./tools/benchmark/bench-tui.sh
```

### Direct CLI Mode
Run benchmarks directly from command line:
```bash
# Run benchmark on active database (all customers)
./tools/benchmark/nordiska-benchmark

# Quick sample benchmark (e.g. 100 customers)
./tools/benchmark/nordiska-benchmark --limit 100

# Benchmark with simulated TSA latency jitter (e.g. 60–100 ms)
./tools/benchmark/nordiska-benchmark --limit 100 --jitter 60-100

# Target a specific database
./tools/benchmark/nordiska-benchmark --db nordiska_synthetic
```

---

## Command-Line Options

| Option | Description | Default |
|---|---|---|
| `--limit <N>` | Limit benchmark to first N customers | `0` (unlimited, full database) |
| `--jitter <ms|min-max>` | Simulated TSA latency in ms (e.g. `80` or `60-100`) | Auto-detected from `.env` |
| `--db <name>` | Target PostgreSQL database name | Auto-detected from `infra/.env` |
| `--poll <seconds>`| Progress telemetry polling interval | `2` seconds |
| `--year <YYYY>` | Reporting tax & statement year | `2026` |
| `--no-clean` | Do not clean reporting tables before run | Disabled (cleans before staging) |
| `--no-save` | Do not write report to `runs/` folder | Disabled (saves to `runs/`) |
| `--help` | Show command line usage information | |
