# Synthetic database manager

Prepare the local stack with [DevSetup](../../docs/local-development-setup.md), then run the tool **from the repository root**. The standalone Linux x86-64 binary is included; no Python or compilation is needed.

## Interactive setup

```bash
./synthetic-data-manager
```

In a terminal this opens an arrow-key menu. Choose create/replace or append, select a customer-count preset (50, 1,000, 10,000, 50,000, 100,000 or 1,000,000), or enter a custom count. Then choose CPU threads, reporting year, random seed and optional long transaction descriptions. The tool shows the plan and asks before generating. After generation it offers to switch the app to the synthetic database.

The menu also provides status, switching back, reset and delete. Esc or Ctrl+C exits a terminal menu. `--menu` explicitly opens the menu; redirected input uses numbered choices. With no arguments and redirected input, the tool shows status.

`./NOR_db_synthetic_data_creator` opens the same native tool.

## Set up 10,000 customers from the command line

```bash
./synthetic-data-manager --customers 10000 --seed 42 --year 2025 --threads 4 &&
./synthetic-data-manager --switch nordiska_synthetic &&
./synthetic-data-manager --status
```

This creates a separate database and connects the app, reporting worker and PDF signer to it. **It replaces previous synthetic data; your normal `nordiska_v2` database and its PDFs are preserved.** The synthetic database uses its own PDF volume.

Compose files are in `infra/`. Local secrets and selection stay in `infra/v2/.env`, which the tool passes explicitly to Compose. Switching recreates all three services, checks their running database settings and rolls back if the switch fails. The backend-only configuration for local frontend development is preserved when active.

The standard seed simulates customer activity during 2025:

- **10,000 customers and 15,436 accounts:** about 1.54 accounts per customer, ranging from 1 to 6.
- About **734,000 ledger entries** and **148 MB** of database storage before PDFs.
- Varied fictional names, generated personal numbers, balances, deposits, withdrawals, interest and tax entries.

These are benchmark fixtures: all accounts use Flex with 3.5% interest and a simplified monthly interest simulation. Different seeds change the population. App Development startup can add demo/admin records; `--status` shows actual counts and the database used by each running service.

## Return to the normal database

```bash
./synthetic-data-manager --switch nordiska_v2
```

The services reconnect to the normal database and its original PDF volume. Synthetic data and PDFs remain available for later use. Reset and delete affect only the synthetic database; they retain PDF files, and replacement seeding can leave old synthetic PDFs unreferenced. PDF cleanup belongs to the separate benchmark workflow.

Seeding prepares data; it does not submit jobs or generate PDF reports. Rebuild instructions, tests and additional options are in [maintainer notes](maintainer-notes.md).
