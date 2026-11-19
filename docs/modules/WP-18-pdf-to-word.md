# WP-18: PDF → Word (text only)

**Builder:** Sonnet · **Reviewer:** Opus (fresh session) · **Depends on:** WP-13 · **Risk:** medium

## Goal
Drop a PDF on **Word** → an editable `.docx` with the PDF's text as paragraphs and headings.
It does **not** reproduce the layout: no columns, tables, images or exact fonts. That limit is stated in the README, not hidden.

## Owns
- New project `src/WheelConvert.Converters.Docx/` (converter `Id = "docx"`, implements `pdf.to-docx` only) + register it in `CompositionRoot.cs` (registration line only; frozen constructors unchanged)
- New `tests/WheelConvert.Converters.Docx.Tests/` + add both projects to `WheelConvert.sln` (append-only)
- ADR-010: rows for the new packages
- `THIRD-PARTY-NOTICES.md`: entries for every new package **and its dependencies** (read each package's own licence/notice files, not agent reports; same rule as WP-12 req. 2)
- `.github/workflows/release.yml`: add the new packages' licence/notice files to the `$notices` list in "Stage files", so they ship in the installer and the zip (a missing file must still fail the build)

## Requirements
1. Packages: choose the latest stable version on the day you start, then **pin it exactly** (`Version="[x.y.z]"`), like every other package (ADR-010). Licence check per ADR-005; record version, licence and dependencies in ADR-010:
   - **UglyToad.PdfPig** (Apache-2.0, expected): text extraction with positions.
   - **DocumentFormat.OpenXml** (MIT, expected): writing `.docx`.
   If either licence is not as expected, stop and set the WP to `BLOCKED`.
2. Project targets `net8.0` (cross-platform, like the other converters). Lock file committed.
3. Open the source read-only (`FileAccess.Read`, `FileShare.ReadWrite | FileShare.Delete`). Encrypted → `Fail(PasswordProtected, "This PDF is password-protected.")`; unreadable → `CorruptInput`.
4. Per page: words → text blocks → reading order, using PdfPig's document layout analysis (word extractor, block segmenter, reading-order detector; check the names against PdfPig's own docs for the pinned version). Join each block's lines into one paragraph; fix hyphenated line ends (`exam-` + `ple` → `example`) only when the next line starts lower-case.
5. Headings: a block whose median font size is ≥ 1.3 × the page's median body size → `Heading 1` (≥ 1.6 ×) or `Heading 2`. Otherwise `Normal`. Use Word's built-in style IDs so headings show in Word's navigation pane.
6. A page break between PDF pages.
7. **Scan detection:** fewer than 20 letters per page on average → `Fail(NoResult, "This PDF looks like a scan: there's no text to copy.")` (OCR is later).
8. Output `CreateTempPath(source, ".docx")`, label `request.OutputLabel` → `doc (converted).docx`. Progress per page; cancellation between pages.
9. The `.docx` must open in Word, LibreOffice and Google Docs without a repair prompt: validate with `OpenXmlValidator` in tests (0 errors).
10. README: add one line under Features: "PDF → Word: text and headings only, not the layout. Scanned PDFs aren't supported yet."

## Gotchas
- Right-to-left (Arabic, Urdu) and complex Indic scripts (Odia, Hindi) can extract in PDF glyph order and may come out garbled: test one Hindi **and one Odia** PDF (user test 9); if broken, keep the target but add the limit to the README line. Don't try to fix shaping in v1.
- Some PDFs have no `ToUnicode` maps: words come back as junk characters. Treat "more than 30 % of characters are U+FFFD or private-use" like a scan (req. 7) with message "Couldn't read the text in this PDF."
- `CheckHealth()`: `Ready` if both libraries load (types resolve); no work on user files.

## Acceptance tests
| # | Who | Test | Expected | Result |
|---|---|---|---|---|
| 1 | Agent | Generated 2-page PDF with a title + 3 paragraphs | `.docx` with Heading 1 + 3 paragraphs + page break; validator 0 errors | PASS |
| 2 | Agent | PDF with hyphenated line ends | Words joined | PASS |
| 3 | Agent | Image-only PDF | `NoResult` "looks like a scan", no temp left | PASS |
| 4 | Agent | Encrypted / garbage | `PasswordProtected` / `CorruptInput`, no temp left | PASS |
| 5 | Agent | Cancel on a 200-page PDF | No temp left | PASS |
| 6 | Agent | Source unchanged (hash) | Pass | PASS |
| 7 | User | A real text PDF (e.g. a report) → Word, open in Word | Text and headings readable and editable | NEEDS USER |
| 8 | User | A phone-scanned PDF → Word | Clear "looks like a scan" message | NEEDS USER |
| 9 | User | One Hindi and one Odia text PDF → Word | Readable, or the README states the limit | NEEDS USER |
| 10 | User | Install the release build; open the install folder's `licenses` | The new packages' licence files are there | NEEDS USER |

## Out of scope
Layout, tables, images, fonts, OCR, `.doc` (legacy) output.

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md (C1, C2), docs/decisions/ADR-005-licensing.md, docs/decisions/ADR-010-package-versions.md, docs/modules/WP-18-pdf-to-word.md.
You are the builder for WP-18. Create branch wp-18-pdf-to-word from main. Restate the plan in 3–5 lines and wait for "go".
```
