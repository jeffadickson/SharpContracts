#!/usr/bin/env python3
"""Execute source-derived Z3 proofs and validate CLI JSON and process exit codes."""
import json
import subprocess
import sys

cases = [
    ("examples/VerifiedByteAddition.cs", "Check", False, 0, "Proved"),
    ("examples/OverflowCounterexample.cs", "Check", False, 1, "Counterexample"),
    *[(f"examples/{file}.cs", method, True, code, outcome)
      for file in ("BinarySearchMidpoint", "PaymentSplit", "BufferAdmission")
      for method, code, outcome in (("Correct", 0, "Proved"), ("Buggy", 1, "Counterexample"))],
]
for source, method, inline, expected_exit, expected_outcome in cases:
    command = ["dotnet", "run", "--no-build", "--configuration", "Release",
               "--project", "SharpContracts.Cli", "--", "--json"]
    if inline:
        command.append("--inline")
    command.extend([source, method])
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
    if report.get("exitCode") != expected_exit or report.get("diagnostics"):
        raise SystemExit(f"{source}: report exit code or diagnostics mismatch")
    for check in report["checks"]:
        if not check.get("contractId") or check.get("line", 0) <= 0:
            raise SystemExit(f"{source}: missing source-linked contract")
        if inline and check["status"] == "Counterexample":
            inputs = check.get("counterexampleInputs")
            if not inputs or any(not 0 <= int(value) <= 2**32 - 1 for value in inputs.values()):
                raise SystemExit(f"{source}: missing or invalid replay inputs")
    print(f"PASS {source} {method}: {expected_outcome}, exit={expected_exit}")
print("Structured CLI contract validation passed")
