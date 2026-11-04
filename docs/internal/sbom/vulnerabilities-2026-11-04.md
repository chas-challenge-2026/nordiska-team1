# Source dependency vulnerability report

Scan completed successfully at **2026-11-04T11:35:42.594180Z** using Trivy **0.76.1**.

Source commit: `0c1bca39e2fe17a90df2dd1feb4a2d46e63c3743`. Exact dependency input hashes are in `provenance.json`.

Scope: tracked npm and NuGet lockfiles, including transitive, development, test, peer, and optional dependencies.
Native libraries, container OS packages/base images, external services, and deployed artifacts were not scanned.

## Results

**3 unique package/version/advisory findings.** Repeated occurrences across projects are deduplicated.

| Severity | Count |
| --- | ---: |
| CRITICAL | 0 |
| HIGH | 0 |
| MEDIUM | 3 |
| LOW | 0 |
| UNKNOWN | 0 |

| Package | Version | Advisory | Severity | Fixed versions reported |
| --- | --- | --- | --- | --- |
| npm: @vitest/mocker | 4.1.11 | [CVE-2026-112949](https://avd.aquasec.com/nvd/cve-2026-112949) | MEDIUM | 4.1.12, 5.0.3 |
| npm: @vitest/mocker | 4.1.11 | [CVE-2026-112950](https://avd.aquasec.com/nvd/cve-2026-112950) | MEDIUM | 4.1.12, 5.0.3 |
| npm: minimatch | 10.2.6 | [CVE-2026-118764](https://avd.aquasec.com/nvd/cve-2026-118764) | MEDIUM | 11.0.1 |

Database snapshot:

```json
{
  "Version": 2,
  "NextUpdate": "2026-11-05T07:16:25.22637534Z",
  "UpdatedAt": "2026-11-04T07:16:25.226375897Z",
  "DownloadedAt": "2026-11-04T11:26:36.165427605Z"
}
```

All severities and unfixed findings are retained. No packages were upgraded or findings suppressed.
Fixed versions are scanner suggestions, not tested upgrade recommendations. Assess runtime reachability and compatibility before remediation.
Zero findings would mean no matches within this scan's inputs and database snapshot; it would not establish zero risk.
