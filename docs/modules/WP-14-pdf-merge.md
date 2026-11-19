# WP-14: Merge PDFs

**Builder:** Sonnet · **Reviewer:** Opus (fresh session) · **Depends on:** WP-13 · **Risk:** low · **Same project as:** WP-20 (don't run in parallel)

## Goal
Select two or more PDFs, drag, drop on **Merge** → one PDF with all pages, files in natural name order.

## Owns
- `src/WheelConvert.Converters.Pdf/**` (new class `PdfMerger.cs`; add `pdf.merge` to `SupportedTargetIds`)
- `tests/WheelConvert.Converters.Pdf.Tests/**`

## Requirements
1. Uses PDFsharp `[6.2.4]`, already pinned. **No new package. Never upgrade PDFsharp** (6.5.0+ is AGPL).
2. Open every input with `PdfReader.Open(path, PdfDocumentOpenMode.Import)`; never `Modify` (CONTRACTS C2 S1). Import every page in `request.AllSourcePaths` order into one new `PdfDocument`.
3. Write to `output.CreateTempPath(request.SourcePath, ".pdf")`; commit with `OutputLabels.Merged` → `first (merged).pdf` in the first file's folder.
4. Fewer than 2 inputs (shouldn't happen after WP-13) → `Fail(Unsupported, "Drop two or more PDFs to merge.")`.
5. Any input password-protected → `Fail(PasswordProtected, "<file name> is password-protected.")`; unreadable → `Fail(CorruptInput, "Couldn't read <file name>.")`. Nothing committed, temp discarded.
6. Progress: report after each input file (`i / n`). Check the cancellation token between files and between pages of large files.
7. A source vanished between drop and job → `SourceMissing` naming the file.

## Gotchas
- Bookmarks/outlines and document-level metadata from inputs are not carried over: acceptable in v1, mention in README.
- Two inputs with form fields of the same name can clash; PDFsharp keeps both pages. Acceptable; note in the PR.
- Total size is the sum of inputs; no recompression here (that's WP-20).

## Acceptance tests
| # | Who | Test | Expected | Result |
|---|---|---|---|---|
| 1 | Agent | Merge 2-page + 3-page (generated) | 5 pages, in order, sources unchanged (hash) | PASS (PdfMergerTests) |
| 2 | Agent | Inputs `doc 10.pdf`, `doc 2.pdf` (via DropPlanner) | `doc 2` pages first, output `doc 2 (merged).pdf` | PASS (PdfMergerTests) |
| 3 | Agent | One input encrypted | `PasswordProtected` naming it, no temp left | PASS (PdfMergerTests) |
| 4 | Agent | One input garbage bytes | `CorruptInput` naming it, no temp left | PASS (PdfMergerTests) |
| 5 | Agent | Cancel during a 3 × 100-page merge | `OperationCanceledException`, no temp left | PASS (PdfMergerTests) |
| 6 | Agent | Inputs in two different folders | Output in first file's folder only | PASS (PdfMergerTests) |
| 7 | User | In `wc-test`, select 3 PDFs, Shift-drag, drop on Merge | One merged PDF next to the first, opens in Edge | NEEDS USER |
| 8 | User | Drag a single PDF | No Merge segment | NEEDS USER |
| 9 | User | Wheel readability: drag 1 PDF and hit Reverse, then Rotate, 5 times each; drag 3 PDFs and hit Merge 5 times | 15/15 land on the intended segment. Fewer → record it; a later ADR moves the least-used PDF targets (ADR-011 §8) | NEEDS USER |

## Out of scope
Choosing the order by hand (later: reuse the WP-09 window). Merging images and PDFs together.

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md (C1, C2), docs/decisions/ADR-011-multi-file-targets.md, docs/modules/WP-14-pdf-merge.md.
You are the builder for WP-14. Create branch wp-14-pdf-merge from main. Restate the plan in 3–5 lines and wait for "go".
```
