#!/usr/bin/env python3
"""Generate and verify source dependency evidence without restoring the application."""
import argparse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tarfile
import tempfile
from urllib.parse import quote
import xml.etree.ElementTree as ET


SBOM_VERSION = "4.1.5"
TRIVY_VERSION = "0.76.1"
DOWNLOADS = {
    "sbom-tool": (
        f"https://github.com/microsoft/sbom-tool/releases/download/v{SBOM_VERSION}/sbom-tool-linux-x64",
        "bf5d4f99bc98c119d549d08fc02ae92598a7a42772f17317c01031a92632e05b"),
    "trivy.tar.gz": (
        f"https://github.com/aquasecurity/trivy/releases/download/v{TRIVY_VERSION}/trivy_{TRIVY_VERSION}_Linux-64bit.tar.gz",
        "c7cbe3137fb8a48a17574bede3dd6beb16742d1e9772929940fc90082c39d9c0"),
}
TRIVY_SHA256 = "d1dfca6ae10a1ad4540bb94e64f0510077c47008920a9b554986b39dca573d83"
OUTPUT = Path("docs/internal/sbom")
MANIFEST = Path("_manifest/spdx_2.2/manifest.spdx.json")
NAMES = {"package.json", "package-lock.json", "packages.lock.json", "packages.config",
         "Directory.Packages.props", "Directory.Build.props", "global.json"}
EXCLUDES = {"node_modules", "bin", "obj", ".git"}


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes((json.dumps(value, indent=2, ensure_ascii=False) + "\n").encode("utf-8"))


def git(root, *args):
    return subprocess.check_output(["git", "-C", str(root), *args], text=True).strip()


def input_files(root):
    tracked = subprocess.check_output(["git", "-C", str(root), "ls-files", "-z"], text=True)
    return sorted(p for name in tracked.split("\0") if name
                  for p in [Path(name)] if not EXCLUDES.intersection(p.parts)
                  and (p.name in NAMES or p.suffix in {".csproj", ".sln"}))


def verify_inputs(root, inputs):
    for name, expected in inputs.items():
        path = root / name
        if not path.is_file() or digest(path) != expected:
            raise ValueError(f"Dependency input changed: {name}; regenerate the SBOM")


def run(command, log, env=None):
    with log.open("wb") as output:
        result = subprocess.run([str(x) for x in command], stdout=output,
                                stderr=subprocess.STDOUT, env=env)
    if result.returncode:
        tail = "\n".join(log.read_text(errors="replace").splitlines()[-20:])
        raise RuntimeError(f"{command[0]} failed ({result.returncode}):\n{tail}")


def tools(cache):
    if platform.system() != "Linux" or platform.machine() not in {"x86_64", "AMD64"}:
        raise ValueError("Generation requires Linux x86_64 or x86_64 WSL")
    cache.mkdir(parents=True, exist_ok=True)
    for filename, (url, expected) in DOWNLOADS.items():
        path = cache / filename
        if not path.exists() or digest(path) != expected:
            pending = path.with_suffix(".download")
            subprocess.run(["curl", "-fsSL", "--retry", "2", "--connect-timeout", "15",
                            "--max-time", "180", url, "-o", str(pending)], check=True)
            if digest(pending) != expected:
                raise ValueError(f"Download checksum mismatch: {filename}")
            pending.replace(path)
    trivy = cache / "trivy"
    if not trivy.exists() or digest(trivy) != TRIVY_SHA256:
        with tarfile.open(cache / "trivy.tar.gz") as archive:
            with archive.extractfile("trivy") as src, trivy.open("wb") as dest:
                shutil.copyfileobj(src, dest)
    if digest(trivy) != TRIVY_SHA256:
        raise ValueError("Extracted Trivy binary checksum mismatch")
    for name in ("trivy", "sbom-tool"):
        (cache / name).chmod(0o700)


