# WP-20: Smaller PDF (no Ghostscript)

**Builder:** Sonnet · **Reviewer:** Opus (fresh session) · **Depends on:** WP-13 (and WP-14 merged first: same Pdf project) · **Risk:** **medium-high** (rewrites PDF internals)

## Goal
Drop a PDF on **Smaller** → a copy with its photos and scans recompressed, text still selectable.
Built only with libraries already in the Pdf project. Ghostscript stays banned (ADR-005; decided by the user 2026-11-18: no extra install).
It will not shrink every PDF; when it can't, it says so.

## Owns
- `src/WheelConvert.Converters.Pdf/**` (new class `PdfShrinker.cs`; add `pdf.smaller`)
- `tests/WheelConvert.Converters.Pdf.Tests/**`
- `src/WheelConvert.Converters.Pdf/WheelConvert.Converters.Pdf.csproj`: one new line, a direct SkiaSharp reference (req. 1)
- ADR-010: row for the direct SkiaSharp reference

## Requirements
1. **No new libraries.** Edit with PDFsharp `[6.2.4]` (never upgrade); decode/encode JPEG with **SkiaSharp**. SkiaSharp is already in the build through PDFtoImage, but code that calls it directly must not rely on a dependency's dependency: add `<PackageReference Include="SkiaSharp" Version="[x.y.z]" />` with **the exact version already in `packages.lock.json`** (4.150.1 on 2026-11-18; check the lock file), so the lock file doesn't change. If restore wants to change any other version, stop and ask. The SkiaSharp licence question in `docs/STATUS.md` stays open; this WP doesn't add new SkiaSharp code to the download, only new use of it.
2. Never open the source in `Modify` mode (C2 S1). Instead: copy the source bytes into temp A (`CreateTempPath(source, ".pdf")`), open **temp A** in `Modify` mode, save to temp B, commit B, discard A in `finally` (both always discarded on failure).
3. For each image XObject, in order of size (largest first):
   - Only handle `DCTDecode` (JPEG) with `DeviceRGB` or `DeviceGray`, 8 bits, no `SMask`/`Mask` changes needed. **Skip** everything else (JBIG2, CCITT, JPX, CMYK, Indexed, Flate-encoded images in v1) and count them as skipped.
   - **Rule:** if the image's long edge is above **2000 px**, downscale so it is 2000 px (keep aspect ratio, never enlarge); re-encode JPEG quality **70** either way. A full A4 page at 2000 px is about 170 DPI, sharp enough to read scans at 100 % zoom. (Working out the size each image is drawn at needs content-stream parsing PDFsharp 6.2.4 doesn't offer; not in v1.)
   - Replace the stream only if the new bytes are smaller; update `Width`, `Height`, `Length`.
4. Also turn on PDFsharp's content-stream compression for the saved file.
5. **Size check:** output not at least 10 % smaller than the source → discard, `Fail(NoResult, "Couldn't make this PDF smaller.")`. If every image was skipped, say why: `"Couldn't make this PDF smaller: its images use a format this app can't shrink yet."`.
6. Encrypted → `PasswordProtected`; unreadable → `CorruptInput`; label `OutputLabels.Smaller` → `doc (smaller).pdf`. Progress per image; cancellation between images.
7. **Output must still open and render:** after saving temp B, open it with PDFtoImage and render page 1 (cheap check). Failure → discard, `Fail(Unknown, "Couldn't make this PDF smaller.")` and log the exception.

## Gotchas
- The same image can be used on many pages; replace the shared XObject once.
- Images with a soft mask (`SMask`) keep their mask; only the colour image is resized, and the mask must be resized to match. If that's not possible in v1, skip those images.
- Text-only PDFs (exported from Word) will usually hit req. 5: that's the expected message, not a bug.
- Form fields, links and bookmarks must survive (they're untouched objects); test 6 checks it.

## Acceptance tests
| # | Who | Test | Expected | Result |
|---|---|---|---|---|
| 1 | Agent | Generated PDF with three 4000 × 3000 JPEG pages | ≥ 50 % smaller; renders; pages unchanged in count | PASS (4000x3000 to 2000x1500 per page; 3 pages; renders) |
| 2 | Agent | Text-only generated PDF | `NoResult` "Couldn't make this PDF smaller." | PASS |
| 3 | Agent | PDF whose only image is CMYK | `NoResult` with the "format" message | PASS (dictionary declares DeviceCMYK; skipped on that) |
| 4 | Agent | Shared image on 10 pages | Replaced once; all 10 pages render | PASS (stats: Replaced = 1; 10 pages render) |
| 5 | Agent | Source unchanged (hash); no temp A or B left on every failure path | Pass | PASS (hash and temp checks in every failure test) |
| 6 | Agent | PDF with a link and a bookmark | Both still present | PASS |
| 7 | Agent | Encrypted / garbage / cancel | `PasswordProtected` / `CorruptInput` / no temps | PASS (incl. owner-password-only) |
| 8 | User | A phone-scan PDF from `wc-test` → Smaller | Much smaller; still readable at 100 % zoom | NEEDS USER |
| 9 | User | Upload it to a portal with a size limit | Accepted | NEEDS USER |

## Out of scope
Ghostscript, choosing a quality level, shrinking JBIG2/CCITT/JPX/CMYK/Flate images, font subsetting, removing duplicate objects.

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md (C1, C2), docs/decisions/ADR-005-licensing.md, docs/decisions/ADR-011-multi-file-targets.md, docs/modules/WP-08-pdf-operations.md, docs/modules/WP-20-pdf-smaller.md.
You are the builder for WP-20. Create branch wp-20-pdf-smaller from main. Restate the plan in 3–5 lines and wait for "go".
```
