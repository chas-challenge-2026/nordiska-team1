#!/usr/bin/env python3
"""Validate actual Compose interpolation and moved paths without running services."""
import json
import os
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[3]
infra = root / "infra"
(root / "tmp").mkdir(exist_ok=True)
with tempfile.TemporaryDirectory(dir=root / "tmp") as temporary:
    env = Path(temporary) / ".env"
    env.write_text("\n".join(f"{key}=isolated-config-test" for key in (
        "POSTGRES_BOOTSTRAP_PASSWORD", "NORDISKA_MIGRATOR_PASSWORD", "NORDISKA_API_PASSWORD",
        "NORDISKA_REPORTING_WORKER_PASSWORD", "NORDISKA_JWT_SECRET", "NORDISKA_AUDIT_SIGNING_KEY",
        "PDF_SIGNER_PKCS11_PIN", "PDF_SIGNER_PKCS11_SO_PIN")))
    tool_override = Path(temporary) / "compose.override.yml"
    template = (root / "tools/syntheticDB/compose.override.yml").read_text()
    for override in ("docker-compose.override.yml", "docker-compose.frontend-local.yml"):
        app_service = "app"
        tool_override.write_text(template)
        for database, volume in (("nordiska_v2", "nordiska-v2_report_documents"),
                                 ("nordiska_synthetic", "nordiska-v2_synthetic_report_documents")):
            environment = dict(os.environ, NORDISKA_DATABASE=database, NORDISKA_REPORT_DOCUMENTS_VOLUME=volume)
            result = subprocess.run([
                "docker", "compose", "--project-name", "nordiska-v2", "--project-directory", str(infra),
                "--env-file", str(env), "-f", str(infra / "docker-compose.yml"), "-f", str(infra / override),
                "-f", str(tool_override), "--profile", "full-docker", "config", "--format", "json"
            ], env=environment, capture_output=True, text=True, check=True)
            config = json.loads(result.stdout)
            services = config["services"]
            assert app_service in services
            assert config["volumes"]["report_documents"]["name"] == volume
            for name in (app_service, "reporting-worker", "pdf-signer"):
                assert f"Database={database};" in services[name]["environment"]["ConnectionStrings__ReportingDatabase"]
                assert any(mount["source"] == "report_documents" for mount in services[name]["volumes"])
                assert Path(services[name]["build"]["context"]).resolve() == root
            for mount in services["db"]["volumes"]:
                if mount["type"] == "bind":
                    assert Path(mount["source"]).exists(), mount["source"]
            if override == "docker-compose.frontend-local.yml":
                assert services[app_service]["build"]["target"] == "app"
                assert Path(services["frontend"]["build"]["context"]).resolve() == root
                assert app_service in services["frontend"]["depends_on"]
                assert any(mount["source"] == str(root / "frontend") for mount in services["frontend"]["volumes"])
print("PASS: real Compose paths, app service, both frontend workflows and isolated PDF volume selections")
