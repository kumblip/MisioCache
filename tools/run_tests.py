#!/usr/bin/env python3
"""Run all Python tests and automatically discover .NET smoke tests.

Existing GitHub Build/Release workflows call this script already. Adding another
.NET test project does not require editing either workflow. CI fails closed if
.NET SDK is missing, while local source-only checks report a transparent skip.
"""
import os
from pathlib import Path
import shutil
import subprocess
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    suite = unittest.defaultTestLoader.discover(str(ROOT / "tests"), pattern="test_*.py")
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    if not result.wasSuccessful():
        return 1

    projects = sorted((ROOT / "tests" / "dotnet").rglob("*.csproj"))
    if not projects:
        return 0

    dotnet = shutil.which("dotnet")
    if dotnet is None:
        message = "No .NET SDK found: skipping C# tests locally."
        if os.environ.get("GITHUB_ACTIONS", "").lower() == "true":
            print("ERROR: " + message + " GitHub CI requires .NET.", file=sys.stderr)
            return 1
        print("SKIP: " + message, file=sys.stderr)
        return 0

    for project in projects:
        relative = project.relative_to(ROOT)
        print(f"Running .NET test project: {relative}", flush=True)
        completed = subprocess.run(
            [dotnet, "run", "--project", str(relative), "--configuration", "Release"],
            cwd=ROOT, check=False
        )
        if completed.returncode != 0:
            print(f"ERROR: .NET test project {relative} failed ({completed.returncode}).", file=sys.stderr)
            return completed.returncode
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
