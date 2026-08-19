# Synthetic Input Data Generator — Design

## Purpose

Provide deterministic synthetic account data for benchmarking the tax-report PDF pipeline.

```text
tools/synthetic-input-generator/
```

Datasets are generated on demand and are not stored in the repository.

## Requirements

* JSON-configurable.
* Deterministic through a random seed.
* One JSON input per account.
* Stateful account simulation.
* Configurable workload, account product, currency, interest rules, and tax jurisdiction.
* Swedish savings-account behavior is the default, not hard-coded.

## Default Workloads

**`uniform-500k`**

* 10,000 accounts
* 500,000 customer transactions
* 50 transactions/account
* Control benchmark for ideal load balancing.

**`realistic-500k`**

* 10,000 accounts
* 500,000 customer transactions
* Heavy-tailed transaction distribution.
* Primary benchmark.

**`stress-skewed-500k`**

* 10,000 accounts
* 500,000 customer transactions
* Deliberately extreme concentration of activity.
* Tests scheduling/load-balancing behavior.

## Determinism

The same:

```text
configuration + seed
```

must generate identical accounts, dates, amounts, transactions, and interest results.

Emit a manifest containing the effective configuration, seed, counts, and distribution statistics.

## Account Simulation

Accounts are simulated chronologically.

* Start balance: `0`.
* First customer event: deposit.
* Deposits increase balance.
* Withdrawals cannot exceed available balance.
* Balance may reach `0`.
* Another deposit is required before further withdrawals.
* Opening deposits should generally be larger, but probabilistically rather than by invariant.

Initial ledger types:

```text
DEPOSIT
WITHDRAWAL
INTEREST_CREDIT
TAX_WITHHOLDING
```

The configured customer-transaction count refers to deposits and withdrawals. Daily interest accrual is calculation work, not ledger transactions.

## Interest

Interest accrues daily from the applicable account balance and is periodically credited.

```text
balance changes
    ↓
daily interest accrual
    ↓
periodic INTEREST_CREDIT
```

Default Swedish benchmark configuration:

```json
{
  "currency": "SEK",
  "interest": {
    "annual_rate": 0.025,
    "accrual_frequency": "daily",
    "day_count_convention": "ACTUAL_ACTUAL",
    "credit_frequency": "monthly",
    "credit_day": "month_end"
  }
}
```

There is no need to model a particular clock time. The implementation can calculate interest efficiently from balance intervals.

## Swedish Tax Default

```text
jurisdiction: SE
interest withholding: 30%
```

Gross interest and withholding remain separate:

```text
INTEREST_CREDIT   +1,000.00 SEK
TAX_WITHHOLDING     -300.00 SEK
net balance effect  +700.00 SEK
```

The PDF/reporting input must retain gross credited interest and tax withheld separately.

## Configuration Structure

```json
{
  "seed": 827461,

  "workload": {
    "accounts": 10000,
    "customer_transactions": 500000,
    "distribution": "realistic"
  },

  "account_product": {
    "currency": "SEK",
    "interest": {
      "annual_rate": 0.025,
      "accrual_frequency": "daily",
      "day_count_convention": "ACTUAL_ACTUAL",
      "credit_frequency": "monthly",
      "credit_day": "month_end"
    }
  },

  "jurisdiction": {
    "country": "SE",
    "interest_withholding_rate": 0.30
  }
}
```

Workload generation, account-product behavior, and jurisdiction rules should remain independent.

## Output

```text
generated/
├── manifest.json
├── account-000001.json
├── account-000002.json
└── ...
```

Each account should contain at minimum:

* account identifier
* currency
* reporting year
* interest configuration
* chronological ledger transactions
* gross interest credited
* tax withheld
* ending balance

## Benchmark Use

The same generated workload should support measuring:

* disk input → PDF generation → disk output
* preloaded input → PDFs generated in RAM
* pre-generated PDF buffers → disk writes
* input reading/parsing only

Changing the benchmark implementation must not silently change the synthetic workload.
