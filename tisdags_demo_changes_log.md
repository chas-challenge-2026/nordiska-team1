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
   - Added required Debian development packages for modern CMake and package fallbacks: `git`, `libssl-dev`, `zlib1g-dev`, `libsimdjson-dev`.
   - Changed `COPY native/pdf_generator/ ./` to `COPY native/ native/` so that `native/pdf-signer` is available to the PDF generator build.
   - Updated CMake target invocation to `--target nordiska_pdf_generator_c_api` (matching the active target defined in `CMakeLists.txt`).
2. **Updated Stage 4 (`final`) in `Dockerfile`**:
   - Updated copy source to `libnordiska_pdf_generator_c_api.so`.
   - Created a backward-compatibility symlink `/usr/local/lib/libnordiska_document_c_api.so -> /usr/local/lib/libnordiska_pdf_generator_c_api.so` followed by `ldconfig`.
3. **Verification**:
   - Executed full multi-stage Docker build: completed successfully with exit code 0 (`580a56f43659dca275d6fda736940d31cdc493cc12e61`).
