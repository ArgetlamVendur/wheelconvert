# WP-10: QR reader

**Builder:** Antigravity · **Reviewer:** Claude Code · **Depends on:** WP-01 · **Risk:** low

## Goal
`image.read-qr`: decode QR codes in an image file and return the text (ADR-009). WP-11 copies it to the clipboard and shows it in a notification.

## Owns
- `src/WheelConvert.Converters.Qr/**`
- `tests/WheelConvert.Converters.Qr.Tests/**` (new) + add to `WheelConvert.sln`
- ADR-010 rows

## Requirements
1. Packages: `ZXing.Net` and `Magick.NET-Q8-AnyCPU` (**same version as WP-06**; if WP-06 isn't merged yet, use latest stable and note it).
2. Load the image with Magick **with an explicit format from the extension and the same security policy as WP-06 req. 4** (never sniff the content), read-only, `AutoOrient()`, convert to 8-bit grayscale, get raw bytes, decode with ZXing `RGBLuminanceSource`/`BarcodeReaderGeneric` (`PossibleFormats = [QR_CODE]`, `TryHarder = true`, `TryInverted = true`, multiple barcodes enabled).
3. If nothing found, retry once on a 2× upscaled copy (small codes in screenshots), then return `Fail(NoResult, "No QR code found.")`.
4. Several codes → join texts with `\n` in reading order (top-to-bottom, left-to-right by result points).
5. Return `ConversionResult.OkText(text)`. **Never** create files, open URLs or run anything (ADR-009). `output` is unused.
6. Limit decoded text to 4 000 characters (truncate with "…").
7. Same resource limits as WP-06 (decompression bombs).

## Acceptance tests (xUnit; generate QR images in the test with ZXing's writer)
| # | Test | Expected |
|---|---|---|
| 1 | PNG with one QR "hello" | `OkText("hello")` |
| 2 | Small QR in a large screenshot-like image | Found (upscale retry) |
| 3 | Two QRs | Both, newline-separated, ordered |
| 4 | No QR | `Fail(NoResult)` |
| 5 | Inverted (white-on-black) QR | Found |
| 6 | Source unchanged; no files created in the folder | ✔ |
| 6a | MVG payload saved as `evil.png` | `Fail(CorruptInput)`, no files created, nothing read |
| 7 | User: screenshot of a QR → wheel → QR | Notification shows text; clipboard has it |

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md, docs/modules/WP-10-qr-reader.md, docs/decisions/ADR-009-qr-scope.md.
You are the builder for WP-10. Create branch wp-10-qr from main. Restate the plan in 3–5 lines and wait for "go".
```
