# Synthetic data for local V2

Prepare the local V2 stack with [DevSetup](../../docs/local-development-setup.md), then run these commands **from the repository root**. The tool is included for Linux x86-64; no extra installation or compilation is needed.

## Set up 10,000 customers

```bash
./synthetic-data-manager --customers 10000 --seed 42 --year 2025 --threads 4 &&
./synthetic-data-manager --switch nordiska_synthetic &&
./synthetic-data-manager --status
```

This creates a separate benchmark database and connects the local API, worker and PDF signer to it. **It replaces previous synthetic data; your normal `nordiska_v2` database is preserved.**

The tool simulates customer activity with different account counts, balances, deposits, withdrawals, interest and tax during 2025. The standard dataset contains:

- **10,000 customers and 15,436 accounts:** about **1.54 accounts per customer**, ranging from 1 to 6.
- About **734,000 ledger entries** and **148 MB** of database storage before generating PDFs.

These are synthetic benchmark fixtures: accounts use the same Flex product and customer names are placeholders. Different seeds change the population; API startup may add demo records.

## Return to the normal database

```bash
./synthetic-data-manager --switch nordiska_v2
```

This reconnects the local services to your normal database. The synthetic database stays available for later use.

Rebuild instructions, tests and additional options are in [maintainer notes](maintainer-notes.md). Seeding prepares data; it does not generate PDF reports.
