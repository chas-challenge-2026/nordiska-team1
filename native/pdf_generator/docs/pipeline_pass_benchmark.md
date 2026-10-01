# Pipeline pass benchmark — 2026-09-22

Release build, AMD Ryzen AI 7 350 under WSL, one worker, default compression and simdjson. Default pool of 100 customer payloads cycled to 1,000 customers (3,410 PDFs) per iteration; three warmup calls and three measured iterations per process. Values below are medians of all three iterations, not a historical before/after comparison. Runs executed serially; no CPU affinity or frequency control.

Command template:

```sh
build/pdf_generator_benchmark --api direct --workers 1 --target-customers 1000 --iterations 3 --warmups 3 --renderer R [--signing] [--instrumented]
```

Raw logs for this run: `build/pass-bench/*.txt` (local build artifacts).

## Throughput

| Renderer | OFF plain docs/s | OFF timed | ON plain | ON timed |
|---|---:|---:|---:|---:|
| native | 19,502.1 | 19,243.6 | 17,862.1 | 17,616.6 |
| haru | 11,986.8 | 11,806.8 | 11,464.2 | 10,924.1 |
| cairo | 938.2 | 925.1 | 942.9 | 928.6 |

## Passes with signing stub enabled

Elapsed microseconds/document, summed over workers. Nested inclusive rows must not be added to their children. Fine timers include clock overhead; independently computed medians need not sum exactly.

| Pass | Native | Haru | Cairo |
|---|---:|---:|---:|
| Ingestion | 4.391 | 4.989 | 6.976 |
| Layout | 1.343 | 1.559 | 2.829 |
| Render | 38.510 | 69.464 | 1026.975 |
| Signing pipeline (inclusive) | 11.636 | 14.613 | 38.478 |
| Preparation (inclusive) | 1.450 | 1.947 | 4.524 |
| Locate xref | 0.056 | 0.087 | 0.288 |
| Trailer parse | 0.134 | 0.176 | 0.440 |
| Xref entries | 0.174 | 0.217 | 0.491 |
| Catalog parse | 0.109 | 0.143 | 0.369 |
| Metadata copy | 0.119 | 0.175 | 0.328 |
| Format update / ByteRange | 0.657 | 0.902 | 2.217 |
| Reserve / write buffer | 0.093 | 0.113 | 0.201 |
| Signing digest | 1.533 | 2.844 | 13.784 |
| Signer wrapper (inclusive) | 0.168 | 0.219 | 2.379 |
| External call (reported) | 0.000 | 0.000 | 0.000 |
| CMS insertion | 3.541 | 3.685 | 3.604 |
| Final artifact checksum | 4.770 | 5.745 | 14.008 |
| Signing unassigned | 0.143 | 0.166 | 0.181 |

## Interpretation

Inspection is sub-microsecond for Native and Haru in this workload; eliminating it cannot explain the previously claimed 60% regression. Hashing and insertion are measurable. The 8192-character stub allocates/copies a string, and insertion validates every character before copying. Its external-call metric is deliberately zero, while wrapper time measures the actual cost.

Signing OFF still prepares the signature slot and computes both hashes. Rendering remains an inclusive measurement; renderer-internal passes have not yet been isolated. Historical throughput figures used different or unrecorded conditions, so these measurements do not establish the cause of a historical regression.

Corrected misleading CPU-time and phase-throughput labels, removed the unsupported wall-equivalence sanity claim, honored all requested warmups, and forced direct C++ when signing/ingestor options cannot be applied by the C ABI. Existing benchmark stub changes were retained.

Validation: formatting checks and all four CTest suites passed. Added byte-for-byte timed/untimed preparation equivalence checks for all renderers and timing nesting/accounting assertions.
