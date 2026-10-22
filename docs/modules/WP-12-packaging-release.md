# WP-12: Packaging and release

**Builder:** Antigravity · **Reviewer:** Claude Code · **Depends on:** all · **Risk:** medium (antivirus, signing)

## Goal
A downloadable, installable release on GitHub, with licences in order and the antivirus go/no-go answered.

## Owns
- `.github/workflows/build.yml`, `.github/workflows/release.yml` (new)
- `installer/` (new): Inno Setup script
- `LICENSE`, `THIRD-PARTY-NOTICES.md` (new), `README.md`, `CHANGELOG.md` (new)
- `docs/GO-NO-GO.md` (new)

## Requirements
1. `LICENSE`: MIT, copyright holder as the user specifies.
2. `THIRD-PARTY-NOTICES.md`: every package from ADR-010 with licence text links, **plus an inventory of the native libraries those packages bundle** (read each package's own notice/licence files): Magick.Native delegates (e.g. libheif/libde265 — believed LGPL; libraw; others), PDFium's FreeType (FTL credit), ICU, libjpeg-turbo, SkiaSharp. Record LGPL obligations (notice, licence text, relinking terms if statically linked) and ask the user before release if any are unclear. Note that FFmpeg is not distributed.
2a. Publish with `-p:IncludeNativeLibrariesForSelfExtract=true` (already in `build.yml`) so the native DLLs are inside the single file.
3. `release.yml`: on tag `v*`: build, run tests, publish self-contained=false single-file win-x64, build the Inno Setup installer (`choco install innosetup` on the runner), attach `WheelConvert-Setup-<ver>.exe`, the portable zip and `SHA256SUMS.txt` to a GitHub Release (draft).
4. Installer: per-user install to `%LOCALAPPDATA%\Programs\WheelConvert` (no admin), Start-menu shortcut, optional "Start with Windows", checks for the .NET 8 Desktop Runtime and links to it if missing, uninstaller removes the Run key and (asks before) the settings folder.
5. **Go/no-go (ADR-006):** scan the published exe with Microsoft Defender and VirusTotal. Record detections in `docs/GO-NO-GO.md`. If the hook is flagged, propose flipping the default trigger to Hotkey (new ADR).
6. Signing: document options (SignPath Foundation for OSS; Azure Trusted Signing — check eligibility for an Indian individual) in `docs/GO-NO-GO.md`. Ship unsigned with checksums until one is approved.
7. README: what it does (GIF placeholder), install, FFmpeg note, privacy (everything local), how to report bugs, how agents contribute (link AGENTS.md).
8. `winget` manifest: draft only, in `installer/winget/`, not submitted.

## Acceptance tests
| # | Who | Steps | Expected |
|---|---|---|---|
| 1 | Agent | Push tag `v0.1.0-rc1` in a fork/branch | Draft release with 3 assets |
| 2 | User | Install on a clean Windows user account | Starts, tray icon, no admin prompt |
| 3 | User | Uninstall | Run key gone, program folder gone |
| 4 | User | Defender + VirusTotal | Results recorded in GO-NO-GO.md |

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/modules/WP-12-packaging-release.md, ADR-005, ADR-006, ADR-010.
You are the builder for WP-12. Create branch wp-12-release from main. Restate the plan in 3–5 lines and wait for "go".
```
