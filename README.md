CI is temporarily disabled at the user's request. The workflow is preserved as .github/workflows/ci.yml.disabled; rename it back to ci.yml and push to restore CI. Local builds, tests, coverage and quality checks remain required.

Structured branches, nested blocks and early returns now extend the typed CLR core (SC-091/092); see [Issue #2 evidence](docs/ENGINEERING-ISSUE2-CONTROLFLOW.md). Local uint assignment, promoted shifts and StrictDoc traceability are implemented; detailed execution evidence is recorded in docs/ENGINEERING-2026-10-24.md. Canonical requirements: requirements/SharpContracts.sdoc.

# SharpContracts

Design by Contract and experimental formal verification for ordinary C# using Roslyn and Microsoft Z3.

```csharp
[Verify]
public static uint Midpoint(uint low, uint high)
{
    Contract.Require(low <= high, "MID-PRE", "Valid interval");
    uint midpoint = unchecked(low + (high - low) / 2u);
    Contract.Assert(midpoint >= low, "MID-LOW", "Lower bound");
    Contract.Ensure(midpoint <= high, "MID-HIGH", "Upper bound");
    return midpoint;
}
```

Attributes configure verification. C# expressions define contracts alongside the implementation. `Ensure` checks its call site; automatic method-exit postconditions are not implemented.

## Build and verify

Use the .NET 10 SDK (the latest recorded local build used SDK 10.0.402). All nine project files now target `net10.0`, including the Roslyn analyzer (previously `netstandard2.0`). This may affect analyzer host compatibility and requires a fresh build and IDE validation. GitHub Actions CI is intentionally disabled, so the preserved workflow is not evidence of a current CI run.

```sh
dotnet restore SharpContracts.sln
dotnet build SharpContracts.sln --configuration Release
dotnet test SharpContracts.sln --configuration Release

dotnet run --project SharpContracts.Cli -- --inline --json examples/BinarySearchMidpoint.cs Correct
dotnet run --project SharpContracts.Cli -- --inline --json examples/PaymentSplit.cs Buggy
dotnet run --project SharpContracts.Cli -- --inline --json examples/BufferAdmission.cs Correct
```

Windows x64 uses the native library from Microsoft.Z3 4.12.2. Linux x64 needs the matching upstream library; GitHub Actions uses the checksum-pinned `scripts/setup-z3-linux.sh`. An older system `libz3` is not interchangeable with the managed package.

## Executed evidence

The previous stabilized code revision `15ece33cb2e6b6fcccdcf7bc483c0f059015af65` passed [all seven CI jobs](https://github.com/jeffadickson/SharpContracts/actions/runs/39549862533), including CI Gate, with 193 passing tests.

The current local suite passes **392 tests, zero failures/skips**: 16 runtime, 32 solver, 344 verification. This increment adds 100 named source-to-Z3/CLR cases, three corpus/path tests and 26 typed-IR backend tests. Build checkpoints at iterations 10 through 100 and semantic reviews at 15 through 90 are recorded in [the iteration ledger](docs/ITERATION_LOG.md). Current-increment CI must be checked separately from that previous green revision; final execution evidence is recorded in the engineering report.

| Annotated source | Defective method | Corrected method |
|---|---|---|
| [Binary-search midpoint](examples/BinarySearchMidpoint.cs) | Wrapped sum produces an out-of-range midpoint | Both range obligations proved |
| [Payment split](examples/PaymentSplit.cs) | Odd cents lose a cent | Conservation proved |
| [Buffer admission](examples/BufferAdmission.cs) | Wrapped request can be admitted | Accepted usage stays within capacity |

These outcomes are covered by executed source-derived Z3 tests. Solver-generated inputs are replayed against the exact compiled annotated methods. Corrected proofs quantify over all permitted inputs in the documented model. Concrete CLR replay and browser exercises supplement those proofs; they do not establish general translator soundness.

## Shared compiler-bound pipeline

C# source -> Roslyn IOperation -> typed symbolic CLR expressions -> shared verification queries -> Z3 -> structured ProofReport.

`BoundNumericVerifier` and `InlineContractVerifier` now share expression lowering, bit-vector encoding, feasibility checks and counterexample extraction. The IR extends the original `ContractExpr` model with explicit widths, signedness, conversions and source spans. The mathematical Int/Real and BV64 prototypes remain available as separate experimental models; their capabilities have not all migrated.

Supported scalar expressions include unchecked integral conversions, addition/subtraction/multiplication, comparisons, unary operations, pure Boolean/bitwise operations and unsigned division by a nonzero constant, and C# shifts. Entry-point restrictions and unsupported features are listed in [the inline design](docs/INLINE_CONTRACT_DESIGN.md).

Full CFG/SSA joins, loops, checked exceptions, heap state, custom operators, generic math and true method-level postconditions remain open in [Issue #2](https://github.com/jeffadickson/SharpContracts/issues/2). Unsupported translation clears partial proofs. Impossible preconditions never become a successful proof. Unreachable later contracts are UNKNOWN and retain earlier counterexamples.

JSON exit codes: 0 proved, 1 counterexample, 2 unsupported/no contracts, 3 unknown, 4 inconsistent assumptions. Counterexamples include a contract ID, line, Z3 model and decimal-string input values; signed parameters retain their signed interpretation.

## Language designs and learning

- [SharpContracts C# language specification](docs/LANGUAGE_SPEC.md)
- [Separate VeloFormal temporal language v0.2](https://github.com/jeffadickson/VeloFormal/blob/main/docs/contract-language-v0.2.md)
- [Requirements](docs/REQUIREMENTS.md) - [Technical introduction](docs/INTRODUCTION.md) - [Custom types](docs/CUSTOM_TYPES.md)
- [Interactive introduction](index.html) - [Guided tutorial](tutorial/index.html) - [GitHub Actions](https://github.com/jeffadickson/SharpContracts/actions)

VeloFormal's clocked RTL syntax is independent of SharpContracts' inline C#. The original symbolic expression model is preserved; no shared cross-project IR serialization format is claimed.

The latest 50-case increment and five successful compile checkpoints are recorded in [the 50-iteration ledger](docs/ITERATION_50_LOG.md). Canonical [StrictDoc requirements](requirements/SharpContracts.sdoc) and [measured quality evidence](docs/ENGINEERING-2026-10-24.md) distinguish managed coverage, seeded properties and bounded mutation campaigns from formal proof.
