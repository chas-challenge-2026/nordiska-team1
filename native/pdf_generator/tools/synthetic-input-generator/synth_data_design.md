# Synthetic Input Data Generator — Design

## Purpose

Provide deterministic synthetic account data for benchmarking the tax-report PDF pipeline.

```text
tools/synthetic-input-generator/
```

Datasets are generated on demand and are not stored in the repository.

The goal is not to simulate a bank perfectly. The goal is to produce credible, repeatable workloads so PDF performance can be measured and compared.

## Requirements

* JSON-configurable.
* Deterministic through a random seed.
* One JSON input per account.
* Stateful account simulation.
* Swedish savings-account behavior is the default.
* Keep configuration minimal; add more knobs only when there is a concrete need.

## Default Workloads

### `uniform-500k`

* 10,000 accounts
* 500,000 customer transactions
* 50 transactions per account
* Control benchmark for ideal load balancing.

### `realistic-500k`

* 10,000 accounts
* 500,000 customer transactions
* Skewed/heavy-tailed transaction allocation.
* Most accounts have relatively few transactions; a small number have many.
* Primary benchmark.

This is intended to be credible rather than to claim an exact empirical model of Nordiska customers.

### `stress-skewed-500k`

* 10,000 accounts
* 500,000 customer transactions
* Deliberately extreme concentration of activity.
* Used to expose poor scheduling and load balancing.

## Determinism

The same:

```text
configuration + seed
```

must generate identical accounts, dates, amounts, transactions, and interest results.

The generator should emit a manifest containing:

* effective configuration
* seed
* account count
* customer transaction count
* generated ledger-event count
* basic transaction-distribution statistics

## Account Simulation

Accounts are simulated chronologically.

Rules:

* Start balance is `0`.
* First customer event is a deposit.
* Deposits increase balance.
* Withdrawals cannot exceed available balance.
* Balance may reach `0`.
* Another deposit is required before further withdrawals.
* Opening deposits should generally be larger than ordinary deposits, but this is probabilistic rather than guaranteed.

Initial ledger types:

```text
DEPOSIT
WITHDRAWAL
INTEREST_CREDIT
TAX_WITHHOLDING
```

The configured transaction count refers to customer deposits and withdrawals.

Interest accrual calculations are not ledger transactions.

## Interest

Interest accrues daily from the applicable account balance and is periodically credited.

```text
balance changes
    ↓
daily interest accrual
    ↓
periodic INTEREST_CREDIT
```

There is no need to simulate a particular clock time. Interest can be calculated efficiently from balance intervals.

Default Swedish behavior:

```text
currency: SEK
annual interest rate: configurable
interest accrual: daily
interest credit: monthly at month-end
interest withholding tax: 30%
```

Gross interest and withholding remain separate:

```text
INTEREST_CREDIT   +1,000.00 SEK
TAX_WITHHOLDING     -300.00 SEK
net balance effect  +700.00 SEK
```

The report input must retain gross credited interest and tax withheld separately.

## Minimal Configuration

The public configuration surface should stay small:

```json
{
  "seed": 827461,
  "year": 2025,

  "accounts": 10000,
  "transactions": 500000,
  "distribution": "realistic",

  "currency": "SEK",
  "annual_interest_rate": 0.025
}
```

Swedish defaults such as daily accrual, monthly crediting and 30% withholding remain implementation defaults for now.

If another jurisdiction or account product is later required, those rules can be promoted into configuration rather than designing the full abstraction in advance.

## Output

```text
generated/
├── manifest.json
├── account-000001.json
├── account-000002.json
└── ...
```

Each account JSON should contain at minimum:

* account identifier
* currency
* reporting year
* annual interest rate
* chronological ledger transactions
* gross interest credited
* tax withheld
* ending balance

## Benchmark Workloads

The requirement workload is:

```text
10,000 reports
500,000 customer transactions
```

The benchmark should also scale this workload to demonstrate headroom:

| Scale | Accounts | Transactions |
| ----- | -------: | -----------: |
| 0.1×  |    1,000 |       50,000 |
| 0.5×  |    5,000 |      250,000 |
| 1×    |   10,000 |      500,000 |
| 2×    |   20,000 |    1,000,000 |
| 5×    |   50,000 |    2,500,000 |
| 10×   |  100,000 |    5,000,000 |

This allows claims such as:

* meets the required workload
* handles 2× / 5× / 10× the required workload
* meets the requirement under a constrained CPU or memory budget

## Benchmark Modes

The same synthetic workload should support measuring:

1. **End-to-end**

   * read JSON from disk
   * parse input
   * generate PDFs
   * write PDFs to disk

2. **Generation only**

   * preload and parse all input
   * generate PDFs into memory
   * exclude disk I/O

3. **Output I/O only**

   * pre-generate PDF buffers
   * measure writing them to disk

4. **Input I/O only**

   * read and parse JSON
   * exclude PDF generation

This separates PDF-engine performance from storage performance.

## Reference Resource Constraints

Use a reproducible constrained environment for reference benchmarks.

Example:

```text
CPU allocation: 1 CPU
memory limit:   512 MB
workers:        1
build:          Release
seed:           fixed
```

Docker/Linux CPU quotas are suitable for limiting available CPU time. Do not describe a CPU quota as a specific clock speed such as a “1 GHz CPU”.

For every benchmark, record the actual host CPU model as well.

## Metrics

Primary metrics:

* elapsed wall-clock time
* PDFs per second
* transactions per second
* peak memory usage

Useful engineering metrics from Linux `perf`:

* CPU task time
* retired instructions
* CPU cycles
* IPC
* cache misses
* context switches

Wall-clock throughput remains the main user-facing performance result. Hardware counters are diagnostic and useful for comparing implementation changes.

## Reproducibility

A benchmark result should always identify:

```text
commit/build
compiler and build mode
host CPU
resource constraints
seed
workload profile
account count
transaction count
benchmark mode
```

Changing the benchmark implementation must not silently change the synthetic workload.
