#!/usr/bin/env python3
import sys
from pathlib import Path

curr = Path(__file__).resolve().parent
for _ in range(5):
    if (curr / "infra" / "docker-compose.yml").exists():
        repo_root = curr
        break
    curr = curr.parent
else:
    repo_root = Path.cwd()

sys.path.insert(0, str(repo_root))

from tools.synthetic.manage_synthetic_data import main

if __name__ == "__main__":
    main()
