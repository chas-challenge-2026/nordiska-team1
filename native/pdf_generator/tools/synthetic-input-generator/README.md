# Synthetic Customer Batch Generator

Standalone deterministic Python tool for generating realistic Swedish banking customer datasets for the Nordiska PDF Generator pipeline and benchmark harness.

```text
tools/synthetic-input-generator/
├── generate_data.py          # Generator script
├── README.md                 # Documentation
├── synth_data_design.md      # Architecture design notes
└── generated/
    └── pool_100/             # Pre-generated 100-customer benchmark pool
        ├── manifest.json     # Statistics and run parameters
        ├── customer-000001.json
        ├── customer-000002.json
        └── ...
```

---

## What It Generates

Each generated JSON file represents an **atomic customer batch** matching `$schema_version: "1.0"` as required by `nordiska::JsonIngestor` and the C ABI (`nordiska_pdf_v1_generate_customer_batch`):

1. **Full-Year Simulation**: Simulates chronological banking events across all of **2025** (`2025-01-01` to `2025-12-31`).
2. **Pareto Accounts per Customer**:
   - Accounts per customer follow a Pareto distribution ($\alpha = 1.85$, range 1 to 10 accounts).
   - Most customers have 1–2 accounts (*Sparkonto*, *Privatkonto*), while heavy-tail customers have multiple accounts (*Fasträntekonto*, *Barnsparande*, *Buffertspar*).
3. **Pareto Transactions per Account**:
   - Transaction volume follows a Pareto distribution ($\alpha = 1.40$, range 8 to 600 transactions per account).
4. **Realistic Swedish Cash Flow & Invariants**:
   - Non-negative balances: opening balance between 5,000 and 150,000 SEK; customer withdrawals cannot exceed available balance.
   - Automatic deposit injection if balance drops below threshold.
   - Realistic Swedish transaction descriptions (*"Löneinsättning"*, *"Kortköp ICA Supermarket"*, *"Swish"*, *"Hyra"*, *"Skatteåterbäring"*, *"Gymkort SATS"*, etc.).
5. **Multi-Document Customer Batch**:
   - For every account: an `account_statement` (*Kontoutdrag*) with period, formatted balances, and complete transaction ledger.
   - For interest-bearing accounts: an `annual_tax_report` (*Kontrolluppgift för ränteinkomst*) with calculated interest earned and 30% preliminary tax withheld.
   - All display values are pre-formatted according to Swedish banking conventions (`"+35 000,00 SEK"`, `"125 000,00 SEK"`).

---

## Usage

### Generate 100 Customer Batches (Default)

```bash
python3 tools/synthetic-input-generator/generate_data.py --customers 100 --clean
```

### Custom Options

```bash
python3 tools/synthetic-input-generator/generate_data.py \
    --customers 500 \
    --seed 42 \
    --year 2025 \
    --output generated/pool_500 \
    --clean
```

Options:
- `--customers <N>`: Number of customer batches to generate (default: `100`).
- `--seed <N>`: Master random seed for perfect byte-for-byte reproducibility (default: `827461`).
- `--year <N>`: Calendar reporting year (default: `2025`).
- `--output <path>`: Destination directory for JSON files.
- `--clean`: Wipe the target output directory before generating.

---

## Verification with Native PDF Generator

The generated JSON files can be directly verified and benchmarked using the native benchmark tool:

```bash
# Verify entire 100-customer pool with C ABI
../../build/pdf_generator_benchmark generated/pool_100 --api cabi
```
