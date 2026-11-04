#!/usr/bin/env python3
"""Exercise the state-aware TUI in a real 80x24 PTY with mocked Docker."""
import fcntl
import os
from pathlib import Path
import pty
import re
import select
import struct
import subprocess
import sys
import termios
import time

state = Path(os.environ['TEST_STATE'])
env = Path(os.environ['TEST_REPO']) / 'infra/v2/.env'


def fixture(exists=True, empty=False, saved='nordiska_v2', runtime=None):
    for path in state.iterdir():
        path.unlink()
    env.write_text(f'NORDISKA_DATABASE={saved}\n')
    if exists:
        (state / 'exists').touch()
    if empty:
        (state / 'empty').touch()
    if runtime:
        (state / ('runtime-normal' if runtime == 'nordiska_v2' else 'runtime-synthetic')).touch()


class Terminal:
    def __init__(self, color=True, term='xterm-256color'):
        self.master, self.slave = pty.openpty()
        fcntl.ioctl(self.slave, termios.TIOCSWINSZ, struct.pack('HHHH', 24, 80, 0, 0))
        self.before = termios.tcgetattr(self.slave)
        environment = dict(os.environ, TERM=term)
        environment.pop('NO_COLOR', None)
        if not color:
            environment['NO_COLOR'] = '1'
        self.process = subprocess.Popen([sys.argv[1]], stdin=self.slave, stdout=self.slave, stderr=self.slave,
                                        env=environment)
        self.output = b''
        self.since = 0

    def wait(self, marker):
        deadline = time.monotonic() + 12
        while not (marker.search(self.output[self.since:]) if isinstance(marker, re.Pattern) else marker in self.output[self.since:]):
            if time.monotonic() > deadline or self.process.poll() is not None:
                raise AssertionError(f'Missing {marker!r}: {self.output.decode(errors="replace")}')
            if select.select([self.master], [], [], 0.1)[0]:
                self.output += os.read(self.master, 65536)
        # Collect the remainder of the rendered frame without sending premature input.
        while select.select([self.master], [], [], 0.03)[0]:
            self.output += os.read(self.master, 65536)

    def send(self, keys):
        self.since = len(self.output)
        os.write(self.master, keys)

    def screen(self):
        # Minimal VT renderer for the cursor/erase sequences used by this TUI.
        cells = [[' '] * 80 for _ in range(24)]
        row = col = 0
        text = self.output.decode('utf-8', errors='replace')
        position = 0
        while position < len(text):
            if text[position:position + 2] == '\x1b[':
                match = re.match(r'\x1b\[([0-9;?]*)([A-Za-z])', text[position:])
                if match:
                    args, command = match.groups()
                    if command in ('H', 'f'):
                        parts = [int(x or '1') for x in args.split(';')] if args else [1, 1]
                        row, col = parts[0] - 1, (parts[1] if len(parts) > 1 else 1) - 1
                    elif command == 'J' and args == '2':
                        cells = [[' '] * 80 for _ in range(24)]
                    elif command == 'K':
                        if 0 <= row < 24:
                            cells[row][0 if args == '2' else col:] = [' '] * (80 if args == '2' else 80 - col)
                    position += len(match.group(0))
                    continue
            ch = text[position]
            position += 1
            if ch == '\r':
                col = 0
            elif ch == '\n':
                row += 1
            elif ch >= ' ':
                assert col < 80, 'Screen exceeds 80 columns'
                assert row < 24, 'Screen exceeds 24 rows'
                cells[row][col] = ch
                col += 1
        return '\n'.join(''.join(line).rstrip() for line in cells)

    def finish(self, keys=b'\x1b'):
        try:
            self.send(keys)
            self.process.wait(timeout=8)
            while select.select([self.master], [], [], 0)[0]:
                self.output += os.read(self.master, 65536)
            assert self.process.returncode == 0, self.output.decode(errors='replace')
            assert termios.tcgetattr(self.slave) == self.before, 'Terminal settings not restored'
            assert b'\x1b[?1049l' in self.output, 'Original screen not restored'
            assert b'\x1b[?25h' in self.output, 'Cursor not restored'
        finally:
            if self.process.poll() is None:
                self.process.kill()
            self.process.wait()
            os.close(self.master)
            os.close(self.slave)


