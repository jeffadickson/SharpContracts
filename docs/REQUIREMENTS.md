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
| SC-007 | Explicit check ordering, side-effect policy and exceptions | P0 | Planned |
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
| SC-025 | Typed contract IR and serialization | P0 | Planned |
| SC-026 | Explicitly documented verifiable C# subset | P0 | Planned |
| SC-027 | Verification conditions from contract semantics | P0 | Planned |
| SC-028 | Z3/SMT bounded verification integration | P0 | Planned |
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
\n## Milestone 1 progress\n\nRuntime unit tests, interface-boundary attribute enforcement, narrow Roslyn analyzer diagnostics SC1001-SC1003, CI workflow, and HTML analyzer explorer committed. Automatic method weaving, Roslyn code fixes and proof backends remain open.\n