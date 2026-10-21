# WP-08: PDF operations and page renderer

**Builder:** Claude Code · **Reviewer:** Antigravity · **Depends on:** WP-01 · **Risk:** medium

## Goal
All `pdf.*` targets, plus `IPdfPageRenderer` for thumbnails (WP-09) and `pdf.to-images`.

## Owns
- `src/WheelConvert.Converters.Pdf/**`
- `tests/WheelConvert.Converters.Pdf.Tests/**` (new) + add to `WheelConvert.sln`
- ADR-010 rows for PDFsharp and PDFtoImage

## Requirements
1. Packages: `PDFsharp` (edit) and `PDFtoImage` (render; brings PDFium + SkiaSharp natives). Latest stable; record in ADR-010; confirm licences (ADR-005).
2. Open sources with `PdfReader.Open(path, PdfDocumentOpenMode.Import)` (read-only). Never `Modify` mode on the source.
3. Operations (each writes **new** documents by importing pages):

   | Target | Output | Label |
   |---|---|---|
   | pdf.split-pages | one PDF per page | `OutputLabels.Page(i, n)` |
   | pdf.split-half | pages 1..⌈n/2⌉, rest | `OutputLabels.Part(1)`, `Part(2)`; 1-page PDF → `Fail(Unsupported, "Only one page.")` |
   | pdf.extract-odd / even | one PDF | `OddPages` / `EvenPages`; even on a 1-page PDF → `Fail(Unsupported, "No even pages.")` |
   | pdf.rotate-90 | one PDF, every page +90° clockwise | `Rotated` |
   | pdf.reverse | one PDF | `Reversed` |
   | pdf.reorder | one PDF in `Options[PageOrder]` order | `Reordered` |
   | pdf.to-images | one PNG per page at 150 DPI | `OutputLabels.Page(i, n)` |

4. `pdf.reorder` validation (CONTRACTS C6): parse `"3,1,2"`; each 1..n; no duplicates; at least one page; missing/invalid → `Fail(Unsupported, "Invalid page order.")`.
5. Multi-output (CONTRACTS C2 S5): commit pieces one by one; on failure discard remaining temps, keep committed pieces, return `Fail(…, "Stopped after k of n pages: <reason>")`. Check the cancellation token between pages.
6. Errors: password-protected → `PasswordProtected` ("This PDF is password-protected."); unreadable → `CorruptInput`. PDFsharp throws `PdfReaderException` for both: inspect the message or try `PdfReader.TestPdfFile`.
7. `CheckHealth()`: `Ready` if PDFsharp types load and PDFium initialises (call a cheap PDFtoImage API on an embedded 1-page sample PDF resource, once, cached). Otherwise `Missing("PDF library failed to load.")`.
8. `PdfPageRenderer` (contract in `AppServices.cs`): `GetPageCountAsync` and `RenderPagePngAsync(path, index, maxPixels)` with PDFtoImage. Encrypted → `PdfPasswordProtectedException`; unreadable → `InvalidDataException`. Must be thread-safe (PDFium is not: serialise calls with a `SemaphoreSlim(1)`).
9. Large PDFs: `pdf.to-images` on > 200 pages → still allowed, but progress is reported per page and cancellation works.

## Gotchas
- PDFsharp 6 needs a font resolver only for **creating text**; importing pages doesn't. Don't add one.
- Rotation: `page.Rotate = (page.Rotate + 90) % 360`.
- Some PDFs have form fields/annotations: importing keeps them; acceptable.
- PDFtoImage's API takes a `Stream` or bytes; open the file with `FileAccess.Read` and `FileShare.ReadWrite | FileShare.Delete` (another app may have it open).
- Req. 7's one-time PDFium initialisation may take > 50 ms; that's allowed (WP-11 runs health checks off the UI thread).

## Acceptance tests (xUnit; generate test PDFs in the test with PDFsharp)
| # | Test | Expected |
|---|---|---|
| 1 | 3-page PDF → split-pages | `doc (page 1..3).pdf`, each 1 page; source unchanged (hash) |
| 2 | 40-page → split-pages | Names `page 01`…`page 40` |
| 3 | 5-page → halves | 3 + 2 pages |
| 4 | odd / even / reverse / rotate | Correct page counts / order / rotation |
| 5 | reorder `"3,1,2"` / `"1,1"` / `"0"` / missing | Correct / Fail / Fail / Fail |
| 6 | Encrypted PDF | `PasswordProtected`, no temp left |
| 7 | Garbage bytes named .pdf | `CorruptInput` |
| 8 | to-images on 2-page PDF | 2 PNGs, readable |
| 9 | Renderer page count + thumbnail ≤ maxPixels | ✔ |
| 10 | Cancel during split of 50 pages | Committed pieces stay, no temps |

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md (C1, C2, C6), docs/modules/WP-08-pdf-operations.md, docs/decisions/ADR-005-licensing.md.
You are the builder for WP-08. Create branch wp-08-pdf from main. Restate the plan in 3–5 lines and wait for "go".
```
