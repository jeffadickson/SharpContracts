# 100-iteration semantic development ledger

Date: 2026-10-22. An iteration is one named requirement/semantic case, not one commit or a claim that 100 separate features were implemented. The corpus is `SharpContracts.Verification.Tests/fixtures/semantic100.json`; xUnit enforces 100 sequential unique IDs/names. Source is compiled by Roslyn, translated through the typed IR and checked by native Z3. SAT witnesses replay against compiled original C# with the expected failing contract ID. Proved cases additionally execute CLR boundaries; those samples are not the proof.

## Build checkpoints

All ten solution build checkpoints passed. Local logs are in `artifacts/iterations/build-NNN.log` (ignored build artifacts). Test checkpoints ran the accumulated cases; early defects were retained and corrected rather than deleting cases.

| Iteration | Build | Accumulated semantic test result |
|---|---|---|
| 010 | Pass | Fixture enum deserialization failed; corrected with explicit enum converter |
| 020 | Pass | 20 passed |
| 030 | Pass | 30 passed |
| 040 | Pass | 40 passed |
| 050 | Pass | 50 passed |
| 060 | Pass | 59 passed, case 052 failed: Roslyn implicit constant conversion |
| 070 | Pass | 69 passed, case 052 still failed in pre-fix build |
| 080 | Pass | 80 passed after constant-conversion correction |
| 090 | Pass | 90 passed |
| 100 | Pass | Complete suite: 322 passed, 0 failed, 0 skipped |

## Review checkpoints

- 015: Reviewed typed scalar identity and backend dispatch; reject source-defined runtime symbol lookalikes and preserve the old AST/models.
- 030: Reviewed sign/zero extension and narrowing. Consolidated proof queries and preserved earlier counterexamples when later checks are unreachable.
- 045: Reviewed unsigned division totality and compiler constant expressions. Signed/zero/variable division remains rejected; constant operands are translated before materialization.
- 060: Reviewed backend malformed-node rejection, checked flags and scalar type mismatches; added 26 backend tests. Identified the implicit constant conversion case.
- 075: Reviewed constant conversion repair, source side effects, unsupported suffixes, checked contexts, and the dynamically compiled CLR replay harness.
- 090: Reviewed runtime-symbol identity, report precedence, inconsistent preconditions, no-contract results and unreachable checks. Added explicit UNKNOWN assertions for later unreachable obligations.
- Final deep review: Reviewed all new production files and both integration diffs; checked numeric width/signedness, bitwise and Boolean operations, conversion flags, supported division, solver push/pop disposal, counterexample signed decoding, source lookalike rejection, fixture completeness, legacy-model separation, CI coverage and documentation boundaries. Full-suite, CLI and HTML checks precede the final commit. The test log filename collision seen during the iteration-100 solution run is corrected by using separate project TRX paths for final evidence.

## Case ledger

