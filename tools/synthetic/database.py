"""Database management and bulk ingestion for NOR_db_synthetic_data_creator.

Handles:
- Detecting Docker PostgreSQL container
- Querying status of nordiska_v2 and nordiska_synthetic
- Reading and modifying NORDISKA_DATABASE in infra/.env
- Non-blocking schema clone via pg_dump from nordiska_v2 into nordiska_synthetic
- High-performance PostgreSQL COPY streaming ingestion
- Sequence resetting and role mapping (Customer role in AspNetUserRoles)
- Instant deletion of nordiska_synthetic
"""

from __future__ import annotations

import io
import os
import re
import subprocess
from dataclasses import dataclass
from pathlib import Path
import sys
from typing import List, Optional, Tuple

# Dynamically locate repo root by walking up until 'infra/docker-compose.yml' is found
def _find_repo_root() -> Path:
    curr = Path(__file__).resolve().parent
    for _ in range(5):
        if (curr / "infra" / "docker-compose.yml").exists():
            return curr
        curr = curr.parent
    return Path.cwd()

REPO_ROOT = _find_repo_root()
INFRA_DIR = REPO_ROOT / "infra"
INFRA_ENV_PATH = INFRA_DIR / ".env"
COMPOSE_FILE = INFRA_DIR / "docker-compose.yml"

from .generator import SyntheticAccount, SyntheticCustomer, SyntheticLedgerEntry


@dataclass
class DatabaseStats:
    exists: bool
    customer_count: int = 0
    account_count: int = 0
    ledger_count: int = 0
    size_pretty: str = "0 MB"


