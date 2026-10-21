# WheelConvert

Hold **Shift** and drag a file in Windows Explorer (or press **Ctrl+Alt+Space**) → a wheel appears at the cursor → drop the file on a format. The converted copy appears next to the original. Everything runs on your PC.

> **Status: base / under construction.** The core is built and tested; the app shell and converters are stubs waiting to be implemented. See [`docs/STATUS.md`](docs/STATUS.md).

## Planned features
- Images: JPG, PNG, WebP, ICO (from HEIC, PNG, JPG, WebP, GIF, TIFF, BMP, AVIF)
- Audio/video (needs FFmpeg: `winget install Gyan.FFmpeg`): MP3, M4A, FLAC, WAV, OGG, Opus, MP4, WebM, GIF, extract audio
- PDF: split pages, halves, odd/even, rotate, reverse, reorder, pages → images
- QR codes: read from an image → clipboard
- Never overwrites anything: outputs are named `photo (converted).jpg`, `photo (converted 2).jpg`…

## Repository map
| Path | What |
|---|---|
| `AGENTS.md` | Rules for AI coding agents (Claude Code, Antigravity) |
| `docs/STATUS.md` | Which modules are done / in progress |
| `docs/ARCHITECTURE.md` | How the pieces fit |
| `docs/CONTRACTS.md` | Frozen interfaces and safety rules |
| `docs/modules/` | One work package per module, with tests and start prompts |
| `docs/decisions/` | Locked decisions (ADRs) |
| `docs/reviews/` | Independent review results |
| `src/WheelConvert.Core` | Contracts + pure logic (cross-platform, tested) |
| `src/WheelConvert.Converters.*` | One project per converter |
| `src/WheelConvert.App` | Windows tray app (WPF) |
| `tests/` | Tests |

## Build
```powershell
dotnet build WheelConvert.sln                         # Windows (the App needs Windows)
dotnet run --project tests/WheelConvert.Core.Tests    # any OS; exit code 0 = all tests pass
```

## Licence
MIT (see `LICENSE`, added in WP-12). Third-party licences: `docs/decisions/ADR-005-licensing.md`.
