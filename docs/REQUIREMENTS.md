## Current Issue #2 increment: SC-091 / SC-092

The inline verifier now supports structured blocks, if/else, nested branches and early returns through the typed CLR IR, with independent local snapshots on each path. Complete translation includes dead arms; unsupported operations discard every partial result. Entirely unreachable contracts are UNKNOWN. Require remains an entry prefix and Ensure remains a source call-site check, which an early return can bypass. Bounded exploration is documented in [the engineering evidence](ENGINEERING-ISSUE2-CONTROLFLOW.md). Full CFG/SSA migration, loops, checked exceptions and custom types remain open. This supersedes earlier straight-line-only scope statements below.

Canonical requirements SC-091 and SC-092 are maintained in requirements/SharpContracts.sdoc. CI is temporarily disabled by user request; local execution remains required.

## Current assignment increment — 2026-10-24

Canonical requirements now live in [StrictDoc](../requirements/SharpContracts.sdoc), preserving all pre-existing SC identifiers. Markdown below is a historical overview. SC-089 adds simple assignment to existing ordinary UInt32 locals through immutable IR substitution; SC-088 remains partial because branches, CFG joins and parameter/heap mutation have not migrated. Ref locals are explicitly rejected to prevent accidental alias reasoning. Compound assignment and checked updates remain unsupported.

Coverage, mutation, fuzz/property seeds and execution counts for this increment are recorded in ENGINEERING-2026-10-24.md after actual execution. Coverage measures managed assemblies only, not native Z3 or dynamically emitted replay assemblies.
# Current requirement status - typed CLR core, 2026-10-22

Local execution: all nine projects build; 322 xUnit cases pass with zero failures or skips (16 runtime, 32 solver, 274 verification). This is 129 more tests than the previous stabilized revision. The new corpus contains exactly 100 uniquely numbered source-derived cases, plus integrity and unreachable-path checks. Every SAT case replays the solver witness against dynamically compiled original C#; every proved case also executes boundary samples. All original flagship and archived-corpus tests remain passing. The current commit's Linux CI conclusion must be checked after publication.

| ID | Acceptance requirement | Status and evidence |
|---|---|---|
| SC-081 | Both compiler-bound entry points consume the same typed symbolic primitive-expression IR and Z3 encoder | Closed within the documented straight-line subset; ClrOperationLowerer / ClrExpression / ClrExpressionEncoder |
| SC-082 | Preserve compiler numeric promotions, signedness and unchecked conversions | Closed for supported primitives; semantic cases 001-060 and 100, plus CLR replay |
| SC-083 | Numeric and inline SAT results expose replayable parameter values with correct signed interpretation | Closed; semantic counterexamples, including signed widths and char |
| SC-084 | Source-defined Contract and VerifyAttribute lookalikes must fail closed | Closed; semantic cases 085-087 |
| SC-085 | Invalid typed nodes, checked operations and exceptional division must never yield a formula/proof | Closed within the backend's model; ClrExpressionEncoderTests and cases 061-070 |
| SC-086 | Unreachable later contracts must not erase earlier failures or become vacuous proofs | Closed; cases 088-089 and explicit per-check status assertions |
| SC-087 | Maintain a 100-case source-to-Z3/CLR regression gate with fixture integrity enforcement | Implemented and passing locally; explicit semantic100 CI step retains TRX evidence |
| SC-088 | Migrate legacy CFG/SSA, assignments and branches into the typed core | Open; legacy mathematical and BV64 implementations retained |

SC-025 remains partial: typed expression nodes now integrate with both compiler-bound entry points, but a stable serialization schema is not implemented. SC-031 is implemented for source-linked scalar counterexamples and automated CLR replay within this subset. SC-026/SC-032 are implemented for the documented subset and report outcomes. Custom types, checked exception paths, loops, heap state, generic math and method-level postconditions remain open. This increment does not close all of Issue #2.