| Iteration | Requirement/semantic case | Expected report |
|---|---|---|
| 001 | Int32 addition wraps | Counterexample |
| 002 | UInt32 addition wraps | Counterexample |
| 003 | Int64 addition wraps | Counterexample |
| 004 | UInt64 addition wraps | Counterexample |
| 005 | SByte addition promotes | Proved |
| 006 | Byte addition promotes | Proved |
| 007 | Int16 addition promotes | Proved |
| 008 | UInt16 addition promotes | Proved |
| 009 | Additive identity | Proved |
| 010 | Subtractive cancellation | Proved |
| 011 | Signed Int32 comparison excludes unsigned reinterpretation | Counterexample |
| 012 | Unsigned UInt32 values nonnegative | Proved |
| 013 | Signed Int64 comparison excludes unsigned reinterpretation | Counterexample |
| 014 | Unsigned UInt64 comparison | Proved |
| 015 | Byte to Int32 comparison promotion | Proved |
| 016 | Int16 sign extension preserves negatives | Counterexample |
| 017 | UInt16 zero extension preserves positives | Proved |
| 018 | Signed comparison partition | Proved |
| 019 | Unsigned comparison partition | Proved |
| 020 | Int64 bound includes its minimum | Proved |
| 021 | Signed widening to Int64 | Proved |
| 022 | Unsigned widening to UInt64 | Proved |
| 023 | Int32 to UInt64 sign extension | Counterexample |
| 024 | UInt32 to Int64 zero extension | Proved |
| 025 | Int32 narrowing to byte | Counterexample |
| 026 | UInt64 narrowing wrap | Counterexample |
| 027 | Same-width signed cast changes ordering | Counterexample |
| 028 | Signed byte narrowing roundtrip | Proved |
| 029 | UInt16 widening roundtrip | Proved |
| 030 | Negative narrow cast retains low bits | Proved |
| 031 | Unary plus preserves value | Proved |
| 032 | Double negation wraps consistently | Proved |
| 033 | Int32 negation minimum overflow | Counterexample |
| 034 | UInt32 unary minus promotes Int64 | Proved |
| 035 | Bitwise complement involution | Proved |
| 036 | Bitwise AND identity | Proved |
| 037 | Bitwise OR identity | Proved |
| 038 | Bitwise XOR self zero | Proved |
| 039 | Boolean NOT over comparison | Proved |
| 040 | Boolean bitwise conjunction is pure | Proved |
| 041 | Multiplication by zero | Proved |
| 042 | Multiplication by one | Proved |
| 043 | Multiplication wraparound counterexample | Counterexample |
| 044 | UInt32 division by one | Proved |
| 045 | UInt64 division nonincreasing | Proved |
| 046 | Unsigned constant-expression divisor | Proved |
| 047 | Division truncation loses odd remainder | Counterexample |
| 048 | UInt64 maximum constant retained | Proved |
| 049 | Pure Boolean XOR partition | Proved |
| 050 | Narrow operand multiplication promotes | Proved |
| 051 | Inline initialized local substitution | Proved |
| 052 | Inline implicit constant conversion | Proved |
| 053 | Inline widening and narrowing identity | Proved |
| 054 | Inline unchecked same-width signed conversion | Proved |
| 055 | Inline unsigned complement | Proved |
| 056 | Inline local wrap counterexample | Counterexample |
| 057 | Inline contract supports signed widening | Proved |
| 058 | Inline pure Boolean conjunction | Proved |
| 059 | Inline local multiplication zero | Proved |
| 060 | Inline multiple initialized locals | Proved |
| 061 | Checked Int32 add rejected | Unsupported |
| 062 | Checked UInt32 subtract rejected | Unsupported |
| 063 | Checked Int64 multiply rejected | Unsupported |
| 064 | Checked Int32 negate rejected | Unsupported |
| 065 | Checked narrowing conversion rejected | Unsupported |
| 066 | Unsigned zero divisor rejected | Unsupported |
| 067 | Variable divisor rejected | Unsupported |
| 068 | Signed division overflow not silently modeled | Unsupported |
| 069 | Signed constant division not modeled | Unsupported |
| 070 | Exception in short circuit remains unsupported | Unsupported |
| 071 | Inline assignment fails closed | Unsupported |
| 072 | Inline branch fails closed | Unsupported |
| 073 | Inline loop fails closed | Unsupported |
| 074 | Unmodeled invocation in predicate rejected | Unsupported |
| 075 | Dynamic contract message rejected | Unsupported |
| 076 | Unsupported suffix discards earlier proof | Unsupported |
| 077 | Late precondition rejected | Unsupported |
| 078 | Post-return statements rejected | Unsupported |
| 079 | Uninitialized local rejected | Unsupported |
| 080 | Unmodeled decimal conversion rejected | Unsupported |
| 081 | Bound inconsistent assumptions distinct | InconsistentAssumptions |
| 082 | Inline inconsistent assumptions distinct | InconsistentAssumptions |
| 083 | No-contract method never proved | NoContracts |
| 084 | Inline no-contract method unsupported | Unsupported |
| 085 | Source-defined Contract lookalike rejected by bound engine | Unsupported |
| 086 | Source-defined Contract lookalike rejected by inline engine | Unsupported |
| 087 | Source-defined VerifyAttribute rejected | Unsupported |
| 088 | Bound unreachable check preserves earlier failure | Counterexample |
| 089 | Inline unreachable check preserves earlier failure | Counterexample |
| 090 | Precondition after postcondition discards partial proof | Unsupported |
| 091 | Char witness uses promoted unsigned code unit | Counterexample |
| 092 | Custom overloaded operators not primitive | Unsupported |
| 093 | Floating-point parameter not a bit vector | Unsupported |
| 094 | Nullable lifted arithmetic not modeled | Unsupported |
| 095 | Dynamic binding never approximated | Unsupported |
| 096 | Reference heap access never approximated | Unsupported |
| 097 | Optional parameters rejected | Unsupported |
| 098 | Inline by-reference parameters rejected | Unsupported |
| 099 | Checked statement block not silently flattened | Unsupported |
| 100 | Mixed UInt32 Int32 promotes to Int64 | Proved |

## Remaining scope

The compiler-bound entry points share the new expression and query core, but they still have separate statement frontends. The legacy mathematical-integer branch engine and BV64 prototype remain unchanged. CFG/SSA, mutable locals, branching, checked exceptions, custom operators, generic math, heap reasoning, IR serialization and true exit-time postconditions remain open. No general C# soundness claim or VeloFormal syntax migration is made.
