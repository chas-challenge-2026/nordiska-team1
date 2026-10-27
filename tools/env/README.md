# Nordiska Environment Manager (`nordiska-env`)

An interactive terminal menu and CLI tool to inspect, modify, and export environment variables in `infra/.env` and your local shell session.

## Quick Start

### 1. Interactive Menu & Direct Shell Export
To open the interactive menu and **automatically export all environment variables into your current shell session**:

```bash
source tools/set-env.sh
# or:
source tools/env/set-env.sh
```

### 2. Standalone Binary Execution
To update `infra/.env` without altering your current shell variables:

```bash
./tools/env/nordiska-env
# or:
./tools/set-env.sh
```

---

## Features

- **Database Switching**:
  - One-key switch to **NORMAL DB** (`nordiska_v2`).
  - One-key switch to **SYNTHETIC DB** (`nordiska_synthetic`), including automated synchronization of `NORDISKA_REPORT_DOCUMENTS_VOLUME`.
  - Custom target database support.
- **Variable Management**:
  - View all active variables in a clean, aligned table.
  - Edit existing values with inline previews.
  - Add custom environment variables.
  - Delete / unset variables with confirmation.
- **Secret Generator**:
  - Generate secure cryptographic random secrets for empty or placeholder values (JWT secrets, audit signing keys, log salts, admin password, PKCS#11 PINs).
  - Matches the Nordiska DevSetup security specifications.
- **Shell Export & Subshell**:
  - Export variables to current shell via `source tools/set-env.sh`.
  - `eval $(tools/env/nordiska-env --export)` support.
  - Spawns an isolated subshell with all variables loaded.
- **Preserves `.env` Structure**:
  - Atomic writes preserving original comments, spacing, and variable ordering.

---

## Interactive Menu Overview

```text
====================================================================
              NORDISKA ENVIRONMENT MANAGER
====================================================================
 Config File: /home/jimmy/nordiska-team1/infra/.env
 Target DB (.env):  nordiska_synthetic (Synthetic DB)
 Shell DB ($ENV):   nordiska_synthetic
 Document Storage:  nordiska-v2_synthetic_report_documents
--------------------------------------------------------------------
 Database Presets:
   [1] Switch to NORMAL database      (nordiska_v2)
   [2] Switch to SYNTHETIC database   (nordiska_synthetic)
   [3] Enter custom database name

 Variables & Secrets:
   [4] View all environment variables
   [5] Edit a variable value
   [6] Add a new variable
   [7] Delete a variable
   [8] Generate missing secrets (random keys)

 Shell Integration:
   [9] Launch subshell with variables exported
   [10] Print shell export commands

 Actions:
   [S] Force re-save to infra/.env
   [Q] Quit
--------------------------------------------------------------------
Select an option [1-10, S, Q]:
```

---

## Command Line Options

The tool also supports non-interactive CLI flags for automation and scripting:

```bash
# Switch database
./tools/env/nordiska-env --db nordiska_synthetic
./tools/env/nordiska-env --db nordiska_v2

# Set or get a variable
./tools/env/nordiska-env --set REPORTING_WORKER_CONCURRENCY=16
./tools/env/nordiska-env --get NORDISKA_DATABASE

# List all variables
./tools/env/nordiska-env --list

# Print shell export statements
./tools/env/nordiska-env --export
eval $(./tools/env/nordiska-env --export)

# Generate missing secrets
./tools/env/nordiska-env --generate-secrets

# Check database and config status
./tools/env/nordiska-env --status
```

---

## Building from Source

To compile the release binary:

```bash
bash tools/env/build-linux-release.sh
```

Or using CMake:

```bash
mkdir -p tools/env/build
cmake -B tools/env/build -S tools/env -DCMAKE_BUILD_TYPE=Release
cmake --build tools/env/build
```
