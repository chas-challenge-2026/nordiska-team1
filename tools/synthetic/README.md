# Nordiska Synthetic Banking Data Generator & Dual-Database Management

A high-performance deterministic synthetic retail banking data generator and isolated dual-database management system built in **modern C++23** and Python for local development, integration testing, and industrial-scale load testing (up to millions of customers).

The system populates realistic Swedish retail banking data directly into a dedicated PostgreSQL database (`nordiska_synthetic`), keeping the standard development database (`nordiska_v2`) clean and untouched. Developers can easily switch between databases and scale workloads via an interactive, arrow-key navigable TUI or scriptable CLI.

---

## Key Architectural Decisions

### 1. High-Performance C++23 Streaming Ingestion Pipeline
* Built in modern C++23 (`std::jthread`, `std::format`, `std::span`) matching the repository's native conventions (`native/pdf_generator`).
* **Multi-Core Parallelism**: Slices customer generation across all available CPU cores (e.g. 16 vCPUs on AMD Ryzen).
* **Flat $O(1)$ RAM Footprint**: Streams directly through thread-local memory chunk buffers into PostgreSQL text `COPY FROM STDIN` pipes. Memory usage stays strictly under **20 MB RAM** regardless of dataset size (from 50 to 1,000,000+ customers).
* **Throughput**:
  * Pure CPU generation: **~18,000,000 – 30,000,000 transactions/second**.
  * Direct PostgreSQL ingestion: **~160,000+ transactions/second** wire speed.
* Automatically resets sequence generators (`pg_get_serial_sequence`) post-ingestion.

### 2. Swedish Retail Banking Domain Simulation
* **Personal Identity Numbers**: Valid 12-digit Swedish personal identity numbers (`YYYYMMDDNNNC`) with verified Luhn modulo-10 checksums and collision-proof allocation.
* **Account Portfolio**: Pareto-distributed accounts per customer, mapped to system account types (`flex`, `fix`, `standard`, `saving`, `premium`) with corresponding interest rates.
* **Strict Balance Invariant**: `savings_accounts.Balance` strictly equals the algebraic sum of all `ledger_entries.Amount` (0 mismatches across hundreds of thousands of accounts).
* **Standard Interest & Tax Model**:
  * Follows the Swedish retail banking standard **Actual/365** day-count convention with daily accrual on end-of-day balances.
  * Monthly capitalization on the final calendar day of each month:
    * `interest`: Gross interest credit (*Ränteutbetalning*)
    * `tax`: Preliminary tax deduction at 30% (*Preliminärskatt 30%* per Inkomstskattelagen 42 kap.)
  * Explicit ledger rows so downstream consumers sum records directly (`SUM(Amount) WHERE Type = 'interest'`).
* **Customer Role Association**: Automatically maps synthetic customers to the `Customer` role (`RoleId=2`) in `banking."AspNetUserRoles"`.

---

## Interactive Navigable TUI & CLI Usage

Launch the arrow-key navigable terminal UI with:

```bash
./synthetic-data-manager
# or explicitly:
./synthetic-data-manager --tui
```

### Navigable TUI Features
* **Arrow-Key Navigation**: Use `↑` and `↓` arrow keys and `Enter` to select options.
* **Workload Selection**: Choose from pre-configured presets or specify custom counts.
* **CPU Core Selector**: Automatically detects available cores and lets you pick core allocation (e.g., all 16 cores, 8 balanced, or custom).
* **Impact & Workload Warning**: Computes estimated account count, transaction count, disk usage, and estimated time before execution.
* **Live Progress Bar**: Displays real-time progress, processed records, active throughput (`rows/s`), and ETA.

---

### Workload Presets

| Preset | Customers | Accounts | Approx. Transactions | Ingestion Time | Est. Storage | Target Use Case |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **`light`** | 50 | ~78 | ~3,400 | < 1s | < 10 MB | Quick sanity test, frontend smoke tests |
| **`medium`** | 1,000 | ~1,520 | ~70,000 | ~1s | ~20 MB | Daily dev, search, UI pagination |
| **`high`** | 10,000 | ~15,400 | ~735,000 | ~4.5s | ~120 MB | Integration benchmarks, query profiling |
| **`insane`** | 100,000 | ~154,000 | ~7,350,000 | ~25s | ~1.2 GB | Heavy load testing, bulk data scale |
| **`industrial`**| 1,000,000 | ~1,540,000 | ~73,000,000 | ~2-3 min | ~10.5 GB | Enterprise load & index stress tests |

---

### Scriptable CLI Flags (CI/CD)

```bash
# Check status of base and synthetic databases
./synthetic-data-manager --status

# Seed using C++23 native pipeline with 16 threads
./synthetic-data-manager --preset medium

# Seed high workload using 8 CPU threads
./synthetic-data-manager --preset high --threads 8

# Seed custom workload with text stress testing
./synthetic-data-manager --customers 50000 --threads 16 --stress-text

# Append customers without resetting existing data
./synthetic-data-manager --customers 5000 --append

# Switch application to synthetic database (restarts app container)
./synthetic-data-manager --switch nordiska_synthetic

# Switch application back to standard development database
./synthetic-data-manager --switch nordiska_v2

# Drop synthetic database to instantly reclaim disk space
./synthetic-data-manager --drop
```

---

## Native C++23 Module Compilation

The native generator lives in `native/synthetic_generator` and builds with CMake:

```bash
cd native/synthetic_generator
mkdir -p build && cd build
cmake .. -DCMAKE_BUILD_TYPE=Release
make -j$(nproc)
```

The compiled binary `nordiska-synthetic-gen` is automatically accessible via `tools/synthetic/bin/nordiska-synthetic-gen`.
