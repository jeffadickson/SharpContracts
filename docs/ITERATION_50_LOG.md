# 50-iteration implementation and validation ledger — 2026-10-24

The assignment implementation was committed first as c809264c0fd9101909b9bb1e3ef2640f834e2ab7. The following 50 iterations are named source-derived semantic/negative cases for SC-090, not 50 commits or 50 independent features. Native Z3 runs over Roslyn-derived typed expressions; successful counterexample tests replay the actual model against the exact compiled C# source.

| Checkpoint | Compilation | Executed result |
|---|---|---|
| 10 | Passed | 10 source cases passed |
| 20 | Passed | 20 source cases passed |
| 30 | Passed | 30 source cases passed |
| 40 | Passed | 40 source + 8 backend + 1 property tests passed |
| 50 | Passed | Complete verification suite: 344 tests passed under coverage |

Reviews checked mask width, negative count bits, signed arithmetic versus unsigned logical right shift, explicit >>>, narrow-type promotion, alias rejection, constant-folding and checked-context behavior. The final deep review also corrected stale mutation-report reuse by isolating each run and requiring an executed baseline TRX, and preserved CLI source-link/input validation when replacing the Python CLI check with PowerShell.

| Iteration | Semantic case | Expected outcome |
|---|---|---|
| 01 | UInt32 left shift zero | Proved |
| 02 | UInt32 left shift count 32 masks to zero | Proved |
| 03 | UInt32 left shift count 33 masks to one | Proved |
| 04 | UInt32 left shift count negative one | Proved |
| 05 | UInt32 left shift count negative 32 | Proved |
| 06 | Int32 left shift count 32 | Proved |
| 07 | Int32 left shift count 33 | Proved |
| 08 | Int32 left shift negative count | Proved |
| 09 | UInt32 left shift can decrease | Counterexample |
| 10 | Int32 left shift can decrease | Counterexample |
| 11 | Int32 arithmetic right keeps sign | Counterexample |
| 12 | Int32 right shift count 32 | Proved |
| 13 | Int32 right negative count | Proved |
| 14 | UInt32 right count 32 | Proved |
| 15 | UInt32 right cannot increase | Proved |
| 16 | UInt32 right count 33 | Proved |
| 17 | Int32 full arithmetic right is zero or minus one | Proved |
| 18 | UInt32 top bit right is zero or one | Proved |
| 19 | UInt32 negative right count | Proved |
| 20 | Int32 right count 64 | Proved |
| 21 | Int64 left shift count 64 | Proved |
| 22 | UInt64 left shift count 64 | Proved |
| 23 | Int64 left shift count 65 | Proved |
| 24 | UInt64 negative left count | Proved |
| 25 | Int64 negative right count | Proved |
| 26 | UInt64 right count 64 | Proved |
| 27 | Int64 right count 64 | Proved |
| 28 | UInt64 left shift can decrease | Counterexample |
| 29 | Int64 arithmetic sign extension | Proved |
| 30 | UInt64 logical high bit | Proved |
| 31 | Int32 unsigned right has explicit logical semantics | Proved |
| 32 | Int64 unsigned right has explicit logical semantics | Proved |
| 33 | UInt32 unsigned and ordinary right agree | Proved |
| 34 | UInt64 unsigned and ordinary right agree | Proved |
| 35 | Int32 unsigned right by one is nonnegative | Proved |
| 36 | Int64 unsigned right by one is nonnegative | Proved |
| 37 | Int32 unsigned right count 32 retains negative value | Counterexample |
| 38 | Int64 unsigned right count 64 retains negative value | Counterexample |
| 39 | UInt32 variable left count uses low five bits | Proved |
| 40 | UInt64 variable left count uses low six bits | Proved |
| 41 | Byte left shift promotes before shifting by eight | Proved |
| 42 | SByte arithmetic right uses sign extension | Proved |
| 43 | Int16 left shift retains signed Int32 behavior | Counterexample |
| 44 | UInt16 left shift can become negative Int32 | Counterexample |
| 45 | Char left shift can become negative Int32 | Counterexample |
| 46 | Checked context does not add shift overflow exceptions | Proved |
| 47 | Assigned UInt32 shift count 32 preserves value | Proved |
| 48 | Earlier local snapshot survives shifted assignment | Proved |
| 49 | Shifted parameter mutation remains unsupported | Unsupported |
| 50 | Shift count increment clears earlier partial proof | Unsupported |

Totals: 39 all-permitted-input proofs, 9 genuine solver counterexamples replayed by CLR execution, 2 unsupported cases. TRX output retains every structured solver report. FsCheck seed 20261024,1357911 drives 256 inputs across 12 promoted shift forms (3072 result comparisons); the boundary tests add 900 deterministic result comparisons. No property failure required minimization. These comparisons are regression evidence, not proof of complete translator soundness.
