#!/usr/bin/env python3
"""Persistent psql protocol mock for CLI/UI tests; never connects to a database."""
import os
from pathlib import Path
import sys
import time

state = Path(os.environ['TEST_STATE'])
events = state / 'batch-events'
commits = 0
pending = []

def event(text):
    with events.open('a') as out:
        out.write(text + '\n')

for line in sys.stdin:
    sql = line.strip()
    if sql.startswith('COPY '):
        table = ('customers' if 'banking.customers ' in sql else
                 'roles' if 'AspNetUserRoles' in sql else
                 'accounts' if 'savings_accounts' in sql else 'ledger')
        rows = []
        for row in sys.stdin:
            if row == '\\.\n':
                break
            rows.append(row)
        if table == 'customers':
            (state / 'empty').unlink(missing_ok=True)
            with (state / 'customer-rows').open('a') as out:
                out.writelines(rows)
        pending.append(f'{table} {len(rows)}')
        if (state / 'slow-copy').exists():
            time.sleep(0.35)
        if (state / 'fail-copy').exists():
            print('isolated COPY failure', file=sys.stderr)
            sys.exit(1)
    elif sql.startswith("SELECT 'NORDISKA_INDEX|'"):
        (state / 'index-discovery').touch()
    elif sql.startswith('SELECT setval') and 'banking.customers' in sql:
        if (state / 'fail-sequences').exists() or (commits and (state / 'fail-final-sequences').exists()):
            print('Sequence reset failed', file=sys.stderr)
            sys.exit(1)
        event('sequences')
    elif sql == 'COMMIT;':
        if commits == 1 and (state / 'slow-second-batch').exists():
            time.sleep(2)
        for entry in pending:
            event(entry)
        event('commit')
        pending.clear()
        commits += 1
    elif sql.startswith('\\echo '):
        print(sql[6:], flush=True)
