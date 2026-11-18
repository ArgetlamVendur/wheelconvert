# WP-19: Image resize

**Builder:** Sonnet · **Reviewer:** Opus (fresh session) · **Depends on:** WP-13 (and WP-16 merged first: same Image project) · **Risk:** low

## Goal
Drop one or more images on **Resize** → a small dialog → resized copies in the same format.

## Owns
- `src/WheelConvert.Converters.Image/**` (new class `ImageResizer.cs`; add `image.resize`)
- New folder `src/WheelConvert.App/Resize/` (`ResizeWindow.xaml(.cs)`, `ResizePrompt.cs` implementing `IResizePrompt`)
- `CompositionRoot.cs`: replace WP-13's stub `NotImplementedResizePrompt` with `ResizePrompt` (one line)
- Tests in the Image and App test projects

## Requirements: dialog
1. Same look as the WP-09 window. Title "Resize N images" (or the file name for one).
2. Choices (radio buttons, one selected by default: **1920 px**):
   - Long edge: **1920 px**, **1280 px**, **800 px**
   - **50 %**
   - **Custom:** width and height boxes, **Keep proportions** checkbox (on by default; then only one box is needed and the other shows the computed value for the first image)
3. **Allow enlarging** checkbox, off by default.
4. Validation: whole numbers 1–20000; with Keep proportions off, both boxes required. OK disabled until valid. Escape / Cancel / closing → `null` (nothing is converted, no notification).
5. Returns a `ResizeChoice` (ADR-011 §5). The app writes it into the options keys; the prompt is shown **once per drop**.

## Requirements: converter
6. Read options; missing or invalid → `Fail(Unsupported, "Invalid resize settings.")`.
7. `AutoOrient()` first, then compute the target size from the **upright** image. Long edge / percent / custom with proportions: keep aspect ratio. Custom without proportions: exact size (stretches; the user asked for it).
8. Enlarging off and the target is larger than the image → keep the original size for that dimension; if nothing would change → `Fail(NoResult, "<file> is already smaller than that.")`.
9. Same format as the source (target `OutputExtension` is null, ADR-011 §4); same quality settings as the image converter's normal conversion for that format. HEIC/AVIF sources → `.jpg` (same rule as WP-16). Animated GIF → `Unsupported`.
10. Label `OutputLabels.Resized` → `photo (resized).png`. Metadata: keep as the normal image conversion does, but update the stored dimensions.

## Acceptance tests
| # | Who | Test | Expected | Result |
|---|---|---|---|---|
| 1 | Agent | 4000 × 3000, long edge 1280 | 1280 × 960 | |
| 2 | Agent | 3000 × 4000 with EXIF rotation, long edge 800 | 600 × 800, upright | |
| 3 | Agent | 50 % of 1001 × 1001 | 501 × 501 (rounded, documented) | |
| 4 | Agent | Custom 600 × 600 without proportions on 1200 × 800 | 600 × 600 | |
| 5 | Agent | 640 × 480, long edge 1920, enlarging off | `NoResult` | |
| 6 | Agent | Same with enlarging on | 1920 × 1440 | |
| 7 | Agent (App, STA) | Dialog: invalid input disables OK; Escape returns null | Pass | |
| 8 | User | Select 3 photos in `wc-test` → Resize → 800 px | 3 resized copies, originals untouched | NEEDS USER |
| 9 | User | Passport-style: one photo → Custom 600 × 600, proportions off | Exactly 600 × 600 | NEEDS USER |

## Out of scope
Cropping, video resize (use Smaller), remembering the last choice, DPI settings.

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md (C1, C2, C6), docs/decisions/ADR-011-multi-file-targets.md, docs/modules/WP-19-image-resize.md, docs/modules/WP-09-pdf-reorder-window.md.
You are the builder for WP-19. Create branch wp-19-image-resize from main. Restate the plan in 3–5 lines and wait for "go".
```
