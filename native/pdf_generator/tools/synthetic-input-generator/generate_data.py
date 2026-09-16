#!/usr/bin/env python3
"""Generate deterministic synthetic tax-report input files.

The generator deliberately uses one deterministic process.  This keeps the
dataset independent of filesystem iteration order and makes byte-for-byte
reproduction straightforward.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import random
import shutil
from collections import Counter
from datetime import date, timedelta
from pathlib import Path
from statistics import mean, median


ROOT = Path(__file__).resolve().parent
DEFAULT_CONFIG = ROOT / "config" / "generator.json"
PROFILE_DIR = ROOT / "config" / "workloads"


def stable_seed(seed: int, account_index: int) -> int:
    value = f"{seed}:{account_index}".encode("ascii")
    return int.from_bytes(hashlib.sha256(value).digest()[:8], "big")


def load_json(path: Path) -> dict:
    with path.open(encoding="utf-8") as stream:
        value = json.load(stream)
    if not isinstance(value, dict):
        raise ValueError(f"configuration root must be an object: {path}")
    return value


def resolve_config(path: Path, profile_override: str | None = None) -> dict:
    run = load_json(path)
    profile_name = profile_override or run.get("workload", "realistic")
    profile_path = PROFILE_DIR / f"{profile_name}.json"
    profile = load_json(profile_path)
    config = {**profile, **run}
    config["profile"] = profile_name
    config["source_config"] = str(path)
    return config


def allocate_counts(config: dict) -> list[int]:
    accounts = int(config["accounts"])
    total = int(config["transactions"])
    if accounts < 1 or total < accounts:
        raise ValueError("transactions must be at least the number of accounts")

    distribution = config["distribution"]
    kind = distribution["type"]
    if kind == "uniform":
        counts = [total // accounts] * accounts
        for index in range(total % accounts):
            counts[index] += 1
        return counts

    seed = int(config["seed"])
    if kind == "stress-skewed":
        active = max(1, round(accounts * float(distribution["active_account_fraction"])))
        active_transactions = round(total * float(distribution["active_transaction_fraction"]))
        weights = [0.0] * accounts
        rng = random.Random(stable_seed(seed, 0))
        for index in range(active):
            weights[index] = 1.0 + rng.random()
        inactive = accounts - active
        active_extra = max(0, active_transactions - active)
        inactive_extra = total - active_transactions - inactive
        active_counts = [1 + count for count in largest_remainder(active_extra, weights[:active])]
        inactive_counts = [1 + inactive_extra // inactive] * inactive
        for index in range(inactive_extra % inactive):
            inactive_counts[index] += 1
        return active_counts + inactive_counts

    if kind == "lognormal":
        rng = random.Random(seed)
        weights = [rng.lognormvariate(float(distribution.get("mean", 0.0)),
                                      float(distribution.get("sigma", 1.35)))
                   for _ in range(accounts)]
        return [1 + count for count in largest_remainder(total - accounts, weights)]

    raise ValueError(f"unsupported distribution type: {kind}")


def largest_remainder(total: int, weights: list[float]) -> list[int]:
    weight_sum = sum(weights)
    if weight_sum <= 0:
        raise ValueError("distribution weights must have a positive sum")
    raw = [total * weight / weight_sum for weight in weights]
    counts = [math.floor(value) for value in raw]
    remainder = total - sum(counts)
    order = sorted(range(len(weights)), key=lambda i: (-(raw[i] - counts[i]), i))
    for index in order[:remainder]:
        counts[index] += 1
    return counts


def random_amount(rng: random.Random, opening: bool, balance: int) -> int:
    if opening:
        return rng.randint(50_000, 500_000)
    if balance == 0 or rng.random() < 0.58:
        return rng.randint(5_000, 150_000)
    return -rng.randint(1, min(balance, 120_000))


def generate_account(config: dict, account_index: int, customer_count: int) -> dict:
    year = int(config["year"])
    currency = config["currency"]
    rate = float(config["annual_interest_rate"])
    tax_rate = float(config["tax_withholding_rate"])
    rng = random.Random(stable_seed(int(config["seed"]), account_index))
    first_day = date(year, 1, 1)
    last_day = date(year, 12, 31)
    span = (last_day - first_day).days
    days = sorted(rng.randint(0, span) for _ in range(customer_count))

    balance = 0
    accrued = 0.0
    gross_interest = 0
    tax_withheld = 0
    ledger: list[dict] = []
    customer_events: dict[date, list[tuple[int, int]]] = {}
    for event_index, offset in enumerate(days):
        event_date = first_day + timedelta(days=offset)
        amount = random_amount(rng, event_index == 0, balance)
        if amount < 0 and -amount > balance:
            amount = rng.randint(5_000, 150_000)
        balance += amount
        customer_events.setdefault(event_date, []).append((event_index, amount))

    transaction_id = 0
    current = first_day
    while current <= last_day:
        for event_index, amount in customer_events.get(current, []):
            ledger.append({
                "transaction_id": f"account-{account_index:06d}-customer-{event_index:06d}",
                "date": current.isoformat(),
                "type": "DEPOSIT" if amount > 0 else "WITHDRAWAL",
                "currency": currency,
                "amount_minor": amount,
            })
            transaction_id += 1

        accrued += balance * rate / (366 if (year % 4 == 0 and (year % 100 != 0 or year % 400 == 0)) else 365)
        next_day = current + timedelta(days=1)
        if current.month != next_day.month or current == last_day:
            credit = round(accrued)
            accrued -= credit
            if credit:
                tax = round(credit * tax_rate)
                ledger.append({"transaction_id": f"account-{account_index:06d}-interest-{transaction_id:06d}", "date": current.isoformat(), "type": "INTEREST_CREDIT", "currency": currency, "amount_minor": credit})
                ledger.append({"transaction_id": f"account-{account_index:06d}-tax-{transaction_id:06d}", "date": current.isoformat(), "type": "TAX_WITHHOLDING", "currency": currency, "amount_minor": -tax})
                balance += credit - tax
                gross_interest += credit
                tax_withheld += tax
                transaction_id += 2
        current = next_day

    ledger.sort(key=lambda event: (event["date"], event["transaction_id"]))
    return {
        "account_id": f"account-{account_index:06d}",
        "account_number": f"SE{account_index:010d}",
        "currency": currency,
        "reporting_year": year,
        "annual_interest_rate": rate,
        "transactions": ledger,
        "gross_interest_credited": gross_interest,
        "tax_withheld": tax_withheld,
        "ending_balance": balance,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--config", type=Path, default=DEFAULT_CONFIG)
    parser.add_argument("--profile", help="workload profile name, overriding the run config")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--clean", action="store_true", help="remove the selected output directory first")
    args = parser.parse_args()
    config = resolve_config(args.config.resolve(), args.profile)
    output = (args.output or Path(config.get("output_directory", "generated"))).resolve()
    if args.clean and output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True, exist_ok=True)

    counts = allocate_counts(config)
    stats = Counter()
    total_ledger_events = 0
    total_bytes = 0
    for account_index, customer_count in enumerate(counts, 1):
        account = generate_account(config, account_index, customer_count)
        path = output / f"account-{account_index:06d}.json"
        encoded = (json.dumps(account, indent=2, sort_keys=True) + "\n").encode("utf-8")
        path.write_bytes(encoded)
        total_bytes += len(encoded)
        total_ledger_events += len(account["transactions"])
        stats[customer_count] += 1

    ordered = sorted(counts)
    percentile = lambda fraction: ordered[min(len(ordered) - 1, math.ceil(fraction * len(ordered)) - 1)]
    manifest = {
        "seed": config["seed"],
        "profile": config["profile"],
        "effective_config": config,
        "accounts": len(counts),
        "configured_customer_transactions": config["transactions"],
        "actual_customer_transactions": sum(counts),
        "total_ledger_events": total_ledger_events,
        "customer_transactions_per_account": {
            "min": min(counts), "median": median(counts), "mean": mean(counts),
            "p90": percentile(0.90), "p99": percentile(0.99), "max": max(counts)
        },
        "total_generated_input_bytes": total_bytes,
    }
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"Generated {len(counts)} accounts and {sum(counts)} customer transactions in {output}")


if __name__ == "__main__":
    main()