Builds were executed at 10,20,30,40,50,60,70,80,90,100; semantic reviews at 15,30,45,60,75,90 and a final deep review. See [the full case/review ledger](ITERATION_LOG.md). Older milestone entries below are retained as history; historical pending statements do not override this current status.

## Counterexample replay and report hardening — 2026-10-20

The first stabilization commit `360efa09eca8679ea23e8c3f56663b64e27705be` passed [all seven GitHub Actions jobs, including CI Gate](https://github.com/jeffadickson/SharpContracts/actions/runs/39549650831). Local regression coverage now totals 193 passing xUnit cases, zero failures/skips, including eight new tests for solver-input replay, inconsistent inline preconditions, by-reference rejection, and checked/unchecked return wrappers.

Inline counterexamples now include decimal-string `counterexampleInputs` keyed by parameter name alongside the Z3 model, contract ID and source line. Tests replay actual solver inputs against the exact compiled annotated C# examples. Inline feasibility distinguishes inconsistent assumptions (exit 4) from solver UNKNOWN (exit 3); unsupported translation still clears every partial proof. By-reference parameters/returns are explicitly unsupported. CLI validation executes all six flagship source/method combinations plus the two numeric examples. Broader typed-IR consolidation, branches, assignments and modeled checked exceptions remain open in Issue #2.
## Executed stabilization baseline — 2026-10-20

Windows x64, .NET SDK 8.0.132: all nine projects compile; 185 xUnit tests pass (16 runtime, 6 solver, 163 verification), zero failures/skips. The archived corpus separately passes 100 vectors plus its integrity test. Native Z3 4.12.2 executes; the three corrected annotated examples prove their four obligations and all three defective examples yield counterexamples. CLI JSON/exit-code validation and embedded HTML script syntax checks pass.

Fixed Roslyn metadata-reference overload inference and semantic operation lookup for checked/unchecked initializer and return wrappers. `SharpContracts.sln` supports root restore/build/test. Linux CI now selects the matching upstream Z3 4.12.2 native library using a pinned SHA-256 because the NuGet package ships no Linux library. Linux execution and a green CI Gate remain pending; this local evidence does not establish general C# soundness or architecture consolidation. The symbolic expression model remains in `SharpContracts.Runtime/Symbolic`.
## Verified-core acceptance gate — 100 archived vectors

- SC-077 (P0): Execute all 100 archived arithmetic vectors as CLR xUnit cases. **Closed: CLR tests executed successfully in the stabilized CI baseline**.
- SC-078 (P0): Execute the actual annotated C# midpoint/payment/buffer methods and check contract exceptions. **Closed: CLR tests executed successfully in the stabilized CI baseline**.
- SC-079 (P0): Require Z3 source-derived proof for each corrected method and counterexample for each defective method. **Closed: source-derived Z3 and compiled CLR regressions pass**.
- SC-080 (P0): Publish successful GitHub CI Gate run with commit SHA, logs and TRX artifacts before declaring the verified core complete. **Closed for stabilized revision `15ece33cb2e6b6fcccdcf7bc483c0f059015af65`: [passing CI Gate](https://github.com/jeffadickson/SharpContracts/actions/runs/39549862533)**.

# Inline contract verification — implementation milestone

Canonical design: [INLINE_CONTRACT_DESIGN.md](INLINE_CONTRACT_DESIGN.md).

| ID | Requirement | Priority | Status |
|---|---|---|---|
| SC-069 | Opt-in C# verification with [Verify] metadata | P0 | Implemented in source |
| SC-070 | Inline Require, Assert and Ensure contracts using typed C# expressions | P0 | Implemented in source for supported subset |
| SC-071 | Extract proof obligations directly from the annotated C# method, not handwritten Z3 formulas | P0 | Implemented in source for UInt32 midpoint subset |
| SC-072 | Translate local declaration and final return via Roslyn semantic operations | P0 | Implemented in source; broader control flow open |
| SC-073 | Prove corrected midpoint and detect buggy midpoint from the same annotated file | P0 | xUnit and CLI CI gates committed; actual run unverified |
| SC-074 | Reject unmodeled exceptions (zero divisor, checked arithmetic) and unsupported C# constructs | P0 | Implemented for modeled operations; soundness audit pending |
| SC-075 | Support true method-level postconditions across every return/exception path | P0 | Open |
| SC-076 | Integrate attribute-driven verification into compiler/IDE diagnostics automatically | P0 | Open; --inline CLI opt-in only |

**Acceptance currently pending:** a successful compile and Z3 test run in GitHub Actions. Do not mark this entire feature production-complete until that evidence exists.

## Proof showcase — midpoint overflow

A concrete C# binary-search midpoint bug and corrected version are recorded in `examples/BinarySearchMidpoint.cs`. `SharpContracts.Solver.Tests/MidpointProofTests.cs` contains concrete regression cases and a BV32 Z3 proof obligation over all unsigned input pairs with `low<=high`. The tutorial and main HTML page include a matching interactive example. Source/solver equivalence must be confirmed in CI. Automatic proof-condition extraction for division and local state is still open.

## CI status visibility repair

SC-068: Aggregate CI Gate must identify each required dependency and conclusion in the Actions job summary. Implemented in workflow; run verification pending. Legacy GitHub commit statuses and Actions check runs are distinct, so no-run conclusions must not be drawn from empty legacy status results.


## Unary operation progress

Unary negation is supported in the restricted unchecked bit-vector verifier; checked unary negation is rejected. Regression tests added, CI unverified.

## CI aggregate quality gate

SC-067 (P0): A single CI result must enforce successful completion of all six compilation, runtime, solver, verifier, CLI and HTML checks. Configured as `ci-gate`, awaiting verified execution. No branch-protection rule has been applied.

## Verification argument evaluation guard

SC-066 (P0): Reject unmodeled argument evaluation in runtime contract calls. Current implementation requires compile-time constant messages and nonblank constant IDs; regression tests committed, execution unverified. Broader argument purity verification remains open.

## CI diagnostics correction

SC-065 updated: the compile-all job must attempt all nine projects and must not stop after the first compiler failure. Status: workflow change committed; successful execution unverified. `build.log` and `summary.log` are retained in the artifact.

## Build stabilization increment

SC-065: Explicitly compile every production and test project in CI and retain compiler output as an artifact (implemented in workflow; successful execution unverified). All previous requirements remain open until green build evidence exists.

## CI diagnostic artifact milestone

Added TRX output and failure-tolerant artifact collection to runtime, solver, and verification test jobs. This enables independent inspection of test failures without inferring success from committed source. Status: CI configured; green run unverified.

## Milestone 18 — verified-report CI gate

SC-064 (P0): CI must parse JSON output and verify version, classification and exit status for positive and negative proof cases. Implemented in workflow/scripts; execution still unverified.

## Milestone 17 — structured report contract

Added `ProofReport` schema v1, distinct outcome taxonomy, JSON CLI flag, and report unit tests. SC-062 structured machine-readable reporting: implementation committed; CI verification outstanding. SC-063 distinct exit codes for UNKNOWN and contradictory assumptions: implementation committed.

## Milestone 16 — contract feasibility

SC-061 (P0): Check joint precondition satisfiability and reject vacuous proof success. Implementation and regression tests committed; CI execution unverified.

## Milestone 15 — solver hardening

Bound proof queries have a five-second solver timeout. Numeric conversion/comparison lowering now checks for supported signed/unsigned bit-vectors; decimal and custom overloaded operator rejection tests added. Verification status remains **not independently compiled or executed**.

## Milestone 14

Fixed unsigned 64-bit literal lowering and added two regression tests. Build and test execution are still unverified. Numeric semantics remain partially implemented.

## Milestone 13 — differential correctness tests

Added CLR-versus-Z3 differential test suite for integer boundaries and exhaustive byte-pair promotion. CI now prints normal test output. Requirements for end-to-end semantic validation are partially addressed; successful .NET build and test runs remain unverified.

| ID | Requirement | Priority | Status |
|---|---|---|---|
| SC-058 | Compare solver outcomes against compiled C# boundary cases | P0 | Tests committed; execution unverified |
| SC-059 | Exhaustively validate byte addition promotion | P0 | 65,536 input combinations covered in test source |
| SC-060 | Gate correctness claims on a successful CI run | P0 | Open; no green run confirmed |

## Milestone 12 — executable entry point

Added a verification CLI, sample source files, and a GitHub Actions job that requires the safe example to exit 0 and the counterexample to exit 1. README, technical introduction, tutorial, and HTML demo are updated. This is implementation-complete in source but **not verified by a successful build or workflow run**.

| ID | Requirement | Priority | Status |
|---|---|---|---|
| SC-055 | CLI for verifying a source method and reporting outcomes | P0 | Implemented in source; build unverified |
| SC-056 | CI smoke tests for proof and counterexample CLI outcomes | P0 | Workflow committed; run unverified |
| SC-057 | Contract exit statuses distinguish proved, counterexample and unsupported | P0 | Implemented in source; tests pending |

## Tutorial and documentation delivery

Five-chapter standalone HTML tutorial added at `tutorial/index.html`, linked from README, main demo, and introduction. CI workflow checks tutorial JavaScript syntax. This is an educational frontend, not a browser-hosted solver.

## Milestone 11 progress — compiler-bound bit-vectors

Introduced `BoundNumericVerifier`: actual `IOperation` lowering to Z3 bit-vector ASTs with bound primitive numeric widths, explicit sign/zero extension and signed/unsigned comparisons. Added tests for byte promotion, signed 32-bit overflow and unsigned 64-bit wraparound, and conservative checked-operation rejection. Parameter-only straight-line contract calls are supported; no general control flow or locals. Compilation and CI remain unverified.

SC-049: semantic numeric descriptors now feed a limited solver backend (partial).
SC-050: promotion-aware lowering added for supported integer conversions (partial); checked conversions and complete C# numeric rules remain open.

## Milestone 10 progress

Added `NumericSemanticInspector`, using Roslyn `IBinaryOperation` and semantic type symbols to report promoted result widths, unsigned/signed types, checked expressions and user-defined operators. Added four regression tests. This is an audit/preflight, **not an integrated generalized solver**. No confirmed green .NET CI run.

Updated SC-049: Roslyn type classification plus operation binding implemented; verifier integration remains open.
Updated SC-050: Promotion identification implemented; promotion-aware SMT lowering remains open.

## Milestone 9 progress — user-defined numeric types

Implemented Roslyn semantic numeric-type descriptors and an explicit custom operator rejection policy, plus tests and the custom-types design document. Pending: integration into BV lowering, operator-body proofs, C# promotion/conversion rules, structs and generic math verification. Existing proof limitations and unverified CI remain in force.

## Additional traceable requirements

| ID | Requirement | Priority | Status |
|---|---|---|---|
| SC-049 | Infer numeric width and signedness from Roslyn semantic symbols | P0 | Partial: metadata classification only |
| SC-050 | Respect bound C# promotions and checked conversions | P0 | Planned |
| SC-051 | Model custom structs and invariant-bearing fields | P0 | Planned |
| SC-052 | Verify user-defined operator implementations and contracts | P0 | Planned; policy rejects unmodeled operators |
| SC-053 | Support generic math interfaces without assuming algebraic laws | P1 | Planned |
| SC-054 | Model custom implicit/explicit conversion methods | P0 | Planned |

## Milestone 8 progress

Added BV64 if/else joins through SMT ite, with tests for successful clamp, else-path counterexample, and unsupported patterns. Checked arithmetic within branches is explicitly rejected to prevent unsound overflow claims. Tests committed but .NET CI outcome not confirmed.

# SharpContracts Requirements Baseline v0.1

M0-Implemented means prototype support, not proof of verified coverage.

## Authoring and semantics

| ID | Requirement | Priority | Status |
|---|---|---|---|
| SC-001 | Named require preconditions with machine-readable violations | P0 | M0-Implemented |
| SC-002 | Named ensure postconditions with machine-readable violations | P0 | M0-Implemented |
| SC-003 | Named object invariants checked at specified boundaries | P0 | M0-Implemented |
| SC-004 | Old snapshot of value types at method entry | P0 | M0-Implemented |
| SC-005 | Define old semantics for immutable and mutable reference graphs | P0 | Planned |
| SC-006 | Named result and exceptional postconditions | P0 | Planned |
| SC-007 | Explicit check ordering, side-effect policy and exceptions | P0 | Partial: typed boundary ordering; exception semantics incomplete |
| SC-008 | Contract inheritance and Liskov-compatible override rules | P0 | Planned |
| SC-009 | Nullable and generic contracts | P0 | Planned |
| SC-010 | Pure expressions and side-effect detection | P0 | Planned |
| SC-011 | Async Task/ValueTask pre/postconditions | P1 | Planned |
| SC-012 | Cancellation, reentrancy and thread-safety contracts | P1 | Planned |
| SC-013 | Overflow, float, decimal and NaN policies | P0 | Planned |
| SC-014 | Grouping, severity and configurable enablement | P1 | Planned |

## Development tooling

| ID | Requirement | Priority | Status |
|---|---|---|---|
| SC-015 | Roslyn analyzer with stable diagnostics and code fixes | P0 | Partial: SC1001–SC1003, no fixes |
| SC-016 | Method injection via verified weaving or transformation | P0 | Partial: DispatchProxy interface checks; no general weaving |
| SC-017 | Incremental source generation where additive code suffices | P1 | Planned |
| SC-018 | xUnit automated behavioral tests and negative cases | P0 | Implemented: initial tests; CI pending verification |
| SC-019 | NSubstitute sequence-sensitive dependency verification | P1 | Planned |
| SC-020 | Testcontainers database integration tests | P1 | Planned |
| SC-021 | Stable source locations, structured errors and JSON/SARIF | P0 | Planned |
| SC-022 | Documentation site, runnable examples and IDE integration | P0 | Partial: M1 HTML and introduction; no hosted site |
| SC-023 | Deterministic fuzz and property-based generation | P1 | Planned |
| SC-024 | Contract coverage and mutation testing | P1 | Planned |

## Formal verification

| ID | Requirement | Priority | Status |
|---|---|---|---|
| SC-025 | Typed contract IR and serialization | P0 | Partial: symbolic AST and runtime definitions; no stable serialization schema |
| SC-026 | Explicitly documented verifiable C# subset | P0 | Planned |
| SC-027 | Verification conditions from contract semantics | P0 | Partial: Roslyn straight-line VC extraction plus Z3 |
| SC-028 | Z3/SMT bounded verification integration | P0 | Partial: actual Z3 proof/CE API with timeout; no full VC extraction |
| SC-029 | Loop invariants and termination variants | P0 | Planned |
| SC-030 | Quantifiers, arrays, collections, frame conditions | P1 | Planned |
| SC-031 | Map counterexamples into source and executable tests | P0 | Planned |
| SC-032 | Distinguish proved, falsified, timeout, unsupported, unknown | P0 | Planned |
| SC-033 | Dafny adapter for selected pure algorithms | P1 | Planned |
| SC-034 | Optional Boogie backend and translation validation | P1 | Planned |
| SC-035 | Versioned shared expression schema independent of VeloFormal | P1 | Planned |
| SC-036 | Soundness regression corpus and solver cross-checks | P0 | Planned |

## Safety, operations and adoption

| ID | Requirement | Priority | Status |
|---|---|---|---|
| SC-037 | Runtime overhead benchmarks and instrumentation | P1 | Planned |
| SC-038 | Failure redaction, telemetry and OpenTelemetry correlation | P1 | Planned |
| SC-039 | API versioning and compatibility policy | P0 | Planned |
| SC-040 | Trust model for unsafe, native, dynamic and reflective code | P0 | Planned |
| SC-041 | Contract disabling must not alter behavior | P0 | Planned |
| SC-042 | Reproducible CI, offline where possible | P0 | Planned |
| SC-043 | Requirement-to-test-to-code traceability | P0 | Planned |
| SC-044 | Binary and package supply chain policy | P1 | Planned |
| SC-045 | Interface contracts for persistence and external services | P1 | Planned |
| SC-046 | Separate assumptions, guarantees and environmental constraints | P0 | Planned |
| SC-047 | Transaction rollback/atomicity on observed failure | P0 | Planned |
| SC-048 | Explain runtime detection versus mathematical proof | P0 | M0-Implemented |

## M0 limitations

Explicit Contract calls only; no automatic weaving, static proofs or test generation. Old<T> copies values only, not referenced graphs. Fault-injected transfer mutates state before detection, illustrating SC-047. Browser prototype is JavaScript rather than a C# runtime. C# has not been compiled in the original environment.
\n## Milestone 7 progress

Introduced separate signed 64-bit BV verifier using Z3, unchecked wrapping arithmetic and explicit checked add/sub overflow obligations. Added boundary tests. Older branch-sensitive verifier remains mathematical-integer based; unified compiler-aware semantics and CI verification are still open.

## Milestone 6 progress

Branch-sensitive `if/else` symbolic execution added, with independent path conditions, multi-path postcondition checks, parse-error handling, `AllProved` and failure-atomic reporting. New regression tests detect a defect in only one branch and reject unsupported loops even if earlier checks passed. Still uses ideal mathematical integer semantics; not proof of CLR execution. CI is not independently verified.

## Milestone 5 progress

Source-level method extraction and Z3 proof/counterexample reporting added; restricted straight-line long subset, mathematical-integer semantics (not CLR overflow), no verified build yet.

## Milestone 4 progress\n\nActual Microsoft.Z3 solver API; UNSAT proves encoded implications, SAT returns raw counterexample model, UNKNOWN is kept distinct. Dedicated Z3 test project and CI job added. .NET execution remains unverified. Model semantics limited to mathematical Int/Real rather than .NET overflow/decimal rounding.\n\n## Milestone 3 progress\n\nTyped symbolic expression AST, runtime evaluator, SMT-LIB rendering, and xUnit coverage added. SMT-LIB query asks satisfiability, not validity. No Z3 binding, formal proof, persistent IR schema, or fully aligned decimal/SMT arithmetic semantics.\n\n## Milestone 2 progress\n\nTyped reusable runtime contract definitions, explicit pre/postcondition wrapper, synchronous interface return-value postcondition, and related xUnit tests. Contract expressions are executable delegates rather than formal symbolic IR. Postconditions have no automatic rollback.\n\n## Milestone 1 progress\n\nRuntime unit tests, interface-boundary attribute enforcement, narrow Roslyn analyzer diagnostics SC1001-SC1003, CI workflow, and HTML analyzer explorer committed. Automatic method weaving, Roslyn code fixes and proof backends remain open.\n

## SC-089 / SC-090 current acceptance

StrictDoc requirements/SharpContracts.sdoc is authoritative. SC-089 verifies ordinary UInt32 local assignment with immutable snapshots; SC-090 verifies promoted <<, >> and >>>, including negative/oversized count masking. The 50-case ledger is ITERATION_50_LOG.md. SC-088 remains partial: CFG/SSA joins, branches and early returns have not migrated. Full coverage/mutation/seed evidence is in ENGINEERING-2026-10-24.md; no native-Z3 coverage or general C# soundness is claimed.
