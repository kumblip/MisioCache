#!/usr/bin/env python3
"""Self-tests that run before each published release, without extra dependencies."""
import unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
if __name__=='__main__':
    suite=unittest.defaultTestLoader.discover(str(ROOT/'tests'),pattern='test_*.py')
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    raise SystemExit(0 if result.wasSuccessful() else 1)
