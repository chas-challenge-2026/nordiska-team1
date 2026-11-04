"""Offline checks for the SBOM evidence checks and failure handling."""
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch


class EvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        script = Path(__file__).with_name("sbom.py")
        if not script.exists():
            raise AssertionError("SBOM tooling has not been implemented")
        spec = importlib.util.spec_from_file_location("sbom", script)
        cls.sbom = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.sbom)

    def test_changed_input_invalidates_provenance(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            path = root / "package-lock.json"
            path.write_text('{"packages": {}}\n')
            inputs = {"package-lock.json": self.sbom.digest(path)}
            self.sbom.verify_inputs(root, inputs)
            path.write_text('{"packages": {"new": {}}}\n')
            with self.assertRaisesRegex(ValueError, "changed"):
                self.sbom.verify_inputs(root, inputs)

    def test_scan_must_cover_both_ecosystems_even_with_zero_findings(self):
        source = {("npm", "react", "18.3.1"): {}, ("nuget", "npgsql", "8.0.6"): {}}
        empty = {"Results": []}
        with self.assertRaisesRegex(ValueError, "coverage"):
            self.sbom.check_scan(empty, source)
        complete = {"Results": [
            {"Type": "npm", "Packages": [{"Name": "react", "Version": "18.3.1"}]},
            {"Type": "nuget", "Packages": [{"Name": "Npgsql", "Version": "8.0.6"}]},
        ]}
        self.sbom.check_scan(complete, source)

    def test_failed_command_does_not_replace_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            dest = Path(directory) / "existing.json"
            dest.write_text("previous valid evidence\n")
            with self.assertRaises(Exception):
                self.sbom.run(["python3", "-c", "raise SystemExit(2)"], Path(directory) / "run.log")
            self.assertEqual(dest.read_text(), "previous valid evidence\n")

    def test_dangling_spdx_relationship_is_rejected(self):
        manifest = {"spdxVersion": "SPDX-2.2", "SPDXID": "SPDXRef-DOCUMENT",
                    "packages": [{"SPDXID": "SPDXRef-root"}],
                    "relationships": [{"spdxElementId": "SPDXRef-root",
                                       "relatedSpdxElement": "SPDXRef-missing"}]}
        with self.assertRaisesRegex(ValueError, "reference"):
            self.sbom.check_references(manifest)

    def test_only_recorded_lockfiles_are_inventory_inputs(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "frontend").mkdir()
            (root / "frontend/package-lock.json").write_text(json.dumps({"packages": {
                "node_modules/react": {"version": "18.3.1", "license": "MIT"}}}))
            (root / "node_modules").mkdir()
            (root / "node_modules/package-lock.json").write_text("{}")
            packages = self.sbom.locked_packages(root, ["frontend/package-lock.json"])
            self.assertEqual(set(packages), {("npm", "react", "18.3.1")})

    def test_report_displays_scan_time_in_utc(self):
        report = {"CreatedAt": "2026-11-04T00:30:00+01:00", "Results": []}
        provenance = {"sourceCommit": "example", "vulnerabilityDatabase": {}}
        text = self.sbom.summary(report, provenance)
        self.assertIn("2026-11-03T23:30:00Z", text)

    def test_scan_ignores_ambient_filters_and_includes_all_severities(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            fake = root / "trivy"
            fake.write_text("#!/usr/bin/env python3\nimport json,os,sys\n"
                            "print(json.dumps({'args':sys.argv[1:], 'filter':os.environ.get('TRIVY_SEVERITY')}))\n")
            fake.chmod(0o700)
            log = root / "scan.log"
            with patch.dict(os.environ, {"TRIVY_SEVERITY": "HIGH,CRITICAL", "TRIVY_IGNORE_UNFIXED": "true"}):
                self.sbom.scan_dependencies(root, root, root / "report.json", log)
            invocation = json.loads(log.read_text())
            self.assertIsNone(invocation["filter"])
            args = invocation["args"]
            self.assertEqual(args[args.index("--severity") + 1], "UNKNOWN,LOW,MEDIUM,HIGH,CRITICAL")
            self.assertEqual(args[args.index("--config") + 1], "")
            self.assertEqual(args[args.index("--ignorefile") + 1], "")
            self.assertIn("--ignore-unfixed=false", args)

    def test_update_preserves_published_evidence_when_scan_fails(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "package-lock.json").write_text(json.dumps({"packages": {
                "node_modules/react": {"version": "18.3.1", "license": "MIT"}}}))
            previous = root / self.sbom.OUTPUT / self.sbom.MANIFEST
            previous.parent.mkdir(parents=True)
            previous.write_text("previous evidence\n")

            def external_tool(command, log, env=None):
                args = [str(x) for x in command]
                if args[1] == "generate":
                    output = Path(args[args.index("-m") + 1])
                    self.sbom.write_json(output / self.sbom.MANIFEST, {
                        "spdxVersion": "SPDX-2.2", "SPDXID": "SPDXRef-DOCUMENT",
                        "documentDescribes": ["SPDXRef-root"],
                        "packages": [{"SPDXID": "SPDXRef-root"}], "relationships": []})
                elif args[1] == "fs":
                    raise RuntimeError("simulated scanner failure")

            with patch.object(self.sbom, "tools"), \
                    patch.object(self.sbom, "input_files", return_value=[Path("package-lock.json")]), \
                    patch.object(self.sbom, "run", side_effect=external_tool):
                with self.assertRaisesRegex(RuntimeError, "simulated scanner failure"):
                    self.sbom.update(root, root / "tools")
            self.assertEqual(previous.read_text(), "previous evidence\n")
            self.assertEqual([p for p in (root / self.sbom.OUTPUT).rglob("*") if p.is_file()], [previous])


if __name__ == "__main__":
    unittest.main()