def locked_packages(source, inputs=None):
    """Deduplicate exact package identities, retaining a lockfile evidence location."""
    packages = {}
    files = [source / name for name in inputs] if inputs is not None else list(source.rglob("*lock.json"))
    for path in sorted(p for p in files if p.name == "package-lock.json"):
        for name, entry in json.loads(path.read_text())["packages"].items():
            if not name or "version" not in entry:
                continue
            name = entry.get("name") or name.rsplit("node_modules/", 1)[-1]
            packages[("npm", name, entry["version"])] = {
                "input": path.relative_to(source).as_posix(), "name": name,
                "license": entry.get("license", "NOASSERTION"),
                "download": entry.get("resolved", "NOASSERTION"),
            }
    for path in sorted(p for p in files if p.name == "packages.lock.json"):
        for entries in json.loads(path.read_text()).get("dependencies", {}).values():
            for name, entry in entries.items():
                if entry.get("resolved") and entry.get("type") != "Project":
                    packages[("nuget", name.lower(), entry["resolved"])] = {
                        "input": path.relative_to(source).as_posix(), "name": name,
                        "license": "NOASSERTION", "download": "NOASSERTION",
                    }
    return packages


def purl(key):
    ecosystem, name, version = key
    return f"pkg:{ecosystem}/{quote(name, safe='/')}@{quote(version, safe='')}"


def package_key(package):
    for reference in package.get("externalRefs", []):
        locator = reference.get("referenceLocator", "")
        for ecosystem in ("npm", "nuget"):
            if locator.startswith(f"pkg:{ecosystem}/"):
                name = package["name"]
                return ecosystem, name.lower() if ecosystem == "nuget" else name, package["versionInfo"]
    return None


def nuget_license(item):
    key, metadata = item
    _, name, version = key
    url = f"https://api.nuget.org/v3-flatcontainer/{quote(name)}/{quote(version)}/{quote(name)}.nuspec"
    cache = Path(os.environ.get("NUGET_PACKAGES", Path.home() / ".nuget/packages"))
    path = cache / name / version.lower() / (name + ".nuspec")
    evidence = {"package": purl(key), "source": url, "license": "NOASSERTION"}
    try:
        if path.is_file():
            raw = path.read_bytes()
            evidence["retrievedFrom"] = "local NuGet package cache"
        else:
            raw = subprocess.check_output(["curl", "-fsSL", "--connect-timeout", "10",
                                           "--max-time", "30", url], stderr=subprocess.DEVNULL)
            evidence["retrievedFrom"] = "NuGet flat-container API"
        document = ET.fromstring(raw)
        # NuGet nuspec metadata uses several versions of the same XML namespace.
        for node in document.iter():
            node.tag = node.tag.rsplit("}", 1)[-1]
        meta = document.find("metadata")
        if (meta is None or (meta.findtext("id") or "").lower() != name
                or meta.findtext("version") != version):
            raise ValueError("nuspec identity does not match the locked package")
        evidence["sha256"] = hashlib.sha256(raw).hexdigest()
        license_node = meta.find("license")
        if license_node is not None and license_node.get("type") == "expression" and license_node.text:
            evidence["license"] = license_node.text.strip()
        else:
            evidence["unresolvedReason"] = "Package metadata has no SPDX license expression (file or URL only)"
    except (OSError, subprocess.CalledProcessError, ET.ParseError, ValueError) as error:
        evidence["unresolvedReason"] = str(error)
    return key, evidence


