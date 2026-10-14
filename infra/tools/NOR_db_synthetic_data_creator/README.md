# Compatibility entry point

`./NOR_db_synthetic_data_creator` points to the checked-in native Linux x86-64 release used by `./synthetic-data-manager`. No Python or local compilation is needed. Follow [the tool guide](../../../tools/synthetic/README.md).

Run from the repository root:

```bash
./NOR_db_synthetic_data_creator --customers 10000 --seed 42 --year 2025 --threads 4
./NOR_db_synthetic_data_creator --switch nordiska_synthetic
```

Seeding replaces previous synthetic data. The development database remains separate.
