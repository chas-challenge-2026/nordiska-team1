# Instrumented benchmark: 50,000 customers, 16 workers

Release, default compression, simdjson, direct C++ API. AMD Ryzen AI 7 350 (8 physical cores / 16 logical CPUs), WSL. Native and Haru only. Each process performs three warmup calls followed by three iterations of 50,000 customers / 170,500 PDFs. The 100-customer input pool is cycled. Signing ON uses the fixed 8192-character hex stub; OFF still prepares the slot and performs both hashes. Runs were sequential.

```sh
build/pdf_generator_benchmark --api direct --workers 16 --target-customers 50000 --iterations 3 --warmups 3 --renderer native --instrumented # add --signing; repeat with haru
```

## Measured throughput

| Renderer / signing | Iteration 1 docs/s | Iteration 2 | Iteration 3 | Median |
|---|---:|---:|---:|---:|
| native-off | 160,150.3 | 174,600.6 | 179,096.4 | 174,600.6 |
| native-on | 155,136.0 | 157,369.1 | 145,915.4 | 155,136.0 |
| haru-off | 100,778.4 | 90,772.4 | 94,661.2 | 94,661.2 |
| haru-on | 94,633.1 | 91,316.4 | 90,439.7 | 91,316.4 |

## Pass timings

Median summed worker elapsed microseconds per PDF across the three iterations. These are not wall latency or CPU time: concurrent timers include scheduling delays. Nested inclusive rows are already represented by their children. Instrumentation remains enabled for all throughput numbers.

| Pass | Native OFF | Native ON | Haru OFF | Haru ON |
|---|---:|---:|---:|---:|
| Ingestion | 7.024 | 7.306 | 7.760 | 7.890 |
| Layout | 2.667 | 2.793 | 3.076 | 3.043 |
| Render | 67.789 | 69.591 | 139.016 | 136.982 |
| Signing pipeline (inclusive) | 12.742 | 21.525 | 17.514 | 25.510 |
| Preparation (inclusive) | 3.100 | 3.254 | 4.235 | 4.033 |
| Locate xref | 0.114 | 0.136 | 0.166 | 0.150 |
| Trailer parse | 0.257 | 0.256 | 0.342 | 0.333 |
| Xref entries | 0.367 | 0.393 | 0.468 | 0.474 |
| Catalog parse | 0.238 | 0.256 | 0.283 | 0.289 |
| Metadata copy | 0.205 | 0.232 | 0.368 | 0.369 |
| Format update / ByteRange | 1.488 | 1.592 | 2.003 | 1.919 |
| Reserve / write buffer | 0.207 | 0.242 | 0.291 | 0.245 |
| Signing digest | 2.773 | 2.868 | 4.840 | 4.458 |
| Signer wrapper (inclusive) | 0.000 | 0.322 | 0.000 | 0.353 |
| External call (reported) | 0.000 | 0.000 | 0.000 | 0.000 |
| CMS insertion | 0.000 | 7.893 | 0.000 | 7.944 |
| Final artifact checksum | 6.761 | 7.029 | 8.335 | 8.461 |
| Signing unassigned | 0.108 | 0.229 | 0.109 | 0.234 |

Raw logs: `build/bench-50000-16-workers/*.txt`. Earlier interrupted one-worker and uninstrumented runs are excluded.
