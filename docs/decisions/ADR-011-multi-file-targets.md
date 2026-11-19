# ADR-011: Multi-input targets and the v0.2 target set
Status: Accepted · 2026-11-18 (user, via overnight-run authorisation) · Date: 2026-11-18

## Context
v0.1 maps one dropped file to one job (`ConversionRequest.SourcePath`, one `DropPlanner` request per file).
v0.2 adds features regular users ask for most (see the comparison with Citrus, 2026-11-18):
merge PDFs, several images → one PDF, a "Smaller" option, image resize, PDF → Word (text only), and a smaller PDF.
Two of these need **several files in, one file out**, and two need an output that keeps the source's format.
Both are impossible under the frozen contracts in `src/WheelConvert.Core/Contracts/` and `TargetCatalog.cs`.

## Decision
All changes are **additive**: every existing call site and constructor keeps compiling and behaving the same.

### 1. `ConversionTarget` gets two optional parameters (appended, with defaults)
| Parameter | Default | Meaning |
|---|---|---|
| `bool CombinesInputs` | `false` | All compatible dropped files become **one** request; the converter reads them all and writes one output. |
| `int MinInputs` | `1` | Fewest compatible files the target needs. `FormatRegistry.GetTargetsFor` hides the segment when fewer compatible files are being dragged (pure check on extensions; no file-system access). |
| `int? MaxInputs` | `null` (no limit) | Most compatible files the target accepts. `GetTargetsFor` hides the segment when more compatible files are being dragged (same pure check). Used to keep the wheel readable when several files are dragged (§8). |