def assert_layout(view, controls, title=None):
    lines = view.splitlines()
    if title:
        assert lines[0].strip() == title, view
    assert lines[1].strip() == '-' * 50, view
    assert controls in [line.strip() for line in lines], view
    footer = next(i for i, line in enumerate(lines) if line.strip() == controls)
    assert lines[footer - 1].strip() == '-' * 50, view
    assert not any(line.strip() for line in lines[footer + 4:]), view


fixture(saved='nordiska_synthetic', runtime='nordiska_v2')
terminal = Terminal()
terminal.wait(b'   Exit')
view = terminal.screen()
assert_layout(view, 'Up/Down Navigate   Enter Select   R Refresh   Esc Exit', 'Synthetic Data Manager [Active DB: NORMAL]')
assert view.count('Switch to SYNTHETIC DB') == 1
assert 'Switch to SYNTHETIC DB' in view
assert '1,003' in view and '70,722' in view
assert b'\x1b[32mNORMAL\x1b[0m' in terminal.output
assert b'\x1b[1;36;7m > Switch to SYNTHETIC DB' in terminal.output
for hidden in ('Append', 'Empty synthetic database', 'Create synthetic data', 'System details', 'Saved:', 'PDF volume', 'Docker host', 'Apply', 'Revert'):
    assert hidden not in view, view
# Up wraps to Exit and Down returns to the same switching action.
terminal.send(b'\x1b[A\x1b[B')
terminal.wait(b'> Switch to SYNTHETIC DB')
terminal.finish()
print('PASS: compact 80x24 main screen, actual state, no append or diagnostic noise')

fixture()
terminal = Terminal()
terminal.wait(b'   Exit')
for key, runtime, label in ((b'r', 'runtime-synthetic', 'SYNTHETIC'),
                            (b'R', 'runtime-normal', 'NORMAL')):
    for marker in ('runtime-normal', 'runtime-synthetic'):
        (state / marker).unlink(missing_ok=True)
    (state / runtime).touch()
    terminal.send(key)
    terminal.wait(b'Status refreshed.')
    assert f'Active DB: {label}' in terminal.screen()
    assert '> Refresh status' in terminal.screen()
assert ' up ' not in (state / 'calls').read_text()
terminal.finish()
print('PASS: lowercase and uppercase refresh reload running state without recreating services')

fixture()
(state / 'slow-up').touch()
terminal = Terminal()
terminal.wait(b'   Exit')
terminal.send(b'\r')
terminal.wait(b'Please wait...')
assert_layout(terminal.screen(), 'Please wait...', 'Synthetic Data Manager')
terminal.wait(b'OK: switched to SYNTHETIC')
view = terminal.screen()
assert 'Active DB: SYNTHETIC' in view
assert '> Switch back to NORMAL DB' in view
assert b'\x1b[1;36mSYNTHETIC\x1b[0m' in terminal.output
assert b'\x1b[32m OK: switched to SYNTHETIC.\x1b[0m' in terminal.output
frames = re.findall(rb'Switching DB: NORMAL -> SYNTHETIC  ([|/\\-])', terminal.output)
assert len(set(frames)) >= 3, frames
terminal.send(b'\r')
terminal.wait(b'OK: switched to NORMAL')
assert '> Switch to SYNTHETIC DB' in terminal.screen()
terminal.finish()
print('PASS: one-selection switching, animated spinner, direction refresh and preserved selection')

fixture()
terminal = Terminal()
terminal.wait(b'   Exit')
terminal.send(b'\x1b[A\x1b[A\x1b[A\r')
terminal.wait(b'Esc Back')
assert 'Random customer login' in terminal.screen()
assert_layout(terminal.screen(), 'Esc Back', 'Random customer login from NORMAL DB')
assert 'anna@example.com' in terminal.screen()
assert 'password123' in terminal.screen()
terminal.send(b'\x1b')
terminal.wait(b'> Show random customer login')
assert 'PDF directory' not in terminal.screen()
terminal.finish()
print('PASS: random customer login opens directly and Esc returns to main')

fixture(exists=False)
terminal = Terminal()
terminal.wait(b'   Exit')
terminal.send(b'\r')
terminal.wait(b'Esc Back')
view = terminal.screen()
assert_layout(view, 'Up/Down Navigate   Enter Select   Esc Back', 'Create synthetic data')
for label in ('100', '1000', '10k', '100k', '1m', '10m', 'MAX (37,620,000)', 'Custom amount'):
    assert label in view
