# Inline C# contracts: implemented verification model

## Current Issue #2 increment: SC-091 / SC-092

The inline verifier now supports structured blocks, if/else, nested branches and early returns through the typed CLR IR, with independent local snapshots on each path. Complete translation includes dead arms; unsupported operations discard every partial result. Entirely unreachable contracts are UNKNOWN. Require remains an entry prefix and Ensure remains a source call-site check, which an early return can bypass. Bounded exploration is documented in [the engineering evidence](ENGINEERING-ISSUE2-CONTROLFLOW.md). Full CFG/SSA migration, loops, checked exceptions and custom types remain open. This supersedes earlier straight-line-only scope statements below.

Canonical requirements SC-091 and SC-092 are maintained in requirements/SharpContracts.sdoc. CI is temporarily disabled by user request; local execution remains required.

Canonical requirements: requirements/SharpContracts.sdoc. Expression semantics are shared with the parameter-only numeric entry point through ClrOperationLowerer, ClrExpression, ClrExpressionEncoder and ProofQuery. The older mathematical and BV64 engines remain separate, retained prototypes.

| Feature | Inline entry point |
|---|---|
| Authoring | Ordinary C#, runtime [Verify] attribute, Contract.Require/Assert/Ensure |
| Signature | Unique synchronous uint method with ordinary uint parameters and block body |
| Locals | One initialized ordinary uint local per declaration; simple assignment to existing ordinary uint locals |
| Updates | RHS uses the pre-assignment immutable symbolic state; captured locals do not change |
| Expressions | Compiler-promoted 8/16/32/64-bit integral values and Boolean expressions |
| Arithmetic | Unchecked +, -, *, conversions; unsigned division by a nonzero constant |
| Bit operations | AND/OR/XOR, unary complement, <<, >> and >>> with C# masking and signedness |
| Contracts | Nonblank constant IDs and constant messages; Require before executable statements |
| Return | Supported uint expression on every normal path; Ensure remains a check at its call site |
| Failure policy | Unsupported statements/operations clear all partial proof results |

Ref locals, parameter assignment, fields, aliases, compound/increment assignments, loops, custom operators, nullable lifting, heap state, signed/variable division, remainder, checked exceptions and method-level result contracts remain unsupported. SC-088 remains partial: structured paths are supported, but full CFG/SSA migration is outstanding.

SAT returns a source line, contract ID, raw Z3 model and decimal parameter inputs. Initial contradictory assumptions are inconsistent, never Proved. A later check unreachable after prior contract failures is Unknown and preserves the earlier counterexample. Solver UNKNOWN/timeouts cannot yield an overall Proved result.

C# shift counts use low five bits for 32-bit values and low six for 64-bit values. This mask is explicit in Z3 because SMT bit-vector shifts do not mask oversized counts. Sign extension applies only to signed >>; >>> always zero-fills. Narrow C# operands promote before shifting. Checked contexts do not make shifts overflow, while unmodeled checked operands remain rejected.

The numeric entry point accepts ordinary integral parameters and only Require/Ensure statements. It shares expressions, conversion rules, solver queries and witnesses with inline methods, but does not accept local statements. Sharing the expression core does not imply all statement/control-flow frontends have merged.

See ITERATION_50_LOG.md for all 50 semantic cases and build checkpoints, ENGINEERING-2026-10-24.md for measured tests/coverage/mutations/seeds, and LANGUAGE_SPEC.md for the preserved C# authoring language. VeloFormal's temporal syntax and a cross-project IR serialization format remain independent future work.
