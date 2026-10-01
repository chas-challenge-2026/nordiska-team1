#!/usr/bin/env python3
"""Synthetic Data Manager CLI & Navigable TUI for Nordiska Sparbanken.

Provides a resize-proof, arrow-key navigable terminal UI with live progress
reporting and automated flags for managing synthetic retail banking data.
"""

from __future__ import annotations

import argparse
import os
import signal
import sys
import time
import subprocess
from pathlib import Path
from typing import List, Optional, Callable

# Ensure tools/synthetic is in sys.path
SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from tools.synthetic.database import DockerDatabaseManager
from tools.synthetic.generator import SyntheticDatasetGenerator

NATIVE_BIN_PATH = SCRIPT_DIR / "bin" / "nordiska-synthetic-gen"

# ANSI formatting
RESET = "\033[0m"
BOLD = "\033[1m"
GREEN = "\033[32m"
CYAN = "\033[36m"
YELLOW = "\033[33m"
RED = "\033[31m"
DIM = "\033[2m"
CLEAR_SCREEN = "\033[H\033[J"
HIDE_CURSOR = "\033[?25l"
SHOW_CURSOR = "\033[?25h"


def print_banner(mgr: DockerDatabaseManager) -> None:
    active_db = mgr.get_active_database_env()
    db_color = GREEN if active_db == "nordiska_synthetic" else YELLOW

    print(f"{BOLD}Nordiska Sparbanken — Synthetic Data Manager{RESET}")
    print(f"Active database: {db_color}{BOLD}{active_db}{RESET}")

    stats = mgr.get_synthetic_stats()
    if stats.exists:
        print(f"Synthetic database: {GREEN}{stats.customer_count:,} customers, {stats.account_count:,} accounts, {stats.ledger_count:,} transactions ({stats.size_pretty}){RESET}")
    else:
        print(f"Synthetic database: {DIM}Not created (deleted / uninitialized){RESET}")
    print("─" * 60)


def display_status(mgr: DockerDatabaseManager) -> None:
    active_db = mgr.get_active_database_env()
    print(f"\n{BOLD}Database Status:{RESET}")
    db_color = GREEN if active_db == "nordiska_synthetic" else YELLOW
    print(f"  Target Application DB: {db_color}{BOLD}{active_db}{RESET}")

    try:
        v2_stats = mgr.run_psql(
            "nordiska_v2",
            'SELECT count(*) FROM banking.customers; SELECT count(*) FROM banking.savings_accounts; SELECT count(*) FROM banking.ledger_entries;',
        ).split()
        print(f"  nordiska_v2 (base):       {v2_stats[0]} customers, {v2_stats[1]} accounts, {v2_stats[2]} transactions")
    except Exception as ex:
        print(f"  nordiska_v2 (base):       {RED}Error ({ex}){RESET}")

    stats = mgr.get_synthetic_stats()
    if stats.exists:
        print(
            f"  nordiska_synthetic:        {GREEN}{stats.customer_count:,} customers, "
            f"{stats.account_count:,} accounts, {stats.ledger_count:,} transactions ({stats.size_pretty}){RESET}"
        )
    else:
        print(f"  nordiska_synthetic:        {DIM}Not created (deleted / uninitialized){RESET}")