assert '> 10k' in view
assert 'CPU threads' not in view and 'Reporting year' not in view
# Verify the large presets in settings, then cancel without creating data.
terminal.send(b'\x1b[B' * 4 + b'\r')
terminal.wait(b'> Start creation')
assert 'Customers: 37,620,000' in terminal.screen()
terminal.send(b'\x1b')
terminal.wait(b'> MAX (37,620,000)')
terminal.send(b'\x1b[A\r')
terminal.wait(b'> Start creation')
assert 'Customers: 10,000,000' in terminal.screen()
terminal.send(b'\x1b')
terminal.wait(b'> 10m')
terminal.send(b'\x1b[A' * 3)
terminal.wait(b'> 10k')
# Wrap upward from 10k through 1000 and 100 to Custom amount.
terminal.send(b'\x1b[A\x1b[A\x1b[A\r')
terminal.wait(b' Custom amount:')
assert_layout(terminal.screen(), 'Enter Continue   Esc Back', 'Create synthetic data')
# Arrow/function keys must not cancel the prompt or inject their digits.
terminal.send(b'\x1b[A\x1b[B\x1b[3~0\r')
terminal.wait(b'Enter a whole number from 1 to 37,620,000.')
assert_layout(terminal.screen(), 'Enter Continue   Esc Back', 'Create synthetic data')
assert b'Cancelled.' not in terminal.output
terminal.send(b'\x1b')
terminal.wait(b'> Custom amount')
terminal.send(b'\x1b')
terminal.wait(b'Cancelled.')
assert '> Create synthetic data' in terminal.screen()
assert 'DROP DATABASE' not in (state / 'calls').read_text()
terminal.finish(b'\x03')
print('PASS: preset menu, custom validation, harmless arrow keys, Esc returns and Ctrl+C restores terminal')

for empty, exists in ((True, True), (False, False)):
    fixture(exists=exists, empty=empty)
    terminal = Terminal()
    terminal.wait(b'   Exit')
    view = terminal.screen()
    assert 'Show random customer login' in view
    assert 'Empty synthetic database' not in view
    if not exists:
        assert 'Switch to SYNTHETIC DB' not in view and 'Switch back to NORMAL DB' not in view and 'Delete synthetic DB' not in view
        assert 'Create synthetic data' in view
    else:
        assert 'Create synthetic data' not in view
        assert 'Switch to SYNTHETIC DB' in view
        assert 'Delete synthetic DB and return to NORMAL' in view
    terminal.finish()
print('PASS: missing and empty synthetic databases hide inapplicable actions')

fixture(runtime='nordiska_synthetic')
terminal = Terminal()
terminal.wait(b'   Exit')
terminal.send(b'\x1b[B\r')
terminal.wait(b'Delete synthetic DB and return to NORMAL?')
assert_layout(terminal.screen(), 'Y/N Choose   Enter Confirm   Esc Cancel', 'Confirm deletion')
terminal.send(b'y\r')
terminal.wait(b'OK: synthetic DB deleted; NORMAL selected.')
view = terminal.screen()
assert 'Active DB: NORMAL' in view
assert '> Create synthetic data' in view
assert 'Delete synthetic DB' not in view
assert 'Switch to SYNTHETIC DB' not in view
terminal.finish()
print('PASS: deletion returns to NORMAL and replaces delete/switch with creation')


fixture()
(state / 'fail-up-once').touch()
terminal = Terminal()
terminal.wait(b'   Exit')
terminal.send(b'\r')
terminal.wait(b'Error:')
assert b'\x1b[1;31m Error:' in terminal.output
terminal.finish()
print('PASS: failed operations use red error messages')

for color, term in ((False, 'xterm-256color'), (True, 'dumb')):
    fixture()
    terminal = Terminal(color=color, term=term)
    terminal.wait(b'   Exit')
    assert 'Active DB: NORMAL' in terminal.screen()
    terminal.finish()
    assert not re.search(rb'\x1b\[[0-9;]*m', terminal.output), 'Color opt-out emitted SGR styling'
print('PASS: NO_COLOR and TERM=dumb disable colors without changing navigation')

