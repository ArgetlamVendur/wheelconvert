# WP-15: Images → PDF

**Builder:** Sonnet · **Reviewer:** Opus (fresh session) · **Depends on:** WP-13 · **Risk:** medium · **Same project as:** WP-16, WP-19 (don't run in parallel)

## Goal
Drop one or more images on **PDF** → one PDF, one image per A4 page, upright, in natural name order.
Main use: phone photos or scans of a form, ID or receipts that a portal wants as a single PDF.

## Owns
- `src/WheelConvert.Converters.Image/**` (new class `ImagesToPdf.cs`; add `image.to-pdf` to `SupportedTargetIds`)
- `tests/WheelConvert.Converters.Image.Tests/**`
- ADR-010: one new row (PDFsharp in Converters.Image)

## Requirements
1. Decode with Magick.NET (already in this project: handles HEIC, WebP, AVIF, TIFF…). Add **PDFsharp `[6.2.4]`, exact pin** to this project for writing the PDF; record it in ADR-010 (licence already verified for WP-08; same version). **Never upgrade** (6.5.0+ is AGPL).
2. Per image: `AutoOrient()` (fixes sideways phone photos) → take the first frame only (animated GIF, multi-page TIFF: first frame; note in README) → flatten transparency onto white.
3. Page: **A4** (595 × 842 pt), **landscape** when the image is wider than tall, margin 18 pt (≈ 6 mm). Fit the image inside the margins keeping its aspect ratio, centred. Small images are scaled up to fit too (forms expect a full page).
4. Embedding: downscale so the image is at most 300 DPI at its placed size (A4 at 300 DPI ≈ 2480 × 3508 px) to keep files small. Photos (source JPG, HEIC, WebP, AVIF) → JPEG quality 85. Graphics (source PNG, GIF, BMP, TIFF) → PNG (lossless) via PDFsharp. Strip all metadata from the embedded image (privacy: no GPS in the PDF).
5. Output: `CreateTempPath(request.SourcePath, ".pdf")`. One input → label `request.OutputLabel` (`photo (converted).pdf`). Several → `OutputLabels.Combined` (`scan 01 (combined).pdf`).
6. Errors: an input can't be decoded → `Fail(CorruptInput, "Couldn't read <file name>.")`, nothing committed. Progress per image; cancellation checked between images.
7. Memory: process one image at a time and dispose it before the next (40 phone photos must not load at once).

## Gotchas
- PDFsharp needs **no font resolver**: no text is drawn. Don't add one.
- PDFsharp `XImage.FromStream` needs a seekable stream; write Magick's output to a `MemoryStream` and rewind.
- CMYK JPEGs: convert to sRGB in Magick before encoding.

## Acceptance tests
| # | Who | Test | Expected | Result |
|---|---|---|---|---|
| 1 | Agent | 1 portrait JPEG | 1-page PDF, portrait A4, label "converted" | PASS (`OnePortraitJpeg_OnePortraitA4Page_LabelConverted`) |
| 2 | Agent | 3 images: 1200 × 1600 JPEG, 1600 × 1200 JPEG, 800 × 1200 PNG with alpha | 3 pages: portrait, landscape, portrait; alpha on white | PASS (`ThreeImages_PortraitLandscapePortrait_AlphaOnWhite_LabelCombined`) |
| 3 | Agent | JPEG with EXIF orientation 6 | Page shows it upright | PASS (`ExifOrientation6_PageShowsImageUpright`) |
| 4 | Agent | 6000 × 4000 photo | Embedded image ≤ 300 DPI at placed size | PASS (`LargePhoto_IsDownscaledToAtMost300Dpi`) |
| 5 | Agent | Embedded image has no EXIF/GPS | Pass | PASS (`EmbeddedImage_HasNoExifOrGpsMetadata`) |
| 6 | Agent | One corrupt input among 3 | `CorruptInput` naming it, no temp left | PASS (`OneCorruptInputAmongThree_FailsNamingIt_NothingLeftBehind`) |
| 7 | Agent | Cancel during 30 images | No temp left | PASS (`CancelDuring30Images_NoTempLeft`) |
| 8 | User | Select 3 phone photos (one HEIC) in `wc-test`, drop on PDF | One PDF, A4 pages, upright, in name order | NEEDS USER |
| 9 | User | Upload that PDF to any portal you use | Accepted | NEEDS USER |

## Out of scope
Page size choice, margins setting, OCR, one PDF per image.

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md (C1, C2), docs/decisions/ADR-011-multi-file-targets.md, docs/decisions/ADR-005-licensing.md, docs/modules/WP-15-images-to-pdf.md.
You are the builder for WP-15. Create branch wp-15-images-to-pdf from main. Restate the plan in 3–5 lines and wait for "go".
```
