# Sales Showcase — 20 local iterations published

The two source-derived contract examples are now part of the repository:
- `examples/PaymentSplit.cs`: odd-cent conservation bug.
- `examples/BufferAdmission.cs`: admission bug caused by uint wraparound.
- `SharpContracts.Verification.Tests/SalesShowcaseTests.cs`: positive/negative inline-verifier tests.
- Fixture files are copied into test output by the verification test project.

## Validation
Code and tests have been committed, but successful .NET compilation and Z3 execution remain unconfirmed.

## User-visible demo
The original standalone HTML showcase and local development scripts remain packaged in the previous local artifact. They have **not** all been mirrored into the GitHub repository.

## Proposed next milestone
Expand the local regression corpus without running tests except at explicit iteration 50 and iteration 100, keeping subsequent commits local and unpushed.