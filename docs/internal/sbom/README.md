# Source Dependency SBOM

This directory contains the source dependency inventory for Nordiska project version `2.0.0`, its provenance, and a dated vulnerability scan. The SPDX format remains `SPDX-2.2` at the existing manifest path.

## Regenerate

Run from the repository root on Linux x86_64 or x86_64 WSL with Bash, Python 3, Git, and curl installed:

```bash
bash tools/sbom/update.sh
```

The script downloads temporary, checksum-verified copies of Microsoft SBOM Tool `4.1.5` and Trivy `0.76.1`. Network access is required for tool downloads, license metadata, and vulnerability database updates. It does not install application dependencies or upgrade packages.

Generation stages the current working-tree contents of Git-tracked dependency manifests and lockfiles, preserving their relative paths. It excludes `node_modules`, `bin`, and `obj`, so installed packages and build leftovers do not change the inventory. Keep manifests and lockfiles consistent before regenerating; add new dependency files to Git tracking first.

Microsoft SBOM Tool runs with license lookup and package metadata parsing enabled (`-li true -pm true`). License enrichment is best effort: missing or unresolved information stays `NOASSERTION`; it is never inferred from a package name. Review declared license expressions and unresolved entries before relying on them.

NuGet lockfiles are converted to a temporary `packages.config` containing their exact resolved versions for Microsoft's detector. npm peer and platform-specific optional entries omitted by that detector are supplemented directly from the lockfile. These additions are recorded in provenance. License declarations are supplemented from npm lockfiles and matching-version NuGet `.nuspec` metadata (local package cache, or the official NuGet API); the evidence source and SHA-256 are recorded. File- and URL-only license declarations remain unresolved. `licenseConcluded` is not inferred.

Trivy scans the staged lockfiles directly, including development dependencies and all severities and unfixed findings. Local Trivy environment settings, configuration files, and ignore files are excluded from this scan. This preserves package-manager identities and dependency scopes for vulnerability matching. The scan is separate from the Microsoft-generated SPDX document.

Existing published artifacts are preserved until generation and scanning succeed. A failed scan is reported as a failure, never as zero findings. Resolve the reported download, generation, or scan error and rerun the command; do not treat an older report as a new successful scan.

## Generated artifacts

All paths below are relative to this directory:

| File | Contents |
| --- | --- |
| `_manifest/spdx_2.2/manifest.spdx.json` | SPDX 2.2 package inventory and license fields. |
| `_manifest/spdx_2.2/manifest.spdx.json.sha256` | SHA-256 digest of the manifest, written with LF line endings. |
| `provenance.json` | Generation timestamp, Git HEAD, dependency input paths and SHA-256 hashes, pinned tool versions and binary hashes, and license coverage counts. |
| `vulnerabilities-YYYY-MM-DD.json` | Trivy JSON result, with its temporary staging path replaced by a stable label. |
| `vulnerabilities-YYYY-MM-DD.md` | Human-readable scan summary and findings. |

Git HEAD identifies the checked-out revision; the input hashes identify the actual dependency file contents used, including any tracked local edits. Tool versions are pinned, but timestamps, external metadata, and vulnerability databases can change between runs.

## Verify

Run the local verification command from the repository root:

```bash
python3 tools/sbom/sbom.py verify .
python3 -m unittest discover -s tools/sbom -p 'test_*.py'
```

Verification checks the saved artifact and provenance against the recorded hashes and the current tracked dependency inputs. It requires no tool downloads or network access. A passing check establishes consistency with the recorded inputs; rerun generation to refresh licenses or vulnerability data.

## Coverage and limits

The inventory covers npm and NuGet dependencies found in the tracked manifests and lockfiles, including transitive, development, and test dependencies. A source inventory can include dependencies that do not ship in a production deployment.

The following are outside this inventory:

* Native C/C++ dependencies, including OpenSSL, libharu, Cairo, zlib, simdjson, and nlohmann/json.
* Container operating-system packages and base images.
* External services and infrastructure components.
* Built binaries and their file integrity or correspondence to deployed artifacts.

License and vulnerability information depends on available upstream metadata and scanner coverage. `NOASSERTION` means the license is unresolved. Zero reported vulnerabilities means none were matched within the successful scan's scope and database snapshot; it does not establish zero risk. These artifacts are not an assertion of legal compliance.

This workflow generates documentation and reports. Package upgrades, remediation, CI integration, and application changes require separate work.

## Tool references

* [Microsoft SBOM Tool arguments](https://github.com/microsoft/sbom-tool/blob/main/docs/sbom-tool-arguments.md), including license lookup and metadata parsing.
* [Trivy filesystem scanning](https://github.com/aquasecurity/trivy/blob/main/docs/guide/target/filesystem.md).
* [Trivy Node.js coverage](https://www.trivy.dev/docs/latest/guide/coverage/language/nodejs/) and [.NET coverage](https://www.trivy.dev/docs/latest/guide/coverage/language/dotnet/).