### 2. `ConversionRequest` gets one optional parameter (appended, with default)
- `IReadOnlyList<string>? AdditionalSourcePaths = null`.
- New helper `IReadOnlyList<string> AllSourcePaths` = `SourcePath` followed by `AdditionalSourcePaths`.
- `SourcePath` stays the **first** file. Output is named after it and written to **its** folder (CONTRACTS C2 S6 unchanged: "the source's folder" means the first file's folder). The other inputs may live in other folders; they are only ever opened read-only (S1 applies to every input).

### 3. `DropPlanner.Plan`
- For `CombinesInputs` targets: apply the existing per-file filters, then build **one** request from the accepted files.
- Order: **natural sort by file name** (`scan 2.jpg` before `scan 10.jpg`, case-insensitive, ties broken by full path) so the result is predictable. Explorer's drag order is not reliable, so it is not used.
- Fewer accepted files than `MinInputs` → no request; every accepted file goes to `Skipped` with "Drop at least N files for <Label>.".
- More accepted files than `MaxInputs` (any target, combining or not) → no request; every accepted file goes to `Skipped` with "Drop one file at a time for <Label>." when `MaxInputs` is 1, otherwise "Drop at most N files for <Label>.". The wheel already hides such segments; this covers the hotkey path and any mismatch.
- **Images → PDF always combines** (decided by the user, 2026-11-18): dropping 5 images on PDF gives **one** 5-page PDF, never 5 PDFs. The main use is a portal that wants one file. For one PDF per image, drop the images one at a time. This is intended, not a bug.

### 4. Targets whose output keeps the source's format
- `OutputExtension = null` **and** `ProducesText = false` means "the converter chooses the extension (normally the source's own)". Such targets are never hidden as redundant (`IsRedundantFor` already returns false for null).
- Converters for these targets pass the chosen extension to `CreateTempPath`/`Commit` themselves (the writer already accepts any extension).
- WP-13 must find every use of `OutputExtension!` / `OutputExtension.` in `src/` and make each one null-safe.

### 5. New interaction: resize
- New contract in `AppServices.cs`: `IResizePrompt.PromptAsync(IReadOnlyList<string> imagePaths, CancellationToken)` → `ResizeChoice?` (null = cancelled).
- `ResizeChoice(int? LongEdge, int? Percent, int? Width, int? Height, bool AllowEnlarge)`: exactly one of `LongEdge`, `Percent` or (`Width` and/or `Height`) is set.
- Written into options by the app as new `OptionKeys`: `resizeLongEdge`, `resizePercent`, `resizeWidth`, `resizeHeight`, `resizeAllowEnlarge` ("true"/"false").
- CONTRACTS C6 gains `image.resize`: the app prompts **once per drop** and applies the choice to every file.
- The coordinator's constructor gains an `IResizePrompt` parameter; this is the one change to a constructor called in `CompositionRoot.cs`.

### 6. New targets (`TargetCatalog`, appended; existing IDs and priorities unchanged)
| ID | Label | Input | Output ext | Priority | Flags |
|---|---|---|---|---|---|
| `image.to-pdf` | PDF | Image | `.pdf` | 60 | `CombinesInputs`, `MinInputs: 1` |
| `image.smaller` | Smaller | Image | `null` (keeps format, see WP-16) | 70 | `IsFormatChange: false` |
| `image.resize` | Resize | Image | `null` (keeps format) | 80 | `RequiresInteraction`, `IsFormatChange: false` |
| `video.smaller` | Smaller | Video | `.mp4` | 50 | `IsFormatChange: false` |
| `pdf.merge` | Merge | Pdf | `.pdf` | 90 | `CombinesInputs`, `MinInputs: 2`, `IsFormatChange: false` |
| `pdf.to-docx` | Word | Pdf | `.docx` | 100 | — |
| `pdf.smaller` | Smaller | Pdf | `.pdf` | 110 | `IsFormatChange: false` |

### 7. New output labels (`OutputLabels`, CONTRACTS C2 table)
| Target | Label | Example |
|---|---|---|
| `image.to-pdf`, one image | `request.OutputLabel` (default "converted") | `photo (converted).pdf` |
| `image.to-pdf`, several images | `combined` | `scan 01 (combined).pdf` |
| `pdf.merge` | `merged` | `report (merged).pdf` |
| `image.smaller`, `video.smaller`, `pdf.smaller` | `smaller` | `photo (smaller).jpg` |
| `image.resize` | `resized` | `photo (resized).png` |
| `pdf.to-docx` | `request.OutputLabel` | `doc (converted).docx` |

### 8. Keeping the PDF wheel readable (decided by the user, 2026-11-18)
After v0.2 a single PDF shows 10 segments; dragging two or more would show 11 with Merge. Single-file page operations make little sense across several PDFs, so they get `MaxInputs: 1` and are **hidden when two or more PDFs are dragged**:

| Target | Change |
|---|---|
| `pdf.reorder` | `MaxInputs: 1` (asks for an order per file: not useful across files) |
| `pdf.split-half` | `MaxInputs: 1` |
| `pdf.extract-odd` | `MaxInputs: 1` |
| `pdf.extract-even` | `MaxInputs: 1` |
| `pdf.reverse` | `MaxInputs: 1` |

IDs, labels and priorities stay the same; only the new optional parameter is set.

| Dragged | PDF segments shown |
|---|---|
| 1 PDF | Split, Images, Rotate, Reorder, Halves, Odd, Even, Reverse, Word, Smaller (Merge hidden) → **10** |
| 2+ PDFs | Split, Images, Rotate, Merge, Word, Smaller → **6** |

This changes v0.1 behaviour in one way: dragging several PDFs no longer offers the five targets above. A 10-segment wheel for one PDF is checked by hand in WP-14 (user test 9); if it's too hard to hit, a later ADR moves the least-used targets elsewhere.

## Not decided here
- OCR for scanned PDFs (later; needs Windows-only APIs, which breaks "converters are cross-platform").
- Bundling FFmpeg: still not bundled (ADR-012 only adds an assisted install).

## Consequences
- `docs/CONTRACTS.md` C1, C2, C4, C6 and C8 are updated in WP-13 to match.
- Converters that don't implement the new targets need no change.
- A combined job can fail on any input; the error message names the file (`"Couldn't read scan 3.jpg."`).
