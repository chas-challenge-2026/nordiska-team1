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

The timers use `steady_clock` and report summed worker **elapsed** time, not CPU
time. Use one worker to investigate individual passes; use uninstrumented runs
for throughput and compare both modes to estimate instrumentation overhead.

```bash
./build/pdf_generator_benchmark --api direct --workers 1 --target-customers 1000 --iterations 3 --warmups 3 --renderer native --signing --instrumented
```

The report separates ingestion, layout, rendering and the inclusive signing
pipeline. Signing contains preparation (xref location, trailer parsing, xref
entry traversal, catalog parsing, metadata copying, update formatting/ByteRange
patching, buffer reserve/writes), signing digest, signer wrapper, CMS insertion,
and final artifact checksum. Nested rows are already included in their parents.
The wrapper includes allocation/copying; the external-call row is provided by
the signer and remains zero for the benchmark stub.

`--signing` uses a fixed 8192-character hex stub, not real PKCS#11/CMS.
Signing off still performs slot preparation and both hashes.
Options unavailable through the C ABI select direct C++ and the banner reports
the effective API. All requested warmup calls run before measurement.

Wall timing includes thread/generator setup, result disposal and bookkeeping.
Dividing summed phase time by workers is not a measurement of wall time;
per-phase throughput is therefore not reported. Fine-grained timings include
clock-reading overhead and scheduling effects, especially for sub-microsecond
passes. Renderer timing remains inclusive of its internal rendering work.

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
- **Signing pipeline**: Inclusive preparation, hashing, signer and insertion elapsed time. Nested pass totals are checked against their parents by the signing tests; this is not proof of zero measurement overhead.

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
