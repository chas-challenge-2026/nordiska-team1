# Nordiska PDF Generator — Benchmark Tool

High-performance native C++23 benchmarking harness for measuring document generation throughput, engine latency, and multi-threaded scaling.

```text
src/benchmark/
├── main.cpp          # Benchmark executable entry point
└── README.md         # Documentation and usage guide
```

---

## Architecture & How It Works

1. **In-Memory Preloading (Zero Disk I/O)**:
   - At startup, the benchmark loads all customer JSON payload files from the specified input directory or file into RAM.
   - During the benchmark measurement loop, **no disk reads or writes occur**. This isolates pure CPU throughput, memory bandwidth, JSON parsing, layout construction, and PDF rendering engine performance.
2. **Direct C ABI Integration (`--api cabi`)**:
   - Directly calls the exported public C API (`nordiska_pdf_v1_generate_customer_batch`) using the host delivery callback (`nordiska_pdf_delivery_callback`).
   - Simulates the exact call path used by the .NET backend via P/Invoke.
3. **Multi-Worker Thread Scaling (`--workers <W>`)**:
   - Spawns $W$ concurrent worker threads.
   - Workers pull customer batches atomically from the in-memory pool, allowing measurement of multi-core CPU scaling and lock contention.
4. **Target Scale Simulation (`--target-customers <N>`)**:
   - Cycles through the pre-loaded in-memory pool (e.g. 100 realistic customer batches) to simulate workloads of 1,000, 10,000, or 100,000+ customers without requiring gigabytes of JSON files on disk.
5. **Stop-on-First-Failure**:
   - If any document fails ingestion, validation, or rendering, the benchmark immediately aborts across all threads and reports the exact status code and error diagnostic.

---

## Build

From the `native/pdf_generator` directory:

```bash
cmake --build build --target pdf_generator_benchmark
```

The executable is located at `build/pdf_generator_benchmark`.

---

## Usage & CLI Options

```bash
./build/pdf_generator_benchmark [options]
```

By default, the benchmark **automatically detects and loads the pre-generated 100-customer pool** (`tools/synthetic-input-generator/generated/pool_100`). You do not need to specify any folder path!

### Options

| Option | Values | Default | Description |
|---|---|---|---|
| `-i, --input` | Path | Auto-detected | Custom path to JSON input file or directory |
| `--workers` | Integer | `1` | Number of parallel worker threads |
| `--target-customers` | Integer | `0` | Total customer batches to process across workers (`0` = process loaded pool once) |
| `--api` | `cabi`, `direct` | `cabi` | Invocation mode: C ABI (`nordiska_pdf_v1_generate_customer_batch`) or C++ direct |
| `--renderer` | `haru`, `cairo`, `native` | `haru` | PDF rendering engine |
| `--ingestor` | `simdjson`, `nlohmann` | `simdjson` | JSON ingestion engine |
| `--no-compression` | Flag | Disabled | Disable Flate stream compression in PDF rendering |
| `--compression` | `true`, `false` | `true` | Configure PDF stream compression |
| `--instrumented` | Flag | Disabled | Enables fine-grained pipeline phase breakdown (Ingest, Layout, Render) |
| `--iterations` | Integer | `1` | Number of benchmark measurement iterations |
| `--warmups` | Integer | `1` | Number of warmup batches executed prior to timing |

---

## Examples

### 1. Default Run (100 Customers, Single Worker)

```bash
./build/pdf_generator_benchmark
```

### 2. Multi-Core Scaling: 1,000 Customers Across 4 Workers

```bash
./build/pdf_generator_benchmark --workers 4 --target-customers 1000
```

### 3. High-Throughput Stress Test: 10,000 Customers Across 8 Workers

```bash
./build/pdf_generator_benchmark --workers 8 --target-customers 10000
```