def reconcile(manifest, packages, source):
    """Include peer/platform lock entries omitted by automatic component detection."""
    indexed = {package_key(x): x for x in manifest["packages"]}
    root = manifest["documentDescribes"][0]
    additions, evidence = [], []
    with ThreadPoolExecutor(max_workers=8) as executor:
        nuget = dict(executor.map(nuget_license, ((k, v) for k, v in packages.items() if k[0] == "nuget")))
    for key, metadata in sorted(packages.items()):
        if key not in indexed:
            identifier = "SPDXRef-Locked-" + hashlib.sha256(purl(key).encode()).hexdigest()
            package = {"name": metadata["name"], "versionInfo": key[2], "SPDXID": identifier,
                       "downloadLocation": metadata["download"], "filesAnalyzed": False,
                       "licenseConcluded": "NOASSERTION", "licenseDeclared": "NOASSERTION",
                       "copyrightText": "NOASSERTION", "supplier": "NOASSERTION",
                       "sourceInfo": f"Exact locked dependency from {metadata['input']}; includes optional and peer entries",
                       "externalRefs": [{"referenceCategory": "PACKAGE-MANAGER",
                                         "referenceType": "purl", "referenceLocator": purl(key)}]}
            manifest["packages"].append(package)
            manifest["relationships"].append({"spdxElementId": root, "relationshipType": "DEPENDS_ON",
                                              "relatedSpdxElement": identifier})
            additions.append(purl(key))
        else:
            package = indexed[key]
        if key[0] == "npm":
            record = {"package": purl(key), "license": metadata["license"],
                      "source": metadata["input"], "sha256": digest(source / metadata["input"])}
        else:
            record = nuget[key]
        package["licenseDeclared"] = record["license"]
        evidence.append(record)
    return additions, evidence


def check_references(manifest):
    if manifest.get("spdxVersion") != "SPDX-2.2":
        raise ValueError("Expected SPDX-2.2")
    identifiers = [manifest["SPDXID"]] + [x["SPDXID"] for x in manifest.get("packages", [])]
    identifiers += [x["SPDXID"] for x in manifest.get("files", [])]
    if len(set(identifiers)) != len(identifiers):
        raise ValueError("Duplicate SPDX identifiers")
    for relation in manifest.get("relationships", []):
        if not {relation["spdxElementId"], relation["relatedSpdxElement"]}.issubset(identifiers):
            raise ValueError("Dangling SPDX relationship reference")


def check_scan(report, packages):
    found = set()
    for result in report.get("Results", []):
        ecosystem = result.get("Type")
        if ecosystem not in {"npm", "nuget"}:
            continue
        for entry in result.get("Packages", []):
            name = entry["Name"]
            found.add((ecosystem, name.lower() if ecosystem == "nuget" else name, entry["Version"]))
    missing = packages.keys() - found
    if missing:
        raise ValueError(f"Incomplete vulnerability scan coverage: {len(missing)} locked components missing")


def scan_dependencies(cache, source, report, log):
    # Local Trivy filters/configuration must not turn a complete inventory into a clean-looking report.
    environment = {key: value for key, value in os.environ.items() if not key.startswith("TRIVY_")}
    run([cache / "trivy", "fs", "--config", "", "--ignorefile", "",
         "--scanners", "vuln", "--include-dev-deps", "--list-all-pkgs",
         "--severity", "UNKNOWN,LOW,MEDIUM,HIGH,CRITICAL", "--ignore-unfixed=false",
         "--ignore-status", "", "--exit-code", "0", "--format", "json",
         "--output", report, "--cache-dir", cache / "trivy-cache", source], log, environment)


