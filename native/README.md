# native/

This directory is reserved for v2 native modules.

## What belongs here in v2

### 1. PDF Generator — `pdf_generator/`
- **Language:** C or C++
- **Purpose:** Batch-generate tax reports at year-end
- **Scale target:** 10,000 customers × ~50 transactions = 500,000 records rendered to PDF in < 5 minutes
- **Why native?** Thread.Sleep-based synchronous generation in v1 times out above ~20 accounts. A C/C++ batch worker called from a .NET background job can generate PDFs at 1,000/s using libharu or cairo.
- **Interface:** Called via `dotnet exec` or a named pipe IPC from the .NET background job service.

### 2. PDF Signing Module — `pdf_signer/`
- **Language:** C
- **Purpose:** Cryptographic signing of generated tax report PDFs for audit trail
- **What it does:** Computes SHA-256 hash of PDF content, signs with private key (PKCS#1), embeds signature in PDF metadata.
- **Why not in .NET?** HSM (Hardware Security Module) integration requires direct PKCS#11 C bindings; .NET wrappers add latency and reduce audit surface clarity.

## Build requirements (v2)
- CMake 3.25+
- OpenSSL 3.x dev headers (for pdf_signer)
- libharu (for pdf_generator)
- Valgrind for memory safety verification before integration

## Integration pattern (v2)
Native modules expose a minimal C ABI (`extern "C"`) consumed by the .NET app via P/Invoke or `ProcessStartInfo`. No direct memory sharing between runtimes.
