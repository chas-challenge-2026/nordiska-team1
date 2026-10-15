#!/usr/bin/env python3
"""Observe real committed data before passing psql acknowledgements to the tool."""
import os
import subprocess
import sys

command = [os.environ['SYNTHETIC_TEST_DOCKER'], *sys.argv[1:]]
child = subprocess.Popen(command, stdin=sys.stdin, stdout=subprocess.PIPE, text=True)
for line in child.stdout:
    if line.startswith('NORDISKA_COMMIT_'):
        result = subprocess.run([
            os.environ['SYNTHETIC_TEST_DOCKER'], 'exec', os.environ['SYNTHETIC_TEST_CONTAINER'],
            'psql', '-U', 'nordiska_bootstrap', '-d', 'nordiska_synthetic', '-At', '-v', 'ON_ERROR_STOP=1', '-c',
            '''SELECT (SELECT COUNT(*) FROM banking.customers)||'|'||
            (SELECT COUNT(*) FROM banking."AspNetUserRoles")||'|'||
            (SELECT COUNT(*) FROM banking.savings_accounts)||'|'||
            (SELECT COUNT(*) FROM banking.ledger_entries);'''
        ], check=True, capture_output=True, text=True)
        if os.environ.get('SYNTHETIC_TEST_INDEX_LOG'):
            indexes = subprocess.run([
                os.environ['SYNTHETIC_TEST_DOCKER'], 'exec', os.environ['SYNTHETIC_TEST_CONTAINER'],
                'psql', '-U', 'nordiska_bootstrap', '-d', 'nordiska_synthetic', '-At', '-v', 'ON_ERROR_STOP=1', '-c',
                "SELECT COUNT(*) FROM pg_index WHERE indrelid='banking.ledger_entries'::regclass AND indisvalid AND indisready;"
            ], check=True, capture_output=True, text=True)
            with open(os.environ['SYNTHETIC_TEST_INDEX_LOG'], 'a') as output:
                output.write(indexes.stdout)
        if result.stdout.startswith('500|') and os.environ.get('SYNTHETIC_TEST_FAIL_INDEX_REBUILD'):
            subprocess.run([
                os.environ['SYNTHETIC_TEST_DOCKER'], 'exec', os.environ['SYNTHETIC_TEST_CONTAINER'],
                'psql', '-U', 'nordiska_bootstrap', '-d', 'nordiska_synthetic', '-v', 'ON_ERROR_STOP=1', '-c',
                """CREATE SEQUENCE public.synthetic_index_attempt;
CREATE FUNCTION public.synthetic_fail_index_once() RETURNS event_trigger LANGUAGE plpgsql AS $$
BEGIN IF tg_tag='CREATE INDEX' AND nextval('public.synthetic_index_attempt')=1
THEN RAISE EXCEPTION 'isolated index rebuild failure'; END IF; END; $$;
CREATE EVENT TRIGGER synthetic_fail_index_once ON ddl_command_start WHEN TAG IN ('CREATE INDEX')
EXECUTE FUNCTION public.synthetic_fail_index_once();"""
            ], check=True, stdout=subprocess.DEVNULL)
        with open(os.environ['SYNTHETIC_TEST_BATCH_LOG'], 'a') as output:
            output.write(result.stdout)
    print(line, end='', flush=True)
sys.exit(child.wait())
