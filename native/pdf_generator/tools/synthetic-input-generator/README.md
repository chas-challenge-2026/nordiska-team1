# Synthetic input generator

This standalone Python tool creates deterministic benchmark datasets for the
native PDF generator. The run config selects a workload profile from
`config/workloads/`; all resolved settings are copied into `manifest.json`.

Generate a small test dataset by copying `config/generator.json`, changing the
profile's account and transaction counts, and running:

```bash
python3 generate_data.py --config config/generator.json --clean
```

The output contains one JSON file per account plus `manifest.json`. Account
files contain the fields required by the current `JsonInputAdapter`, along
with the richer ledger and summary fields needed to inspect the simulation.

To generate every available workload profile:

```bash
./generate_all.sh
```

Each profile is written to its own directory under `generated/`. A different
output root can be supplied as the first argument.
