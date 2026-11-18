# ADR-012: Assisted FFmpeg install (amends ADR-004)
Status: Proposed (needs user approval) · Date: 2026-11-18

## Check before accepting (user, by hand)
Uninstall FFmpeg (`winget uninstall Gyan.FFmpeg`), then run the exact command below in PowerShell **as a normal user**, not as administrator:
```
winget install --id Gyan.FFmpeg --exact --source winget --scope user --silent --accept-package-agreements --accept-source-agreements --disable-interactivity
```
- Works and `ffmpeg -version` runs in a new PowerShell window → accept the command as written.
- Fails with "No applicable installer found" (or similar about scope) → run it again **without** `--scope user`. If that works, accept the ADR with the second form: the package is a portable zip that installs for the current user anyway, and the app never runs elevated, so dropping the flag doesn't make the install machine-wide.
- Record which form worked, and the date, in this file before setting `Accepted`.

## Context
ADR-004 keeps FFmpeg out of the download (licence obligations, ~100 MB). Its cost is a manual step:
typing `winget install Gyan.FFmpeg` in PowerShell. Regular users won't do that, so audio/video segments
silently never appear for them.

## Decision
- FFmpeg is **still not bundled**. ADR-004's detection rules are unchanged.
- When the media converter reports `ToolMissing`, the tray menu offers **"Install FFmpeg…"**.
- Clicking it shows a confirmation dialog that says, in plain words: what will be installed (FFmpeg, package `Gyan.FFmpeg`, from the winget community repository), roughly how big it is, that it installs for this Windows user only, and that clicking Install accepts the package's and winget's terms on the user's behalf.
- On "Install", the app runs winget through `ProcessRunner` (no shell, no elevation), with the form confirmed in "Check before accepting":
  `winget install --id Gyan.FFmpeg --exact --source winget --scope user --silent --accept-package-agreements --accept-source-agreements --disable-interactivity`
  (or the same list without `--scope user`).
- winget itself is found with `ToolLocator.Find("winget", …)` (normally `%LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe`). No winget → the dialog explains how to get "App Installer" from the Microsoft Store and shows the manual FFmpeg command to copy. The app never opens a browser or a URL itself.
- After winget exits (any exit code), the app calls `FormatRegistry.Refresh()` and reports the result: FFmpeg found → success notification; not found → error notification with winget's exit code and "Try again or install FFmpeg manually."
- The app never runs winget without that click. No automatic or background installs, no updates.

## Consequences
- No licence change: the user installs FFmpeg; we only start winget for them.
- One new external tool (winget) is launched; it is a Microsoft component and runs with the user's own rights (`app.manifest` stays `asInvoker`).
- Supersedes ADR-004's consequence "Audio/video conversion needs a one-time user step" with "…a one-time click".
