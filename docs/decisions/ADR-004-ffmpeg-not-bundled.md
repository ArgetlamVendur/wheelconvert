# ADR-004: FFmpeg is detected, not bundled
Status: Accepted · Date: 2026-10-20

## Context
Bundling FFmpeg binaries brings LGPL/GPL obligations (source offer, matching build configuration, patent-encumbered codecs) and ~100 MB of download.

## Decision
- The media converter finds `ffmpeg.exe` via `ToolLocator` (setting `FfmpegPath`, used alone if set → process PATH → user PATH → machine PATH (both re-read from the registry on Windows, so a later install is found without a restart) → `%LOCALAPPDATA%\Microsoft\WinGet\Links` → `%ProgramFiles%\WinGet\Links`).
- If missing, the media converter reports `ConverterHealth.Missing("Install FFmpeg: winget install Gyan.FFmpeg")`; its wheel segments disappear and the tray menu shows how to fix it, with a "Check again" item that calls `FormatRegistry.Refresh()`.
- FFmpeg runs only through `ProcessRunner`.

## Consequences
- Audio/video conversion needs a one-time user step.
- No FFmpeg licence obligations for this project.