fixture(runtime='nordiska_synthetic')
(state / 'unicode-customer').touch()
terminal = Terminal()
terminal.wait(b'   Exit')
terminal.send(b'\x1b[A\x1b[A\x1b[A\r')
terminal.wait(b'Esc Back')
view = terminal.screen()
assert 'Random customer login from SYNTHETIC DB' in view
assert 'Gösta Åström' in view
assert 'Scroll' not in view
terminal.send(b'\r')
terminal.wait(b'> Show random customer login')
terminal.finish()
print('PASS: Swedish names remain intact; short login page has no scroll hint and Enter returns')

fixture()
(state / 'fail-query').touch()
terminal = Terminal()
terminal.wait(b'   Exit')
view = terminal.screen()
assert '> Retry loading database state' in view
assert 'Create synthetic data' not in view and 'Delete synthetic DB' not in view
(state / 'fail-query').unlink()
terminal.send(b'\r')
terminal.wait(b'> Switch to SYNTHETIC DB')
assert 'Error:' not in terminal.screen()
terminal.finish()
print('PASS: failed status read offers retry and restores applicable actions after recovery')

for exists, saved, target in ((False, 'nordiska_synthetic', 'NORMAL'), (True, 'nordiska_synthetic', 'SYNTHETIC')):
    fixture(exists=exists, saved=saved)
    (state / 'no-app').touch()
    terminal = Terminal()
    terminal.wait(b'   Exit')
    view = terminal.screen()
    assert 'Active DB: OFFLINE' in view
    assert f'> Start app on {target} DB' in view
    assert 'Show random customer login' not in view
    terminal.send(b'\r')
    terminal.wait(f'OK: app started on {target}.'.encode())
    assert f'Active DB: {target}' in terminal.screen()
    terminal.finish()
print('PASS: offline app is shown honestly, starts on an existing target and refreshes to running state')

fixture()
(state / 'no-login-customer').touch()
terminal = Terminal()
terminal.wait(b'   Exit')
assert 'Show random customer login' not in terminal.screen()
terminal.finish()

fixture()
terminal = Terminal()
terminal.wait(b'   Exit')
(state / 'no-login-customer').touch()
terminal.send(b'\x1b[A\x1b[A\x1b[A\r')
terminal.wait(b'Error:')
view = terminal.screen()
assert 'Show random customer login' not in view
assert '> Switch to SYNTHETIC DB' in view
terminal.finish()
print('PASS: disappearing eligible customer produces an error and safe refreshed selection')

fixture()
(state / 'fail-up-always').touch()
terminal = Terminal()
terminal.wait(b'   Exit')
terminal.send(b'\r')
terminal.wait(b'Error:')
view = ' '.join(terminal.screen().split())
assert 'Switch and rollback failed' in view
assert 'Recreate the V2 services before continuing' in view
assert 'OK:' not in view
terminal.finish()
print('PASS: full rollback failure remains visible across result lines')


fixture()
terminal = Terminal()
terminal.wait(b'   Exit')
terminal.send(b'\x1b[B\r')
terminal.wait(b'Delete synthetic DB and return to NORMAL?')
assert_layout(terminal.screen(), 'Y/N Choose   Enter Confirm   Esc Cancel', 'Confirm deletion')
terminal.send(b'\x1b')
terminal.wait(b'Cancelled.')
assert '> Delete synthetic DB' in terminal.screen()
assert 'DROP DATABASE' not in (state / 'calls').read_text()
terminal.finish()
print('PASS: shared title, separators and footer on menus, prompts, login, loading and confirmations')