Sample output:
```text
Loaded 100 customer payload(s) (1.47 MB) into RAM.
Benchmark mode: API=cabi, workers=8, target_customers=10000, renderer=haru, ingestor=nlohmann

--- Benchmark Results (Iteration 1) ---
  Total wall time:       3691.86 ms (3.69 s)
  Customers processed:   10000
  Throughput (customers):2708.7 cust/sec (0.37 ms/customer)
  Documents generated:   34100
  Throughput (documents):9236.5 docs/sec (0.11 ms/doc)
  Transactions simulated:447200 (121131.3 tx/sec)

--- Memory Utilization & High Watermark ---
  Input RAM footprint:   1.47 MB (100 payloads, avg 15.08 KB/batch, max 131.25 KB)
  Baseline Process RSS:  7.29 MB (after loading input payloads)
  Peak RSS (high-water): 13.99 MB (maximum physical RAM mapped)
  Worker RAM overhead:   6.70 MB (~0.84 MB / worker across 8 thread(s))

--- Output Data Footprint ---
  Total PDF generated:   120.51 MB (32.64 MB/sec)
  Average customer batch:12.34 KB / customer
  Peak customer batch:   67.07 KB
  Average document size: 3.62 KB / doc
  Peak document size:    15.04 KB
```

### 3. Comparing PDF Engines (Native, Libharu, Cairo)

```bash
# Dedicated Native engine (fastest, ~44,000 docs/sec)
./build/pdf_generator_benchmark --renderer native --no-compression --workers 16 --target-customers 50000

# Libharu engine
./build/pdf_generator_benchmark --renderer haru --workers 16 --target-customers 50000

# Cairo engine
./build/pdf_generator_benchmark --renderer cairo --workers 16 --target-customers 50000
```

### 4. Phase Breakdown & Profiling (`--instrumented`)

To measure exactly where CPU time is spent across the three pipeline stages (JSON Ingestion, Layout Builder, PDF Render Engine) and verify sanity against total wall time:

```bash
./build/pdf_generator_benchmark --workers 8 --target-customers 10000 --instrumented
```

Sample output:
```text
--- Pipeline Phase Breakdown (--instrumented) ---
  Phase               CPU Time      Wall Equiv      Share     Throughput       CPU / Doc
  JSON Ingestion:        17.51 s        2.19 s      60.6 %     15581.0 docs/s     0.513 ms
  Layout Builder:         0.91 s        0.11 s       3.2 %    298965.1 docs/s     0.027 ms
  PDF Render Engine:     10.46 s        1.31 s      36.2 %     26085.7 docs/s     0.307 ms
  ------------------------------------------------------------------------------------
  Sum of Phases:         28.88 s        3.61 s     100.0 %      9446.4 docs/s     0.847 ms
  Total Wall Time:       29.11 s        3.64 s           -      9372.6 docs/s     0.854 ms
  Sanity Check:       Sum of phases (3.61 s) matches wall time (3.64 s) within 28.42 ms (0.8% delta, worker scheduling overhead)
```

---

## Metric Definitions

### Throughput Metrics
- **Throughput (customers)**: Number of completed customer batches per second and average milliseconds spent per customer.
- **Throughput (documents)**: Total generated PDF documents (account statements + tax reports) per second and average milliseconds per PDF.
- **Transactions simulated**: Total underlying ledger transactions parsed, laid out, and rendered per second.

### Pipeline Phase Breakdown Metrics (`--instrumented`)
- **JSON Ingestion**: Time spent parsing UTF-8 JSON into domain structures (`nlohmann::json` or `simdjson`).
- **Layout Builder**: Time spent in deterministic typography, pagination, table column math, and visual block flow.
- **PDF Render Engine**: Time spent drawing vector shapes, paths, text primitives, and compiling binary PDF streams.
- **Sanity Check**: Compares the sum of the three pipeline phases against measured total wall time to verify timing accuracy without measurement distortion.

### Memory & High-Water Mark Metrics
- **Input RAM footprint**: Exact memory occupied in RAM by the pre-loaded JSON customer payloads (including average and maximum single-batch sizes).
- **Baseline Process RSS**: Resident set size immediately after parsing and loading input payloads into memory.
- **Peak RSS (high-water)**: The absolute maximum physical RAM (`VmHWM` from Linux `/proc/self/status`) mapped to the process throughout the benchmark.
- **Worker RAM overhead**: Difference between peak RSS and baseline input RSS, and the per-thread allocation overhead.

### Output Data Footprint
- **Total PDF generated**: Cumulative size of all completed binary PDF streams delivered to the host callback and generated throughput in MB/sec.
- **Average customer batch**: Mean PDF output size per customer.
- **Peak customer batch**: Maximum memory needed to hold a single customer's complete batch in RAM.
- **Average & Peak document size**: Mean and maximum byte size of individual PDF files.