def summary(report, provenance):
    unique = {}
    for result in report["Results"]:
        for finding in result.get("Vulnerabilities", []):
            key = (result["Type"], finding["PkgName"], finding["InstalledVersion"], finding["VulnerabilityID"])
            unique[key] = finding
    counts = Counter(x.get("Severity", "UNKNOWN") for x in unique.values())
    scan_time = datetime.fromisoformat(report["CreatedAt"]).astimezone(timezone.utc).isoformat().replace("+00:00", "Z")
    lines = ["# Source dependency vulnerability report", "",
             f"Scan completed successfully at **{scan_time}** using Trivy **{TRIVY_VERSION}**.", "",
             f"Source commit: `{provenance['sourceCommit']}`. Exact dependency input hashes are in `provenance.json`.", "",
             "Scope: tracked npm and NuGet lockfiles, including transitive, development, test, peer, and optional dependencies.",
             "Native libraries, container OS packages/base images, external services, and deployed artifacts were not scanned.", "",
             "## Results", "", f"**{len(unique)} unique package/version/advisory findings.** Repeated occurrences across projects are deduplicated.", "",
             "| Severity | Count |", "| --- | ---: |"]
    lines += [f"| {severity} | {counts[severity]} |" for severity in ("CRITICAL", "HIGH", "MEDIUM", "LOW", "UNKNOWN")]
    lines += ["", "| Package | Version | Advisory | Severity | Fixed versions reported |", "| --- | --- | --- | --- | --- |"]
    for key, finding in sorted(unique.items()):
        url = finding.get("PrimaryURL") or ("https://nvd.nist.gov/vuln/detail/" + key[3])
        lines.append(f"| {key[0]}: {key[1]} | {key[2]} | [{key[3]}]({url}) | {finding['Severity']} | {finding.get('FixedVersion') or 'Not supplied'} |")
    lines += ["", "Database snapshot:", "", "```json", json.dumps(provenance["vulnerabilityDatabase"], indent=2), "```", "",
              "All severities and unfixed findings are retained. No packages were upgraded or findings suppressed.",
              "Fixed versions are scanner suggestions, not tested upgrade recommendations. Assess runtime reachability and compatibility before remediation.",
              "Zero findings would mean no matches within this scan's inputs and database snapshot; it would not establish zero risk.", ""]
    return "\n".join(lines)


def verify(root, output=None):
    output = output or root / OUTPUT
    provenance = json.loads((output / "provenance.json").read_text())
    verify_inputs(root, provenance["inputSha256"])
    if set(provenance["inputSha256"]) != {p.as_posix() for p in input_files(root)}:
        raise ValueError("Tracked dependency input set changed; regenerate the SBOM")
    for name, expected in provenance["artifactSha256"].items():
        path = output / name
        if digest(path) != expected:
            raise ValueError(f"Artifact checksum mismatch: {name}")
        if b"\r" in path.read_bytes():
            raise ValueError(f"Artifact is not LF-normalized: {name}")
    path = output / MANIFEST
    if digest(path) != path.with_suffix(".json.sha256").read_text().strip():
        raise ValueError("Manifest checksum mismatch")
    manifest = json.loads(path.read_text())
    check_references(manifest)
    packages = locked_packages(root, provenance["inputSha256"])
    if packages.keys() - {package_key(x) for x in manifest["packages"]}:
        raise ValueError("Manifest is missing locked components")
    check_scan(json.loads((output / provenance["scanReport"]).read_text()), packages)
    print(f"Verified SPDX references, LF/checksums, {len(packages)} locked components, scan coverage, and current input hashes.")


