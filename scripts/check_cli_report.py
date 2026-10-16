#!/usr/bin/env python3
"""Validate SharpContracts machine-readable CLI contract without requiring a proof success."""
import json
import subprocess
import sys

cases = [
    ("examples/VerifiedByteAddition.cs", 0, "Proved"),
    ("examples/OverflowCounterexample.cs", 1, "Counterexample"),
]
for source, expected_exit, expected_outcome in cases:
    command = ["dotnet", "run", "--no-build", "--configuration", "Release",
               "--project", "SharpContracts.Cli", "--", "--json", source, "Check"]
    completed = subprocess.run(command, text=True, capture_output=True)
    if completed.returncode != expected_exit:
        raise SystemExit(
            f"{source}: expected exit {expected_exit}, got {completed.returncode}\n"
            f"stdout: {completed.stdout}\nstderr: {completed.stderr}")
    try:
        report = json.loads(completed.stdout)
    except json.JSONDecodeError as error:
        raise SystemExit(f"{source}: invalid JSON: {error}\n{completed.stdout}")
    if report.get("schemaVersion") != 1:
        raise SystemExit(f"{source}: unexpected schemaVersion {report.get('schemaVersion')}")
    if report.get("outcome") != expected_outcome:
        raise SystemExit(f"{source}: expected outcome {expected_outcome}, got {report.get('outcome')}")
    if not report.get("checks"):
        raise SystemExit(f"{source}: empty checks")
    print(f"PASS {source}: {expected_outcome}, exit={expected_exit}")
print("Structured CLI contract validation passed")