fixture(exists=False)
(state / 'slow-copy').touch()
terminal = Terminal()
terminal.wait(b'   Exit')
terminal.send(b'\r')
terminal.wait(b'> 10k')
terminal.send(b'\x1b[B' * 5 + b'\r')
terminal.wait(b' Custom amount:')
terminal.send(b'2500\r')
terminal.wait(b'> Start creation')
assert_layout(terminal.screen(), 'Up/Down Navigate   Enter Select   Esc Back', 'Create synthetic data')
assert 'Customers: 2,500' in terminal.screen()
assert 'Seed: 42' in terminal.screen()
terminal.send(b'\x1b[A\r')
terminal.wait(b' Seed [42]:')
assert_layout(terminal.screen(), 'Enter Save   Esc Back', 'Generation seed')
terminal.send(b'invalid\r')
terminal.wait(b'Enter a whole number from 0 to')
terminal.send(b'31415\r')
terminal.wait(b'> Seed: 31415')
terminal.send(b'\x1b[B\r')
terminal.wait(b'Remaining: calibrating 500 customers...')
assert_layout(terminal.screen(), 'Please wait...', 'Synthetic Data Manager')
assert 'Seed: 31415' in terminal.screen()
assert 'Stage ETA' not in terminal.screen()
terminal.wait(b'Generated: 500 | Transferred: 0')
assert 'Customers ready: 0 / 2,500' in terminal.screen()
assert 'Transactions saved: 0' in terminal.screen()
terminal.wait(re.compile(rb'Remaining: ~[0-9]+:[0-9]{2}'))
view = terminal.screen()
assert 'Customers ready: 500 / 2,500' in view
assert re.search(r'Rate: [1-9][0-9,]* customers/s', view)
assert re.search(r'Transactions saved: [1-9][0-9,]*', view)
terminal.wait(re.compile(rb'Customers ready: (?!500 /)[0-9,]+ / 2,500'))
view = terminal.screen()
assert re.search(r'Remaining: ~[0-9]+:[0-9]{2}', view)
assert 'Elapsed:' in view
assert 'Resume' not in view and 'Pause' not in view
terminal.wait(b'OK: created 2,500 synthetic customers (seed 31415)')
assert 'Switch to SYNTHETIC DB' in terminal.screen()
terminal.finish()
print('PASS: seed setting, initial 500 fully saved customers and refreshed total ETA during sustained ingestion')

fixture(exists=False)
terminal = Terminal()
terminal.wait(b'   Exit')
terminal.send(b'\r')
terminal.wait(b'> 10k')
terminal.send(b'\r')
terminal.wait(b'> Start creation')
terminal.send(b'\x1b')
terminal.wait(b'> 10k')
assert 'DROP DATABASE' not in (state / 'calls').read_text()
terminal.send(b'\x1b')
terminal.wait(b'Cancelled.')
terminal.finish()
print('PASS: Esc from generation settings returns to count selection without creating data')


fixture(empty=True)
terminal = Terminal()
terminal.wait(b'   Exit')
view = terminal.screen()
assert 'Delete synthetic documents (no documents)' in view
assert b'\x1b[90m - Delete synthetic documents (no documents)' in terminal.output
terminal.send(b'\x1b[B\x1b[B')
terminal.wait(b'> Show random customer login')
assert '> Delete synthetic documents' not in terminal.screen()
terminal.send(b'\x1b[A')
terminal.wait(b'> Delete synthetic DB')
terminal.finish()
print('PASS: empty document action is gray and skipped in both navigation directions')

fixture(runtime='nordiska_synthetic')
(state / 'pdf-files').touch()
terminal = Terminal()
terminal.wait(b'   Exit')
terminal.send(b'\x1b[B\x1b[B\r')
terminal.wait(b'Delete all synthetic PDFs and PDF jobs?')
assert_layout(terminal.screen(), 'Y/N Choose   Enter Confirm   Esc Cancel', 'Delete synthetic documents')
terminal.send(b'n\r')
terminal.wait(b'Cancelled.')
assert (state / 'pdf-files').exists()
terminal.send(b'\r')
terminal.wait(b'Delete all synthetic PDFs and PDF jobs?')
terminal.send(b'y\r')
terminal.wait(b'OK: synthetic PDFs and PDF jobs deleted.')
view = terminal.screen()
assert 'Active DB: SYNTHETIC' in view
assert 'Delete synthetic documents (no documents)' in view
assert '> Show random customer login' in view
assert '1,003' in view and '70,722' in view
assert not (state / 'pdf-files').exists()
terminal.finish()
print('PASS: confirmed document cleanup preserves active DB and financial data, then disables its action')

fixture(exists=False)
(state / 'pdf-files').touch()
terminal = Terminal()
terminal.wait(b'   Exit')
view = terminal.screen()
assert 'Delete synthetic documents (no documents)' not in view
assert 'Delete synthetic documents' in view
terminal.finish()
print('PASS: retained orphan PDFs remain cleanable after deleting the synthetic database')
