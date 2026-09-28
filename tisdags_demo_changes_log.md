# Tisdags-Demo Changes Log

**Branch:** `temp/tisdags_demo`  
**Goal:** Connect the UI button to end-to-end PDF generation for the Tuesday demo.  
**Purpose of this document:** Keep a running, reproducible record of all configuration, build, and code adjustments so the team can review and adopt the changes smoothly.

---

## 1. Native Build & Docker Integration

### Problem
A fresh build of the Docker container fails during Stage 1 (`native-builder`) because:
1. `native/pdf_generator/CMakeLists.txt` depends on `../pdf-signer`, but `Dockerfile` only copied `native/pdf_generator/`.
2. Missing build tools and development libraries in Debian base image: `git`, `libssl-dev`, `zlib1g-dev`, and `libsimdjson-dev`.
3. Target name mismatch: `Dockerfile` was invoking `--target nordiska_document_c_api`, but the target in `CMakeLists.txt` is `nordiska_pdf_generator_c_api`.
4. Shared library naming: Stage 4 was trying to copy `libnordiska_document_c_api.so`, while CMake outputs `libnordiska_pdf_generator_c_api.so`.

### Changes Applied
1. **Updated Stage 1 (`native-builder`) in `Dockerfile`**:
   - Pinned exact Debian Bookworm package versions to eliminate silent version drift:
     - `cmake=3.25.1-1`
     - `pkg-config=1.8.1-1`
     - `libcairo2-dev=1.16.0-7`
     - `libhpdf-dev=2.3.0+dfsg-1+b1`
     - `nlohmann-json3-dev=3.11.2-2`
     - `libssl-dev=3.0.22-1~deb12u1`
     - `zlib1g-dev=1:1.2.13.dfsg-1`
     - `libsimdjson-dev=3.0.1-1`
   - Added `-DFETCHCONTENT_FULLY_DISCONNECTED=ON` to the CMake configuration to strictly forbid network access and prevent silent cloning from GitHub during builds.
   - Changed `COPY native/pdf_generator/ ./` to `COPY native/ native/` so that `native/pdf-signer` is available to the PDF generator build.
   - Updated CMake target invocation to `--target nordiska_pdf_generator_c_api` (matching the active target defined in `CMakeLists.txt`).
2. **Updated Stage 4 (`final`) in `Dockerfile`**:
   - Pinned exact runtime dependencies:
     - `libcairo2=1.16.0-7`
     - `libhpdf-2.3.0=2.3.0+dfsg-1+b1`
   - Updated copy source exclusively to the canonical library `libnordiska_pdf_generator_c_api.so` (discarding legacy naming entirely) followed by `ldconfig`.
3. **Verification**:
   - Executed full multi-stage Docker build with exact pins and offline enforcement: completed successfully with exit code 0 (`07d7ce881da1f1675a95adad7d19e4c330f8e68244f19`).

