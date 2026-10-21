# WP-06: Image converter

**Builder:** Antigravity · **Reviewer:** Claude Code · **Depends on:** WP-01 · **Risk:** medium

## Goal
Convert between image formats: targets `image.jpg`, `image.png`, `image.webp`, `image.ico`.

## Owns
- `src/WheelConvert.Converters.Image/**`
- `tests/WheelConvert.Converters.Image.Tests/**` (new, xUnit) + add it to `WheelConvert.sln`
- One row in `docs/decisions/ADR-010-package-versions.md`

## Requirements
1. Add `Magick.NET-Q8-AnyCPU` (latest stable). Record the version in ADR-010, and in the PR description list the native delegate libraries the package ships with their licences (from its notice files) for WP-12.
2. `CheckHealth()`: `Ready` if `MagickNET.Version` can be read (forces native load); on `TypeInitializationException`/`DllNotFoundException` return `Missing("Image library failed to load.")`.
3. `CanConvert`: input extensions as in the stub; **refuse** `.ico`→`image.ico` (same format is filtered anyway), and refuse `.gif` → `image.ico` (animated GIF to icon is meaningless).
4. Conversion (`ConvertAsync`), following CONTRACTS C1–C2 exactly:
   - `temp = output.CreateTempPath(source, target.OutputExtension)`; `try { … output.Commit(temp, source, ext, label) } finally { if not committed: output.Discard(temp) }`.
   - Label = `request.OutputLabel` (WP-11 fills `OptionKeys.OutputLabel` from settings; default "converted").
   - Follow the reference pattern in CONTRACTS C2 for try/finally and exception mapping.
   - **Never let ImageMagick sniff the format** (ImageTragick class of bugs: a `.png` that's really MVG/SVG/MSL/`text:` can read or write other files). Read with `new MagickImage(sourcePath, new MagickReadSettings { Format = <MagickFormat from the extension> })`, map extensions explicitly (`.jpg/.jpeg→Jpeg`, `.heic/.heif→Heic`, …), and at start-up disable risky coders via `MagickNET.SetSecurityPolicy` (deny `MVG, MSL, SVG, TEXT, URL, EPHEMERAL, PS, EPS, PDF, XPS`). Read-only. For multi-frame inputs (GIF, multi-page TIFF): use the **first frame** for JPG/ICO; keep all frames for WebP/PNG only if the input is animated GIF → WebP (animated WebP). Document the choice in code.
   - `AutoOrient()` (respect EXIF rotation) before writing; strip EXIF GPS? **No** — keep metadata unchanged except orientation (users expect a faithful copy). Note as follow-up.
   - JPG: quality 90 (or `OptionKeys.Quality`), flatten transparency onto white.
   - PNG: lossless.
   - WebP: quality 85, lossless if the source is PNG with transparency? Keep simple: lossy 85, keep alpha.
   - ICO: resize to fit 256×256 (keep aspect, pad transparent to square), write sizes 256, 128, 64, 48, 32, 16 in one file (`icon:auto-resize`).
   - Run the CPU-bound work with `Task.Run` and check the cancellation token between steps.
5. Errors: `MagickCorruptImageErrorException`/`MagickMissingDelegateErrorException` → `CorruptInput` / `Unsupported`; `UnauthorizedAccessException`/`IOException` on write → `OutputFailed`; `MagickResourceLimitErrorException` → `Fail(Unsupported, "Image is too large.")`.
6. Safety limits: set `ResourceLimits.Memory` to ~1 GB and `ResourceLimits.Width/Height` to 30 000 px at start-up (static ctor) to avoid decompression bombs.

## Gotchas
- HEIC needs libheif, which Magick.NET includes on Windows x64. Test a real iPhone HEIC.
- `MagickImage` is `IDisposable`: `using` everywhere.
- Don't use `System.Drawing` (Windows-only, breaks the net8.0 rule).

## Acceptance tests (xUnit unless marked User)
| # | Test | Expected |
|---|---|---|
| 1 | PNG with alpha → JPG | Output exists, white background, source byte-identical before/after |
| 2 | JPG → PNG/WebP/ICO | Valid files (re-open with Magick), ICO contains 6 sizes |
| 3 | EXIF-rotated JPG → PNG | Output upright |
| 4 | Truncated/corrupt JPG | `Fail(CorruptInput)`, **no `.~wc-` file left** |
| 5 | Output name already exists | `photo (converted 2).jpg`, original output untouched |
| 6 | Cancellation token cancelled before start | `OperationCanceledException`, no temp file left |
| 7 | User: real iPhone HEIC → JPG | Correct image |
| 7a | MVG payload (`push graphic-context … image over 0,0 0,0 'file.txt'`) saved as `evil.png` → JPG | `Fail(CorruptInput)`; **no new files in the folder**; `file.txt` not read |
| 8 | User: through the wheel end-to-end (after WP-11) | Output next to source, notification shown |

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md, docs/modules/WP-06-image-converter.md, docs/decisions/ADR-005-licensing.md.
You are the builder for WP-06. Create branch wp-06-image from main. Restate the plan in 3–5 lines and wait for "go".
```
