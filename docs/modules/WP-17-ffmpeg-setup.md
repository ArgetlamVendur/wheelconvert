# WP-17: One-click FFmpeg install

**Builder:** Sonnet · **Reviewer:** Opus (fresh session) · **Depends on:** WP-05, WP-07 (both done), **WP-21** (hardening first: this WP brings FFmpeg to many more users). **Not** WP-13 · **Risk:** medium

> **User approval needed before starting:** ADR-012 must be `Accepted`, with the winget command form the user confirmed by hand (ADR-012, "Check before accepting"). Build exactly that form.

## Goal
A regular user gets audio/video working with one click from the tray, instead of typing a PowerShell command (ADR-012).

## Owns
- New folder `src/WheelConvert.App/Setup/` (`FfmpegInstaller.cs`, `InstallFfmpegWindow.xaml(.cs)`)
- The tray menu file(s) from WP-05 (find them: `git grep -n "Check again" src/WheelConvert.App`): add one menu item only
- `tests/WheelConvert.App.Tests/**` (new tests only)
- `docs/decisions/ADR-012-*.md` (status line only, after the user accepts)

## Must not touch
`ProcessRunner`, `ToolLocator` (use them as they are), converter projects, `app.manifest`.

## Requirements
1. Tray: when the media converter's health is `ToolMissing`, show **"Install FFmpeg…"** above "Check again". Hidden when FFmpeg is found.
2. Click → a small window (WPF, same style as the WP-09 window) with the text from ADR-012 (what, from where, size, user-only, accepts terms) and **Install** / **Cancel**. Plain English, each line ≤ 120 characters.
3. Find winget with `ToolLocator.Find("winget", …)`. Not found → the window says how to get "App Installer" from the Microsoft Store and shows `winget install Gyan.FFmpeg` in a read-only text box with a **Copy** button. No browser, no URL opening.
4. Install → run exactly the argument list in ADR-012 through `ProcessRunner.RunAsync` (argument list, no shell, `UseShellExecute = false`), timeout **15 min**, cancellable from the window (Cancel kills the process tree). Show an indeterminate progress bar and "Installing… this can take a few minutes."
5. When winget exits (any code): call `FormatRegistry.Refresh()` off the UI thread, then:
   - FFmpeg found → close the window, success notification "FFmpeg installed. Audio and video conversions are ready."
   - Not found → keep the window open with "Install didn't finish (code <n>). Try again or install FFmpeg manually." and the copy box from req. 3. Log the last 20 lines of winget's output.
6. Only one install at a time (menu item disabled while running). Never start winget without the click. Never elevated.
7. winget's output may not be UTF-8 (CONTRACTS C9): use it for the log only, never in the UI.

## Gotchas
- `winget.exe` in `WindowsApps` is an app execution alias (a 0-byte reparse point). `File.Exists` returns true and `Process.Start` with the full path works; confirm `ToolLocator` finds it, and if not, write the proposal in the PR (don't change `ToolLocator`).
- winget installs Gyan.FFmpeg as a portable package and links it in `%LOCALAPPDATA%\Microsoft\WinGet\Links`, which `ToolLocator` already searches (ADR-004).
- First winget run may ask to accept source agreements: the `--accept-*` flags cover it; `--disable-interactivity` stops it from hanging.

## Acceptance tests
| # | Who | Test | Expected | Result |
|---|---|---|---|---|
| 1 | Agent | Argument list built for the install | Exactly ADR-012's list, as separate elements | |
| 2 | Agent | Fake runner: exit 0 + FFmpeg now found | Refresh called, success path | |
| 3 | Agent | Fake runner: exit non-zero, FFmpeg not found | Error text with code, copy box shown | |
| 4 | Agent | Fake locator: no winget | Store instructions + copy box; runner never called | |
| 5 | Agent | Cancel during install | Process tree killed, menu item enabled again | |
| 6 | User | Uninstall FFmpeg (`winget uninstall Gyan.FFmpeg`), restart the app, tray → Install FFmpeg… → Install | Audio/video segments appear without restarting | NEEDS USER |
| 7 | User | With FFmpeg installed | No "Install FFmpeg…" item | NEEDS USER |

## Out of scope
Updating FFmpeg, choosing another FFmpeg package, installing anything else.

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md (C9), docs/decisions/ADR-004-ffmpeg-not-bundled.md, docs/decisions/ADR-012-ffmpeg-assisted-install.md, docs/modules/WP-17-ffmpeg-setup.md.
Confirm ADR-012's status is Accepted; if it is still Proposed, stop and ask the user.
You are the builder for WP-17. Create branch wp-17-ffmpeg-setup from main. Restate the plan in 3–5 lines and wait for "go".
```
