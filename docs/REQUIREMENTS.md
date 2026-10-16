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