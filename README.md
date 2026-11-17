# WheelConvert

Hold **Shift** and drag a file in Windows Explorer (or press **Ctrl+Alt+Space**) → a wheel appears at the cursor → drop the file on a format. The converted copy appears next to the original. Everything runs on your PC.

> **Status: release candidate.** Not yet code-signed, so Windows SmartScreen or your antivirus may warn about it. See [Install](#install).

<!-- TODO(WP-12): replace with a short screen recording (GIF) of Shift-drag -> wheel -> drop. -->
*(GIF: coming soon)*

## Features
- **Wheel at your cursor:** hold Shift while dragging a file in File Explorer, or press the hotkey (default Ctrl+Alt+Space). Drop the file on a segment.
- **Images:** JPG, PNG, WebP, ICO (from HEIC, PNG, JPG, WebP, GIF, TIFF, BMP, AVIF).
- **Audio and video** (needs FFmpeg, see below): MP3, M4A, FLAC, WAV, OGG, Opus, MP4, WebM, GIF, extract audio.
- **PDF tools:** split pages, halves, odd/even, rotate, reverse, reorder pages, pages to images.
- **Never overwrites anything:** outputs are named `photo (converted).jpg`, `photo (converted 2).jpg`... next to the original. Your original file is never changed.

## Install
1. Download `WheelConvert-Setup-<version>.exe` from the [Releases](../../releases) page. It installs for your user only and asks for no administrator rights.
2. The app needs the **.NET 8 Desktop Runtime** (x64). The installer tells you if it is missing and links to the download.
3. Optional: tick "Start with Windows" in the installer, or later from the tray menu.
4. Prefer no installer? Download the portable zip, unzip it anywhere (keep all the files together; the `.dll` files next to `WheelConvert.exe` are part of the app) and run `WheelConvert.exe`.
5. Check the download against `SHA256SUMS.txt` from the same release page (in PowerShell: `Get-FileHash .\WheelConvert-Setup-<version>.exe`).

The releases are **not code-signed yet**. If SmartScreen says "Windows protected your PC", choose "More info" then "Run anyway" only if the checksum matches.

### FFmpeg (for audio and video)
FFmpeg is not included. Install it once and WheelConvert finds it on your PATH:
```powershell
winget install Gyan.FFmpeg
```

## Privacy
Everything runs on your PC. WheelConvert does not send your files, file names or usage data anywhere and has no account or telemetry. Settings and the log are kept in `%LOCALAPPDATA%\WheelConvert\`.

## Report a bug
Open an issue on the GitHub repository. Include what you did, what you expected, what happened, your Windows version and, if you can, the log file from `%LOCALAPPDATA%\WheelConvert\`. Please do not attach private files.

## Contributing (humans and AI agents)
Work is split into independent work packages in [`docs/modules/`](docs/modules/). Every coding agent (Claude Code, Antigravity, others) follows the rules in [`AGENTS.md`](AGENTS.md); read it first, then [`docs/STATUS.md`](docs/STATUS.md).

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

Building from source needs the .NET 8 SDK 8.0.400 or later (the repo has a `global.json`).

## Troubleshooting
- No notifications appear: check that Windows "Do not disturb" (Focus) is off.

## Licence
MIT (see [`LICENSE`](LICENSE)). Third-party licences: [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).
