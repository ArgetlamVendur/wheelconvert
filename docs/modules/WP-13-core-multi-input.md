# WP-13: Core contracts for multi-input targets and the v0.2 target set

**Builder:** Sonnet · **Reviewer:** Opus (fresh session) · **Depends on:** WP-01, WP-11 · **Risk:** **High** (frozen contracts) · **Blocks:** WP-14, 15, 16, 18, 19, 20

> **User approval needed before starting.** This WP changes frozen contracts (AGENTS.md §4.1). The approval is ADR-011 moving from `Proposed` to `Accepted`, set by the user.

## Goal
Implement ADR-011 in Core and update the docs, so converter WPs can add the new targets without touching contracts.
No converter implements a new target here: after this WP the app looks and behaves exactly as before (no converter lists the new IDs yet, so their segments stay hidden).

## Owns
- `src/WheelConvert.Core/Contracts/ConversionTarget.cs`, `ConversionRequest.cs`, `AppServices.cs` (add `IResizePrompt`, `ResizeChoice`)
- `src/WheelConvert.Core/Formats/TargetCatalog.cs`, `FormatRegistry.cs`
- `src/WheelConvert.Core/Jobs/DropPlanner.cs`
- `src/WheelConvert.Core/Output/OutputLabels.cs`
- `tests/WheelConvert.Core.Tests/**`
- `docs/CONTRACTS.md` (C1, C2, C4, C6, C8), `docs/decisions/ADR-011-*.md` (status line only, after the user accepts)
- The app coordinator that calls `IPageOrderPrompt` (find it: `git grep -n IPageOrderPrompt src/`) **and** `CompositionRoot.cs`: only to add the `IResizePrompt` constructor parameter, passing a stub `NotImplementedResizePrompt` that returns null (real dialog is WP-19)

## Must not touch
Any converter project. `IConverter.cs`, `IOutputWriter.cs`, `IWheelView.cs`. `Directory.Build.props`.