def update(root, cache):
    tools(cache)
    environment = dict(os.environ, DOTNET_BUNDLE_EXTRACT_BASE_DIR=str(cache / "dotnet-extract"))
    with tempfile.TemporaryDirectory(prefix="nordiska-sbom-") as temporary:
        work = Path(temporary)
        source, scan, output, drop = (work / name for name in ("source", "scan", "output", "drop"))
        for path in (source, output, drop):
            path.mkdir()
        inputs = {}
        for relative in input_files(root):
            dest = source / relative
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(root / relative, dest)
            inputs[relative.as_posix()] = digest(dest)
        packages = locked_packages(source)
        if not packages:
            raise ValueError("No locked dependencies found")
        shutil.copytree(source, scan)
        derived = source / "_derived_nuget/packages.config"
        derived.parent.mkdir()
        config = ET.Element("packages")
        for key, metadata in sorted(packages.items()):
            if key[0] == "nuget":
                ET.SubElement(config, "package", id=metadata["name"], version=key[2], targetFramework="net8.0")
        ET.ElementTree(config).write(derived, encoding="utf-8", xml_declaration=True)
        print("Generating SPDX inventory and looking up license metadata...", flush=True)
        run([cache / "sbom-tool", "generate", "-b", drop, "-bc", source, "-m", output,
             "-pn", "Nordiska-Sparbanken", "-pv", "2.0.0", "-ps", "Nordiska",
             "-nsb", "https://nordiska.local/sbom", "-mi", "SPDX:2.2",
             "-li", "true", "-pm", "true", "-lto", "120"], work / "generate.log", environment)
        manifest = json.loads((output / MANIFEST).read_text())
        additions, evidence = reconcile(manifest, packages, source)
        # Generated source paths are temporary; retain stable input references instead.
        for package in manifest["packages"]:
            if "sourceInfo" in package:
                package["sourceInfo"] = package["sourceInfo"].replace(str(source), "tracked dependency inputs")
        write_json(output / MANIFEST, manifest)
        (output / MANIFEST).with_suffix(".json.sha256").write_bytes((digest(output / MANIFEST) + "\n").encode())
        print("Validating the generated SPDX inventory...", flush=True)
        run([cache / "sbom-tool", "validate", "-b", drop, "-m", output / "_manifest",
             "-o", work / "validation.json", "-mi", "SPDX:2.2"], work / "validate.log", environment)
        print("Scanning locked dependencies (all severities, including development dependencies)...", flush=True)
        report_path = work / "vulnerabilities.json"
        scan_dependencies(cache, scan, report_path, work / "scan.log")
        report = json.loads(report_path.read_text())
        check_scan(report, packages)
        report["ArtifactName"] = "tracked dependency manifests and lockfiles"
        date = datetime.fromisoformat(report["CreatedAt"]).astimezone(timezone.utc).date().isoformat()
        report_name = f"vulnerabilities-{date}.json"
        write_json(output / report_name, report)
        provenance = {
            "generatedAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
            "sourceCommit": git(root, "rev-parse", "HEAD"), "inputSha256": inputs,
            "scope": "Source npm/NuGet lockfile dependencies, including development, test, peer and optional entries; excludes native libraries, OS/base images, external services and built file integrity",
            "tools": {"Microsoft.SBOMTool": {"version": SBOM_VERSION, "sha256": digest(cache / "sbom-tool")},
                      "Trivy": {"version": TRIVY_VERSION, "sha256": digest(cache / "trivy")}},
            "scanReport": report_name,
            "vulnerabilityDatabase": json.loads((cache / "trivy-cache/db/metadata.json").read_text()),
            "lockedComponentCounts": dict(Counter(k[0] for k in packages)),
            "supplementedFromLockfiles": additions,
            "licenseDeclaredCounts": dict(sorted(Counter(p.get("licenseDeclared", "NOASSERTION") for p in manifest["packages"]).items())),
            "licenseEvidence": evidence,
        }
        (output / f"vulnerabilities-{date}.md").write_bytes(summary(report, provenance).encode())
        artifacts = [p for p in output.rglob("*") if p.is_file()]
        provenance["artifactSha256"] = {p.relative_to(output).as_posix(): digest(p) for p in sorted(artifacts)}
        write_json(output / "provenance.json", provenance)
        verify(root, output)
        destination = root / OUTPUT
        for path in sorted(output.rglob("*")):
            if path.is_file():
                target = destination / path.relative_to(output)
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(path, target)
        print(f"Updated {destination.relative_to(root)}; no dependencies or Git history changed.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("update", "verify"))
    parser.add_argument("root", nargs="?", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--tools-dir", type=Path, default=Path(tempfile.gettempdir()) / "nordiska-sbom-tools")
    args = parser.parse_args()
    try:
        if args.action == "update":
            update(args.root.resolve(), args.tools_dir.resolve())
        else:
            verify(args.root.resolve())
    except (OSError, ValueError, RuntimeError, subprocess.CalledProcessError) as error:
        print(f"SBOM {args.action} failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
