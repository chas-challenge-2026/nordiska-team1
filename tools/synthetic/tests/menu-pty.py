#!/usr/bin/env python3
"""Exercise real arrow-key input and terminal restoration against mocked Docker."""
import os
import pty
import select
import subprocess
import sys
import termios
import time


def exercise(keys, expected):
    master, slave = pty.openpty()
    before = termios.tcgetattr(slave)
    process = subprocess.Popen([sys.argv[1]], stdin=slave, stdout=slave, stderr=slave)
    output = b""
    try:
        deadline = time.monotonic() + 10
        while b"\x1b[?25l" not in output:
            if time.monotonic() > deadline:
                raise AssertionError("Interactive menu did not start")
            if select.select([master], [], [], 0.1)[0]:
                output += os.read(master, 65536)
        os.write(master, keys)
        while process.poll() is None:
            if time.monotonic() > deadline:
                raise AssertionError("Interactive menu did not exit")
            if select.select([master], [], [], 0.1)[0]:
                output += os.read(master, 65536)
        while select.select([master], [], [], 0)[0]:
            output += os.read(master, 65536)
        assert process.returncode == 0, output.decode(errors="replace")
        assert expected in output, output.decode(errors="replace")
        assert termios.tcgetattr(slave) == before, "Terminal settings were not restored"
        assert b"\x1b[?25h" in output, "Cursor was not restored"
    finally:
        if process.poll() is None:
            process.kill()
        process.wait()
        os.close(master)
        os.close(slave)


# No arguments opens the TUI. Up/down must return to the safe default: Exit.
exercise(b"\x1b[A\x1b[B\r", b"> Exit")
exercise(b"\x03", b"Cancelled")
print("PASS: native arrow-key navigation, Ctrl+C and terminal restoration")