## Requirements
1. `ConversionTarget`: append `bool CombinesInputs = false, int MinInputs = 1, int? MaxInputs = null`. Validate `MinInputs >= 1` and `MaxInputs is null or >= MinInputs`. Update the XML doc, including the new meaning of `OutputExtension = null` with `ProducesText = false` (ADR-011 §4).
2. `ConversionRequest`: append `IReadOnlyList<string>? AdditionalSourcePaths = null`; add `AllSourcePaths`. `Create(...)` unchanged.
3. Add the new `OptionKeys` from ADR-011 §5 (constants only, no string literals elsewhere).
4. `TargetCatalog`: add the 7 targets from ADR-011 §6 to the fields **and** to `All`. Labels ≤ 8 characters (C8). Set `MaxInputs: 1` on the five PDF targets in ADR-011 §8 (nothing else about them changes).
5. `OutputLabels`: add `Combined = "combined"`, `Merged = "merged"`, `Smaller = "smaller"`, `Resized = "resized"`.
6. `FormatRegistry.GetTargetsFor(paths, …)`: hide a target when the number of dragged paths whose extension the converter `CanConvert` is below `MinInputs` **or above `MaxInputs`**. Extension checks only: no `File.Exists`, no I/O (it runs while the wheel is open).
7. `DropPlanner.Plan` for `CombinesInputs` targets: per-file filters as today → natural sort (ADR-011 §3) → one request, `SourcePath` = first, the rest in `AdditionalSourcePaths`. Below `MinInputs` → no request, accepted files skipped with `"Drop at least N files for <Label>."`. Any target: more accepted files than `MaxInputs` → no request, files skipped with ADR-011 §3's message. Otherwise non-combining targets: behaviour **byte-for-byte unchanged** (existing tests must pass untouched).
8. Natural sort helper in Core (`Jobs/NaturalFileNameComparer.cs`): digit runs compared as numbers (`2` < `10`), otherwise `StringComparer.OrdinalIgnoreCase`, ties by full path. Pure, no P/Invoke (`StrCmpLogicalW` is Windows-only and Core is cross-platform).
9. Null-safety sweep for `OutputExtension` (ADR-011 §4): `git grep -n "OutputExtension" src/`; list every hit in the PR description with what you changed or why it is already safe.
10. `CONTRACTS.md`: document all of the above (new rows in C2's label table, C4 multi-file rule for combining targets, C6 `image.resize`, C8 label list).

## Gotchas
- Adding parameters to a positional `record` changes its `Deconstruct` and `ToString`. Grep for deconstruction of `ConversionTarget`/`ConversionRequest` and settings that serialise them (settings store target **IDs** only; confirm).
- `DropPlanner` already de-duplicates paths case-insensitively; keep that before sorting.
- A combining drop of 40 images is one job: the queue's progress and cancellation work per job, which is fine.

## Acceptance tests
| # | Who | Test | Expected | Result |
|---|---|---|---|---|
| 1 | Agent | All existing Core and App tests | Pass unchanged |  PASS (Core 84/84, App 118, Image 55, Media 123, Pdf 64; two catalog-count assertions and one Pdf converter list assertion adjusted, see PR) |
| 2 | Agent | `NaturalFileNameComparer` on `scan 10, scan 2, Scan 1, scan 02` | `Scan 1, scan 02, scan 2, scan 10` (02 before 2: tie broken by full path) |  PASS (Natural_Sort_ScanNames) |
| 3 | Agent | `DropPlanner` + `pdf.merge`, 3 PDFs dropped in random order | One request; `AllSourcePaths` naturally sorted |  PASS (Plan_Merge_ThreePdfsRandomOrder_OneSortedRequest) |
| 4 | Agent | `pdf.merge` with 1 PDF + 1 JPG | No request; PDF skipped "Drop at least 2 files for Merge."; JPG skipped "Not a Pdf file." |  PASS (Plan_Merge_OnePdfAndOneJpg_NoRequest) |
| 5 | Agent | `image.to-pdf` with 1 image | One request, no additional paths |  PASS (Plan_ImageToPdf_OneImage_OneRequestNoAdditional) |
| 6 | Agent | `GetTargetsFor` with 1 PDF (fake converter listing `pdf.merge`) | Merge hidden; with 2 PDFs shown |  PASS (GetTargetsFor_Merge_NeedsTwoPdfs) |
| 7 | Agent | `IsRedundantFor` on `image.smaller` / `image.resize` with `.jpg` | false |  PASS (IsRedundantFor_SmallerAndResize_NeverRedundant) |
| 8 | Agent | Every `TargetCatalog.All` label ≤ 8 chars, IDs unique | Pass |  PASS (Catalog_LabelsFitAndIdsUnique) |
| 8a | Agent | `GetTargetsFor` with 2 PDFs (fake converter listing all PDF targets) | Reorder, Halves, Odd, Even, Reverse hidden; Split, Images, Rotate, Merge shown |  PASS (GetTargetsFor_TwoPdfs_HidesSingleFileTargets) |
| 8b | Agent | `DropPlanner` + `pdf.reverse` with 2 PDFs | No request; both skipped "Drop one file at a time for Reverse." |  PASS (Plan_Reverse_TwoPdfs_NoRequestBothSkipped) |
| 9 | User | Install the build, use the wheel on an image, a PDF, a video | Same segments as before this WP (no new ones yet) | NEEDS USER |
| 10 | User | Drag two PDFs | Only Split, Images, Rotate (the five single-file ones are gone) | NEEDS USER |

## Review focus
Contract changes are additive only; no behaviour change for existing targets; `GetTargetsFor` does no I/O; the docs match the code.

## Out of scope
Implementing any new target. The resize dialog (WP-19).

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md, docs/decisions/ADR-011-multi-file-targets.md, docs/modules/WP-13-core-multi-input.md.
Confirm ADR-011's status is Accepted; if it is still Proposed, stop and ask the user.
You are the builder for WP-13. Create branch wp-13-core-multi-input from main. Restate the plan in 3–5 lines and wait for "go".
```
