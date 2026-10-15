# Synthetic database manager

Prepare the local stack with [DevSetup](../../docs/local-development-setup.md), then run the tool **from the repository root**. The standalone Linux x86-64 binary is included; no Python or compilation is needed.

## Interactive setup

```bash
./tools/syntheticDB/nordiska-synthetic-linux-x86_64
```

The title shows the active database; the table shows both databases' customer, account, transaction and document counts and size. The switch action follows the running app: **Switch to SYNTHETIC DB** or **Switch back to NORMAL DB**. Select it to switch immediately; a spinner stays active through service recreation and verification.

Create synthetic data appears when the synthetic database is missing. Choose **100**, **1000**, **10k**, **100k**, **1m**, **10m**, **MAX (37,620,000)** or a validated custom amount, then edit the seed if desired and select **Start creation**. Other settings are available through CLI flags. Creation stays in the foreground and shows committed customers, saved transactions, generated/transferred counts and an approximate completion estimate.

To replace an existing dataset, choose **Delete synthetic DB and return to NORMAL**, confirm deletion, then create again. Deletion moves services back to NORMAL before removing the database and retains PDF volumes. **Delete synthetic documents** separately removes synthetic PDFs, document metadata and PDF jobs while retaining financial data and normal PDFs; it requires confirmation and is disabled when nothing needs cleaning.

Actions follow the current database and application state. Use Up/Down and Enter; Esc exits the main screen or returns from creation and login. Selection is preserved where practical after operations.

`--menu` explicitly opens the menu; redirected input uses numbered choices. With no arguments and redirected input, the tool shows detailed status. The interactive menu has no append action.

The menu uses subtle colors for selection, active environment and operation results. Set `NO_COLOR=1` to disable them; redirected output and `TERM=dumb` stay uncolored.

## Set up 10,000 customers from the command line

```bash
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --customers 10000 --seed 42 --year 2025 --threads 4 &&
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --switch nordiska_synthetic &&
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --status
```

This creates a separate database and connects the app, reporting worker and PDF signer to it. **It replaces previous synthetic data; your normal `nordiska_v2` database and its PDFs are preserved.** The synthetic database uses its own PDF volume.

For creation of 10,000 customers or more, the tool keeps the initial 500-customer sample fully indexed, then rebuilds eligible ledger query indexes after bulk loading. Primary keys and foreign keys remain enabled. Completion includes index rebuilding, sequence updates and database statistics. Append and smaller runs keep all indexes. Use `--keep-indexes` to compare the original ingestion path. See [measured results](maintainer-notes.md#performance-measurements).

Compose files are in `infra/`. Local secrets and selection stay in `infra/.env` (legacy `infra/v2/.env` is also supported), which the tool passes explicitly to Compose. Switching recreates all three services, checks their running database settings and rolls back if the switch fails. Database switching uses the tool’s `compose.override.yml` without changing shared infrastructure files. The backend-only configuration for local frontend development is preserved when active.

The standard seed simulates customer activity during 2025:

- **10,000 customers and 15,436 accounts:** about 1.54 accounts per customer, ranging from 1 to 6.
- About **734,000 ledger entries** and **148 MB** of database storage before PDFs.
- Varied fictional names, generated personal numbers, balances, deposits, withdrawals, interest and tax entries.

These are benchmark fixtures: all accounts use Flex with a 3.5% annual rate, daily accrual and monthly payouts on the actual calendar month-end. Different seeds change the population.

## Get a customer login

```bash
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --get-random-customer
```

Prints a random eligible customer's name, email, password (`password123`), customer ID and personal number from the **running app's database**, labeled NORMAL or SYNTHETIC. The menu has the same action. Use the frontend's manual email/password login. The command never switches databases or resets passwords; an offline app or absence of eligible customers produces an error.

Generated customers must have savings accounts, the Customer role and the original fixture password hash. The seeded Anna/Erik demo accounts with empty hashes are also eligible when the app runs in Development. Locked and staff accounts are excluded. ASP.NET Identity may upgrade a generated customer's hash after login, making them ineligible for this lookup even though their password still works.

The synthetic database has the same schema and uses the same app features as the normal database. Its initial generated data covers customers, Flex savings accounts and ledger transactions. Loans, savings goals, inbox messages and report documents are not generated by this seed; those features can create their own records through the app.

## Return to the normal database

```bash
./tools/syntheticDB/nordiska-synthetic-linux-x86_64 --switch nordiska_v2
```

The services reconnect to the normal database and its original PDF volume. Synthetic data and PDFs remain available for later use. Reset and delete affect only the synthetic database; they retain PDF files, and replacement seeding can leave old synthetic PDFs unreferenced. Use **Delete synthetic documents** (or `--delete-documents`) to reclaim retained synthetic PDF storage.

Seeding prepares data; it does not submit jobs or generate PDF reports. Rebuild instructions, tests and additional options are in [maintainer notes](maintainer-notes.md).