class DockerDatabaseManager:
    """Manages Docker PostgreSQL instances, schema cloning, and streaming COPY bulk ingestion."""

    def __init__(self, container_name: str = "nordiska-db-1"):
        self.container_name = container_name
        self._ensure_container_running()

    def _ensure_container_running(self) -> None:
        """Find the active Postgres container name if nordiska-db-1 isn't standard."""
        try:
            output = subprocess.check_output(
                ["docker", "ps", "--filter", "ancestor=postgres:15-bookworm", "--format", "{{.Names}}"],
                text=True,
            ).strip()
            if output:
                self.container_name = output.splitlines()[0]
        except Exception:
            pass

    def run_psql(self, dbname: str, sql: str, stdin_data: Optional[str] = None) -> str:
        """Execute SQL in the PostgreSQL container as bootstrap user."""
        cmd = [
            "docker", "exec", "-i", self.container_name,
            "psql", "-U", "nordiska_bootstrap", "-d", dbname,
            "-v", "ON_ERROR_STOP=1", "-A", "-t", "-c", sql,
        ]
        proc = subprocess.run(cmd, input=stdin_data, text=True, capture_output=True)
        if proc.returncode != 0:
            raise RuntimeError(f"psql failed: {proc.stderr.strip() or proc.stdout.strip()}")
        return proc.stdout.strip()

    def get_active_database_env(self) -> str:
        """Read the currently configured active database from infra/.env."""
        if not INFRA_ENV_PATH.exists():
            return "nordiska_v2"
        content = INFRA_ENV_PATH.read_text(encoding="utf-8")
        match = re.search(r"^\s*NORDISKA_DATABASE\s*=\s*([^\s#]+)", content, re.MULTILINE)
        if match:
            return match.group(1).strip()
        return "nordiska_v2"

    def set_active_database_env(self, target_db: str) -> None:
        """Update NORDISKA_DATABASE in infra/.env."""
        content = ""
        if INFRA_ENV_PATH.exists():
            content = INFRA_ENV_PATH.read_text(encoding="utf-8")
        if re.search(r"^\s*NORDISKA_DATABASE\s*=", content, re.MULTILINE):
            content = re.sub(
                r"^\s*NORDISKA_DATABASE\s*=.*$",
                f"NORDISKA_DATABASE={target_db}",
                content,
                flags=re.MULTILINE,
            )
        else:
            content = f"NORDISKA_DATABASE={target_db}\n" + content
        INFRA_ENV_PATH.write_text(content, encoding="utf-8")

    def restart_app_container(self) -> None:
        """Restart the app container so it re-reads connection strings."""
        subprocess.run(
            ["docker", "compose", "-f", str(COMPOSE_FILE), "restart", "app"],
            cwd=str(INFRA_DIR),
            check=False,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
        )

    def synthetic_db_exists(self) -> bool:
        """Check if nordiska_synthetic database exists."""
        try:
            res = self.run_psql("postgres", "SELECT 1 FROM pg_database WHERE datname = 'nordiska_synthetic';")
            return res == "1"
        except Exception:
            return False

    def get_synthetic_stats(self) -> DatabaseStats:
        """Get customer count, account count, ledger count, and disk size."""
        if not self.synthetic_db_exists():
            return DatabaseStats(exists=False)
        try:
            sql = """
            SELECT 
                (SELECT count(*) FROM banking.customers) || '|' ||
                (SELECT count(*) FROM banking.savings_accounts) || '|' ||
                (SELECT count(*) FROM banking.ledger_entries) || '|' ||
                pg_size_pretty(pg_database_size('nordiska_synthetic'));
            """
            res = self.run_psql("nordiska_synthetic", sql)
            parts = res.split("|")
            return DatabaseStats(
                exists=True,
                customer_count=int(parts[0]),
                account_count=int(parts[1]),
                ledger_count=int(parts[2]),
                size_pretty=parts[3],
            )
        except Exception:
            return DatabaseStats(exists=True)

    def get_existing_personal_nums(self) -> set[str]:
        """Get set of existing PersonalNums in nordiska_synthetic."""
        if not self.synthetic_db_exists():
            return set()
        try:
            sql = 'SELECT "PersonalNum" FROM banking.customers;'
            res = self.run_psql("nordiska_synthetic", sql)
            if not res:
                return set()
            return set(res.splitlines())
        except Exception:
            return set()

    def get_max_ids(self) -> Tuple[int, int, int]:
        """Get (max_customer_id, max_account_id, max_ledger_id) for appending data."""
        if not self.synthetic_db_exists():
            return 0, 0, 0
        sql = """
        SELECT 
            COALESCE((SELECT MAX("Id") FROM banking.customers), 0) || '|' ||
            COALESCE((SELECT MAX("Id") FROM banking.savings_accounts), 0) || '|' ||
            COALESCE((SELECT MAX("Id") FROM banking.ledger_entries), 0);
        """
        res = self.run_psql("nordiska_synthetic", sql)
        parts = res.split("|")
        return int(parts[0]), int(parts[1]), int(parts[2])

    def reset_synthetic_database(self) -> None:
        """Create or recreate nordiska_synthetic as a clean clone of nordiska_v2 without locking connections."""
        terminate_sql = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'nordiska_synthetic';"
        self.run_psql("postgres", terminate_sql)
        self.run_psql("postgres", "DROP DATABASE IF EXISTS nordiska_synthetic;")
        self.run_psql("postgres", "CREATE DATABASE nordiska_synthetic OWNER nordiska_bootstrap;")

        dump_schema_cmd = (
            f"docker exec -i {self.container_name} pg_dump -s -U nordiska_bootstrap nordiska_v2 | "
            f"docker exec -i {self.container_name} psql -U nordiska_bootstrap -d nordiska_synthetic -q"
        )
        subprocess.run(dump_schema_cmd, shell=True, check=True)

        dump_ref_cmd = (
            f"docker exec -i {self.container_name} pg_dump -U nordiska_bootstrap -a -t 'banking.account_type_configs' "
            f"-t 'banking.\"AspNetRoles\"' -t 'faq.faq_entries' nordiska_v2 | "
            f"docker exec -i {self.container_name} psql -U nordiska_bootstrap -d nordiska_synthetic -q"
        )
        subprocess.run(dump_ref_cmd, shell=True, check=True)

        grant_sql = "GRANT CONNECT ON DATABASE nordiska_synthetic TO nordiska_migrator, nordiska_api, nordiska_reporting_worker;"
        self.run_psql("postgres", grant_sql)

    def delete_synthetic_database(self) -> None:
        """Drop nordiska_synthetic and free all space."""
        terminate_sql = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'nordiska_synthetic';"
        self.run_psql("postgres", terminate_sql)
        self.run_psql("postgres", "DROP DATABASE IF EXISTS nordiska_synthetic;")

    def bulk_load(
        self,
        customers: List[SyntheticCustomer],
        accounts: List[SyntheticAccount],
        ledger: List[SyntheticLedgerEntry],
    ) -> None:
        """Stream data into nordiska_synthetic using PostgreSQL COPY FROM STDIN."""
        cust_stream = io.StringIO()
        role_stream = io.StringIO()

        for c in customers:
            norm_email = c.email.upper()
            created_str = c.created_at.strftime("%Y-%m-%d %H:%M:%S+00")
            row = (
                f"{c.id}\t{c.personal_num}\t{c.name}\t{c.email}\t{norm_email}\t"
                f"{c.email}\t{norm_email}\t{c.phone_number}\t{c.password_hash}\t\t\tt\tf\tf\tf\t0\t{created_str}\n"
            )
            cust_stream.write(row)
            role_stream.write(f"{c.id}\t2\n")

        cust_sql = """COPY banking.customers (
            "Id", "PersonalNum", "Name", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
            "PhoneNumber", "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "EmailConfirmed",
            "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount", "CreatedAt"
        ) FROM STDIN WITH (FORMAT text, DELIMITER E'\\t');"""
        self.run_psql("nordiska_synthetic", cust_sql, stdin_data=cust_stream.getvalue())

        role_sql = """COPY banking."AspNetUserRoles" ("UserId", "RoleId") FROM STDIN WITH (FORMAT text, DELIMITER E'\\t');"""
        self.run_psql("nordiska_synthetic", role_sql, stdin_data=role_stream.getvalue())

        acct_stream = io.StringIO()
        for a in accounts:
            created_str = a.created_at.strftime("%Y-%m-%d %H:%M:%S+00")
            row = (
                f"{a.id}\t{a.customer_id}\t{a.account_number}\t{a.account_name}\t{a.account_type}\t"
                f"{a.balance:.2f}\t{a.interest_rate:.6f}\t{a.status}\t{created_str}\n"
            )
            acct_stream.write(row)

        acct_sql = """COPY banking.savings_accounts (
            "Id", "CustomerId", "AccountNumber", "AccountName", "AccountType",
            "Balance", "InterestRate", "Status", "CreatedAt"
        ) FROM STDIN WITH (FORMAT text, DELIMITER E'\\t');"""
        self.run_psql("nordiska_synthetic", acct_sql, stdin_data=acct_stream.getvalue())

        chunk_size = 50000
        for i in range(0, len(ledger), chunk_size):
            chunk = ledger[i : i + chunk_size]
            ledger_stream = io.StringIO()
            for e in chunk:
                created_str = e.created_at.strftime("%Y-%m-%d %H:%M:%S+00")
                clean_label = e.label.replace("\t", " ").replace("\n", " ").replace("\r", " ")
                row = (
                    f"{e.id}\t{e.account_id}\t{e.entry_type}\t{e.amount:.2f}\t"
                    f"{clean_label}\t\\N\tf\t{created_str}\n"
                )
                ledger_stream.write(row)

            ledger_sql = """COPY banking.ledger_entries (
                "Id", "AccountId", "Type", "Amount", "Label", "TargetAccountId", "IsPlanned", "CreatedAt"
            ) FROM STDIN WITH (FORMAT text, DELIMITER E'\\t');"""
            self.run_psql("nordiska_synthetic", ledger_sql, stdin_data=ledger_stream.getvalue())

        seq_sql = """
        SELECT setval(pg_get_serial_sequence('banking.customers', 'Id'), COALESCE((SELECT MAX("Id") FROM banking.customers), 1));
        SELECT setval(pg_get_serial_sequence('banking.savings_accounts', 'Id'), COALESCE((SELECT MAX("Id") FROM banking.savings_accounts), 1));
        SELECT setval(pg_get_serial_sequence('banking.ledger_entries', 'Id'), COALESCE((SELECT MAX("Id") FROM banking.ledger_entries), 1));
        """
        self.run_psql("nordiska_synthetic", seq_sql)
