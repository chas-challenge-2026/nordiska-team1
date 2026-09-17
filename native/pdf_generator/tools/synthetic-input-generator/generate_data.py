#!/usr/bin/env python3
"""Generate deterministic synthetic customer batch JSON files for Nordiska PDF Generator.

Each file represents an atomic customer batch containing one or more documents
(account statements and annual tax reports) for full year 2025, matching the
canonical schema expected by JsonIngestor and the C ABI.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import random
import shutil
from datetime import date, timedelta
from pathlib import Path


ROOT = Path(__file__).resolve().parent
DEFAULT_OUTPUT = ROOT / "generated" / "pool_100"

FIRST_NAMES = [
    "Anna", "Erik", "Karin", "Johan", "Maria", "Karl", "Sara", "Anders",
    "Emma", "Mikael", "Astrid", "Per", "Elin", "Lars", "Linnea", "Fredrik",
    "Ida", "Gustav", "Maja", "Daniel", "Sofia", "Magnus", "Hanna", "Oskar",
    "Clara", "Niklas", "Ebba", "Alexander", "Agnes", "Stefan", "Ingrid", "Henrik",
    "Alma", "Viktor", "Svea", "Emil", "Signe", "Christian", "Alva", "Andreas"
]

LAST_NAMES = [
    "Andersson", "Johansson", "Karlsson", "Nilsson", "Eriksson", "Larsson",
    "Olsson", "Persson", "Svensson", "Gustafsson", "Pettersson", "Jonsson",
    "Jansson", "Hansson", "Bengtsson", "Jönsson", "Lindqvist", "Lindgren",
    "Bergström", "Axelsson", "Lundberg", "Forsberg", "Sandberg", "Ekström",
    "Hedlund", "Holm", "Nyström", "Öberg", "Söderberg", "Nordström"
]

DEPOSIT_LABELS = [
    "Löneinsättning", "Swish från familj", "Överföring sparkonto", "Barnbidrag",
    "Skatteåterbäring", "Insättning", "Swish inbetalning", "Räntegottgörelse"
]

WITHDRAWAL_LABELS = [
    "Kortköp ICA Supermarket", "Kortköp Coop", "Hyra", "Apoteket Hjärtat",
    "Pressbyrån", "Swish betalning", "Klarna månadsfaktura", "SL Månadskort",
    "Ellevio AB", "Restaurangbesök", "Systembolaget", "Gymkort SATS",
    "Bensinstation Circle K", "Streamingabonnemang", "Överföring"
]

ACCOUNT_TEMPLATES = [
    {"name": "Sparkonto", "rate": 0.0275},
    {"name": "Privatkonto", "rate": 0.0},
    {"name": "Buffertspar", "rate": 0.0250},
    {"name": "Fasträntekonto 1 år", "rate": 0.0350},
    {"name": "Semesterfond", "rate": 0.0225},
    {"name": "Barnsparande", "rate": 0.0250},
    {"name": "Målsparande", "rate": 0.0280},
    {"name": "Trygghetskonto", "rate": 0.0300},
]


def stable_seed(master_seed: int, index: int) -> int:
    data = f"{master_seed}:{index}".encode("ascii")
    return int.from_bytes(hashlib.sha256(data).digest()[:8], "big")


def luhn_checksum(digits: str) -> int:
    total = 0
    for i, char in enumerate(reversed(digits)):
        d = int(char)
        if i % 2 == 0:
            d *= 2
            if d > 9:
                d -= 9
        total += d
    return (10 - (total % 10)) % 10


def generate_personnummer(rng: random.Random) -> str:
    # Birth between 1955 and 2004
    year = rng.randint(1955, 2004)
    month = rng.randint(1, 12)
    day = rng.randint(1, 28)
    individual = rng.randint(100, 999)
    # Luhn check is based on YYMMDD + individual
    digits_for_luhn = f"{year % 100:02d}{month:02d}{day:02d}{individual:03d}"
    check = luhn_checksum(digits_for_luhn)
    return f"{year:04d}{month:02d}{day:02d}-{individual:03d}{check}"


def format_sek(amount_minor: int, show_sign: bool = False) -> str:
    sign = ""
    if amount_minor > 0 and show_sign:
        sign = "+"
    elif amount_minor < 0:
        sign = "-"

    abs_minor = abs(amount_minor)
    whole = abs_minor // 100
    cents = abs_minor % 100
    whole_str = f"{whole:,}".replace(",", " ")
    return f"{sign}{whole_str},{cents:02d} SEK"


def pareto_sample(rng: random.Random, x_min: float, alpha: float, x_max: float) -> int:
    u = rng.random()
    val = x_min / ((1.0 - u) ** (1.0 / alpha))
    return int(min(x_max, max(x_min, math.floor(val))))


def generate_customer_batch(master_seed: int, customer_index: int, year: int = 2025) -> dict:
    rng = random.Random(stable_seed(master_seed, customer_index))

    first_name = rng.choice(FIRST_NAMES)
    last_name = rng.choice(LAST_NAMES)
    customer_name = f"{first_name} {last_name}"
    person_num = generate_personnummer(rng)

    # Pareto-distributed account count: min=1, max=10, alpha=1.85
    account_count = pareto_sample(rng, x_min=1.0, alpha=1.85, x_max=10.0)

    documents = []
    total_batch_transactions = 0

    first_day = date(year, 1, 1)
    last_day = date(year, 12, 31)
    days_in_year = (last_day - first_day).days + 1

    for acct_idx in range(account_count):
        account_number = f"NKM-{customer_index:04d}-{acct_idx + 1:02d}"
        tpl = ACCOUNT_TEMPLATES[acct_idx % len(ACCOUNT_TEMPLATES)]
        account_name = tpl["name"]
        interest_rate = tpl["rate"]

        # Pareto-distributed transactions per account: min=8, max=600, alpha=1.4
        tx_count = pareto_sample(rng, x_min=8.0, alpha=1.40, x_max=600.0)
        total_batch_transactions += tx_count

        # Initial opening balance (between 5,000 SEK and 150,000 SEK)
        opening_minor = rng.randint(500_000, 15_000_000)
        current_balance = opening_minor

        # Spaced events throughout the year
        event_offsets = sorted(rng.randint(0, days_in_year - 1) for _ in range(tx_count))

        transactions = []
        for offset in event_offsets:
            tx_date = (first_day + timedelta(days=offset)).isoformat()

            # Withdrawals only if balance allows
            is_deposit = (current_balance < 300_000) or (rng.random() < 0.45)
            if is_deposit:
                amount_minor = rng.randint(20_000, 3_500_000)  # 200 SEK - 35,000 SEK
                tx_type = "deposit"
                desc = rng.choice(DEPOSIT_LABELS)
                current_balance += amount_minor
            else:
                # Max withdrawal is 70% of current balance
                max_withdrawal = max(10_000, int(current_balance * 0.70))
                amount_minor = rng.randint(10_000, min(1_500_000, max_withdrawal))
                tx_type = "withdrawal"
                desc = rng.choice(WITHDRAWAL_LABELS)
                current_balance -= amount_minor

            transactions.append({
                "date": tx_date,
                "type": tx_type,
                "description": desc,
                "currency": "SEK",
                "amount_minor": amount_minor if is_deposit else -amount_minor,
                "amount_display": format_sek(amount_minor if is_deposit else -amount_minor, show_sign=True),
                "balance_after_display": format_sek(current_balance, show_sign=False)
            })

        closing_minor = current_balance

        # 1. Add account_statement document
        documents.append({
            "document_id": f"statement_{account_number}",
            "kind": "account_statement",
            "version": "1.0",
            "document": {
                "title": "Kontoutdrag",
                "account_number": account_number,
                "account_name": account_name,
                "currency": "SEK",
                "period": f"{year}-01-01 - {year}-12-31",
                "opening_balance": format_sek(opening_minor),
                "closing_balance": format_sek(closing_minor),
                "transactions": transactions
            }
        })

        # 2. Add annual_tax_report document if interest applies
        if interest_rate > 0:
            average_balance = (opening_minor + closing_minor) / 2.0
            interest_earned_minor = round(average_balance * interest_rate)
            tax_withheld_minor = round(interest_earned_minor * 0.30)

            documents.append({
                "document_id": f"tax_{account_number}",
                "kind": "annual_tax_report",
                "version": "1.0",
                "document": {
                    "title": "Kontrolluppgift för ränteinkomst",
                    "tax_year": str(year),
                    "account_number": account_number,
                    "account_name": account_name,
                    "total_interest_earned": format_sek(interest_earned_minor),
                    "preliminary_tax_deducted": format_sek(tax_withheld_minor),
                    "reported_to_authority": "Skatteverket (KU20)"
                }
            })

    return {
        "$schema_version": "1.0",
        "customer_id": customer_index,
        "customer_name": customer_name,
        "created_at": f"{year + 1}-01-15T08:00:00Z",
        "documents": documents
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate realistic 2025 customer batch pool.")
    parser.add_argument("--customers", type=int, default=100, help="Number of customer batches to generate (default: 100)")
    parser.add_argument("--seed", type=int, default=827461, help="Deterministic master seed (default: 827461)")
    parser.add_argument("--year", type=int, default=2025, help="Reporting year (default: 2025)")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT, help="Output directory for JSON pool")
    parser.add_argument("--clean", action="store_true", help="Clean output directory first")
    args = parser.parse_args()

    output_dir = args.output.resolve()
    if args.clean and output_dir.exists():
        shutil.rmtree(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    print(f"Generating {args.customers} customer batches for year {args.year} (seed={args.seed})...")

    total_docs = 0
    total_statements = 0
    total_tax_reports = 0
    total_transactions = 0
    total_bytes = 0

    account_counts = []
    tx_counts_per_account = []

    for idx in range(1, args.customers + 1):
        batch = generate_customer_batch(args.seed, idx, args.year)
        file_path = output_dir / f"customer-{idx:06d}.json"
        content = (json.dumps(batch, indent=2, ensure_ascii=False) + "\n").encode("utf-8")
        file_path.write_bytes(content)

        docs = batch["documents"]
        total_docs += len(docs)
        total_bytes += len(content)

        stmts = [d for d in docs if d["kind"] == "account_statement"]
        taxes = [d for d in docs if d["kind"] == "annual_tax_report"]
        total_statements += len(stmts)
        total_tax_reports += len(taxes)
        account_counts.append(len(stmts))

        for stmt in stmts:
            txs = len(stmt["document"].get("transactions", []))
            total_transactions += txs
            tx_counts_per_account.append(txs)

    manifest = {
        "seed": args.seed,
        "year": args.year,
        "customer_count": args.customers,
        "total_documents": total_docs,
        "total_statements": total_statements,
        "total_tax_reports": total_tax_reports,
        "total_transactions": total_transactions,
        "total_bytes": total_bytes,
        "accounts_per_customer": {
            "min": min(account_counts),
            "max": max(account_counts),
            "avg": round(sum(account_counts) / len(account_counts), 2)
        },
        "transactions_per_account": {
            "min": min(tx_counts_per_account),
            "max": max(tx_counts_per_account),
            "avg": round(sum(tx_counts_per_account) / len(tx_counts_per_account), 2)
        }
    }

    manifest_path = output_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    print("\nGeneration Complete:")
    print(f"  Customers:            {args.customers}")
    print(f"  Total Documents:      {total_docs} ({total_statements} statements, {total_tax_reports} tax reports)")
    print(f"  Total Transactions:   {total_transactions}")
    print(f"  Accounts / Customer:  min={manifest['accounts_per_customer']['min']}, max={manifest['accounts_per_customer']['max']}, avg={manifest['accounts_per_customer']['avg']}")
    print(f"  Tx / Account:         min={manifest['transactions_per_account']['min']}, max={manifest['transactions_per_account']['max']}, avg={manifest['transactions_per_account']['avg']}")
    print(f"  Total JSON Size:      {total_bytes / 1024 / 1024:.2f} MB")
    print(f"  Output Directory:     {output_dir}")


if __name__ == "__main__":
    main()
