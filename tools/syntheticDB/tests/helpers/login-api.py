#!/usr/bin/env python3
"""Validate tool-provided credentials against the isolated frontend API."""
import json
from pathlib import Path
import sys
import time
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

base_url, credentials_path = sys.argv[1:]
credentials = {}
for line in Path(credentials_path).read_text().splitlines():
    if ": " in line:
        key, value = line.split(": ", 1)
        credentials[key] = value

for attempt in range(60):
    try:
        with urlopen(base_url + "/api/auth/me", timeout=2):
            break
    except HTTPError as error:
        if error.code == 401:
            break
        raise
    except (URLError, TimeoutError, ConnectionError):
        time.sleep(1)
else:
    raise AssertionError("Isolated API did not become ready")

request = Request(base_url + "/api/auth/login", data=json.dumps({
    "email": credentials["Email"], "password": credentials["Password"]
}).encode(), headers={"Content-Type": "application/json"})
with urlopen(request, timeout=15) as response:
    customer = json.load(response)
    assert response.headers.get("Set-Cookie"), "Login did not set the frontend session cookie"
assert customer["email"] == credentials["Email"]
assert customer["id"] == int(credentials["Customer ID"])
with urlopen(Request(base_url + "/api/auth/me", headers={
    "Authorization": "Bearer " + customer["token"]
}), timeout=15) as response:
    profile = json.load(response)
assert profile["email"] == credentials["Email"]
print("PASS: frontend email/password login and authenticated profile using tool-provided credentials")