def navigable_menu(
    prompt: str,
    options: List[str],
    default_idx: int = 0,
    header_fn: Optional[Callable[[], None]] = None,
) -> int:
    """Resize-proof arrow-key navigable menu with fallback to numbered input."""
    if not sys.stdin.isatty():
        if header_fn:
            header_fn()
        print(f"\n{BOLD}{prompt}{RESET}")
        for idx, opt in enumerate(options, 1):
            print(f"  {idx}) {opt}")
        try:
            choice = input(f"Choose [1-{len(options)}]: ").strip()
            return int(choice) - 1
        except (EOFError, KeyboardInterrupt):
            print("\nExiting.")
            sys.exit(0)
        except Exception:
            return default_idx

    try:
        import tty
        import termios
    except ImportError:
        if header_fn:
            header_fn()
        print(f"\n{BOLD}{prompt}{RESET}")
        for idx, opt in enumerate(options, 1):
            print(f"  {idx}) {opt}")
        try:
            choice = input(f"Choose [1-{len(options)}]: ").strip()
            return int(choice) - 1
        except Exception:
            return default_idx

    selected = default_idx
    fd = sys.stdin.fileno()
    old_settings = termios.tcgetattr(fd)
    resized = False

    def handle_sigwinch(signum, frame):
        nonlocal resized
        resized = True

    old_winch_handler = None
    if hasattr(signal, "SIGWINCH"):
        old_winch_handler = signal.signal(signal.SIGWINCH, handle_sigwinch)

    def redraw():
        sys.stdout.write(CLEAR_SCREEN)
        if header_fn:
            header_fn()
        print(f"\n{BOLD}{prompt}{RESET} {DIM}(↑ / ↓ to navigate, Enter to select){RESET}")
        for idx, opt in enumerate(options):
            if idx == selected:
                print(f"  {CYAN}{BOLD}❯ {opt}{RESET}")
            else:
                print(f"    {DIM}{opt}{RESET}")
        sys.stdout.flush()

    try:
        sys.stdout.write(HIDE_CURSOR)
        sys.stdout.flush()
        tty.setraw(fd)
        redraw()

        while True:
            if resized:
                resized = False
                redraw()

            try:
                ch1 = sys.stdin.read(1)
            except (InterruptedError, OSError):
                # Interrupted by window resize signal
                redraw()
                continue

            if ch1 in ("\r", "\n"):
                break
            elif ch1 == "\x03":  # Ctrl+C
                sys.stdout.write(SHOW_CURSOR)
                sys.stdout.flush()
                termios.tcsetattr(fd, termios.TCSADRAIN, old_settings)
                print("\nCancelled.")
                sys.exit(0)
            elif ch1 == "\x1b":  # Escape sequence
                try:
                    ch2 = sys.stdin.read(1)
                except (InterruptedError, OSError):
                    redraw()
                    continue

                if ch2 == "[":
                    try:
                        ch3 = sys.stdin.read(1)
                    except (InterruptedError, OSError):
                        redraw()
                        continue

                    if ch3 == "A":  # Up
                        selected = (selected - 1) % len(options)
                        redraw()
                    elif ch3 == "B":  # Down
                        selected = (selected + 1) % len(options)
                        redraw()
    finally:
        sys.stdout.write(SHOW_CURSOR)
        sys.stdout.flush()
        if old_winch_handler and hasattr(signal, "SIGWINCH"):
            signal.signal(signal.SIGWINCH, old_winch_handler)
        termios.tcsetattr(fd, termios.TCSADRAIN, old_settings)

    print()
    return selected


def run_native_seed(
    mgr: DockerDatabaseManager,
    customers: int,
    threads: int,
    seed: int,
    year: int,
    stress_text: bool,
    append: bool,
) -> None:
    start_cust_id, start_acct_id, start_tx_id = 1, 1, 1

    if not append or not mgr.synthetic_db_exists():
        print(f"\n{YELLOW}Resetting nordiska_synthetic (cloning schema from nordiska_v2)...{RESET}")
        t0_reset = time.time()
        mgr.reset_synthetic_database()
        print(f"{GREEN}✓ Ready in {time.time() - t0_reset:.2f}s{RESET}\n")
    else:
        max_c, max_a, max_l = mgr.get_max_ids()
        start_cust_id = max_c + 1
        start_acct_id = max_a + 1
        start_tx_id = max_l + 1
        print(f"\n{YELLOW}Appending to existing data (starting at customer ID {start_cust_id})...{RESET}\n")

    if NATIVE_BIN_PATH.exists() and os.access(NATIVE_BIN_PATH, os.X_OK):
        cmd = [
            str(NATIVE_BIN_PATH),
            "--customers", str(customers),
            "--threads", str(threads),
            "--seed", str(seed),
            "--year", str(year),
            "--start-cust-id", str(start_cust_id),
            "--start-acct-id", str(start_acct_id),
            "--start-tx-id", str(start_tx_id),
            "--container", mgr.container_name,
            "--db", "nordiska_synthetic",
        ]
        if stress_text:
            cmd.append("--stress-text")

        proc = subprocess.run(cmd)
        if proc.returncode != 0:
            print(f"{RED}Native generator failed with exit code {proc.returncode}{RESET}")
    else:
        print(f"{YELLOW}Native binary not found, falling back to Python generator...{RESET}")
        existing_personal_nums = mgr.get_existing_personal_nums() if append else None
        gen = SyntheticDatasetGenerator(master_seed=seed, year=year, stress_text=stress_text)
        cust_list, acct_list, tx_list = gen.generate(
            customer_count=customers,
            start_customer_id=start_cust_id,
            start_account_id=start_acct_id,
            start_ledger_id=start_tx_id,
            existing_personal_nums=existing_personal_nums,
        )
        mgr.bulk_load(cust_list, acct_list, tx_list)

    stats = mgr.get_synthetic_stats()
    print(
        f"\n{GREEN}{BOLD}✓ Complete! nordiska_synthetic now has "
        f"{stats.customer_count:,} customers, {stats.account_count:,} accounts, "
        f"{stats.ledger_count:,} transactions ({stats.size_pretty}){RESET}"
    )


def switch_database_flow(mgr: DockerDatabaseManager, target_db: str) -> None:
    if target_db == "nordiska_synthetic" and not mgr.synthetic_db_exists():
        print(f"{RED}Error: Cannot switch to nordiska_synthetic because it does not exist. Seed it first.{RESET}")
        return

    print(f"\n{YELLOW}Switching target database to {target_db} in infra/.env and restarting app container...{RESET}")
    mgr.set_active_database_env(target_db)
    mgr.restart_app_container()
    print(f"{GREEN}✓ Active database is now {target_db}. App container restarted.{RESET}")


def run_tui(mgr: DockerDatabaseManager) -> None:
    hw_cores = os.cpu_count() or 4

    def banner():
        print_banner(mgr)

    while True:
        active_db = mgr.get_active_database_env()
        switch_target = "nordiska_synthetic" if active_db == "nordiska_v2" else "nordiska_v2"

        main_options = [
            "Seed database",
            f"Switch active database to {switch_target}",
            "Reset database (empty all synthetic data)",
            "Delete synthetic database (free disk space)",
            "Restart application container",
            "Show database status",
            "Exit",
        ]

        action_idx = navigable_menu("Action:", main_options, default_idx=0, header_fn=banner)

        if action_idx == 6:  # Exit
            print("Done.")
            sys.exit(0)
        elif action_idx == 5:  # Status
            display_status(mgr)
            input("\nPress Enter to continue...")
        elif action_idx == 4:  # Restart container
            print(f"\n{YELLOW}Restarting nordiska-app container...{RESET}")
            mgr.restart_app_container()
            print(f"{GREEN}✓ App container restarted.{RESET}")
            input("\nPress Enter to continue...")
        elif action_idx == 1:  # Switch DB
            switch_database_flow(mgr, switch_target)
            input("\nPress Enter to continue...")
        elif action_idx == 2:  # Reset DB
            confirm = input(f"{YELLOW}Delete all synthetic records and reset to empty schema? [y/N]: {RESET}").strip().lower()
            if confirm == "y":
                mgr.reset_synthetic_database()
                print(f"{GREEN}✓ Database reset to clean schema.{RESET}")
            input("\nPress Enter to continue...")
        elif action_idx == 3:  # Delete DB
            confirm = input(f"{RED}Drop database nordiska_synthetic and free disk space? [y/N]: {RESET}").strip().lower()
            if confirm == "y":
                mgr.delete_synthetic_database()
                if mgr.get_active_database_env() == "nordiska_synthetic":
                    mgr.set_active_database_env("nordiska_v2")
                    mgr.restart_app_container()
                    print(f"{YELLOW}✓ Switched active database back to nordiska_v2.{RESET}")
                print(f"{GREEN}✓ Database nordiska_synthetic deleted.{RESET}")
            input("\nPress Enter to continue...")
        elif action_idx == 0:  # Seed database
            # Customer count options (default: 10,000)
            customer_options = [
                "10,000 customers (default, ~650k transactions)",
                "50,000 customers (~3.3M transactions)",
                "100,000 customers (~6.5M transactions)",
                "1,000,000 customers (~65M transactions)",
                "1,000 customers (~65k transactions)",
                "50 customers (~3.4k transactions)",
                "Custom number...",
            ]
            c_idx = navigable_menu("Customers to generate:", customer_options, default_idx=0, header_fn=banner)

            if c_idx == 0:
                cust_count = 10000
            elif c_idx == 1:
                cust_count = 50000
            elif c_idx == 2:
                cust_count = 10000
            elif c_idx == 3:
                cust_count = 1000000
            elif c_idx == 4:
                cust_count = 1000
            elif c_idx == 5:
                cust_count = 50
            else:
                try:
                    c_in = input(f"{BOLD}Enter customer count [10000]: {RESET}").strip()
                    cust_count = int(c_in) if c_in else 10000
                except (ValueError, EOFError):
                    cust_count = 10000

            # CPU cores prompt (default: all)
            try:
                core_in = input(f"{BOLD}CPU cores to use [{hw_cores}]: {RESET}").strip()
                threads = int(core_in) if core_in else hw_cores
                threads = max(1, min(hw_cores, threads))
            except (ValueError, EOFError):
                threads = hw_cores

            # Mode prompt
            mode_options = [
                "Replace (clear synthetic database first)",
                "Append (keep existing synthetic data)",
            ]
            mode_idx = navigable_menu("Mode:", mode_options, default_idx=0, header_fn=banner)
            append = mode_idx == 1

            # Workload summary
            est_txs = cust_count * 65
            est_disk_mb = int(est_txs * 0.00016)

            print(f"\n{BOLD}Plan:{RESET}")
            print(f"  Target:     nordiska_synthetic")
            print(f"  Customers:  {cust_count:,} (~{est_txs:,} transactions, ~{est_disk_mb:,} MB)")
            print(f"  Cores:      {threads} threads")
            print(f"  Mode:       {'Append' if append else 'Replace'}")

            try:
                confirm = input(f"\n{BOLD}Proceed? [Y/n]: {RESET}").strip().lower()
            except (EOFError, KeyboardInterrupt):
                confirm = "n"

            if confirm and confirm != "y":
                print("Cancelled.")
                input("\nPress Enter to continue...")
                continue

            run_native_seed(
                mgr=mgr,
                customers=cust_count,
                threads=threads,
                seed=42,
                year=2026,
                stress_text=False,
                append=append,
            )
            input("\nPress Enter to return to menu...")


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Nordiska Synthetic Banking Data Manager"
    )
    parser.add_argument("--menu", "--tui", dest="tui", action="store_true", help="Launch interactive menu")
    parser.add_argument("--status", action="store_true", help="Show database status")
    parser.add_argument(
        "--switch",
        choices=["nordiska_v2", "nordiska_synthetic"],
        help="Switch active database in infra/.env and restart app container",
    )
    parser.add_argument("--reset", action="store_true", help="Reset synthetic database to clean empty schema")
    parser.add_argument("--delete", "--drop", dest="delete", action="store_true", help="Delete synthetic database")
    parser.add_argument("--customers", type=int, default=10000, help="Customer count (default: 10000)")
    parser.add_argument("--threads", type=int, help="CPU threads (default: all available cores)")
    parser.add_argument("--seed", type=int, default=42, help="Seed (default: 42)")
    parser.add_argument("--year", type=int, default=2026, help="Year (default: 2026)")
    parser.add_argument("--stress-text", action="store_true", help="Varied string lengths and symbols")
    parser.add_argument("--append", action="store_true", help="Append without resetting database")
    parser.add_argument("-y", "--yes", action="store_true", help="Skip confirmation prompt")

    args = parser.parse_args()
    mgr = DockerDatabaseManager()

    has_flags = any([
        args.status,
        args.switch,
        args.reset,
        args.delete,
        "--customers" in sys.argv,
    ])

    if args.tui or (not has_flags and sys.stdin.isatty()):
        run_tui(mgr)
        return

    if not has_flags:
        display_status(mgr)
        return

    if args.status:
        display_status(mgr)

    if args.reset:
        mgr.reset_synthetic_database()
        print(f"{GREEN}✓ nordiska_synthetic reset to clean schema.{RESET}")

    if "--customers" in sys.argv:
        threads = args.threads or (os.cpu_count() or 4)
        run_native_seed(
            mgr=mgr,
            customers=args.customers,
            threads=threads,
            seed=args.seed,
            year=args.year,
            stress_text=args.stress_text,
            append=args.append,
        )

    if args.switch:
        switch_database_flow(mgr, args.switch)

    if args.delete:
        mgr.delete_synthetic_database()
        if mgr.get_active_database_env() == "nordiska_synthetic":
            mgr.set_active_database_env("nordiska_v2")
            mgr.restart_app_container()
            print(f"{YELLOW}✓ Switched active database back to nordiska_v2.{RESET}")
        print(f"{GREEN}✓ Database nordiska_synthetic deleted.{RESET}")


if __name__ == "__main__":
    main()
