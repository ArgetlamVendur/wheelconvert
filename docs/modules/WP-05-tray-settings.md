# WP-05: Tray icon, menu, notifications, start with Windows

**Builder:** Antigravity · **Reviewer:** Claude Code · **Depends on:** WP-01 · **Risk:** low

## Goal
The app's only permanent UI: a tray icon with a menu, balloon notifications (ADR-007), and the "Start with Windows" option.

## Owns
- `src/WheelConvert.App/Tray/TrayIcon.cs`
- `src/WheelConvert.App/Tray/TrayMenuModel.cs` (new; pure, what the menu shows)
- `src/WheelConvert.App/Tray/StartupRegistration.cs` (new)
- `assets/wheelconvert.ico` (new; original artwork — a simple wheel/circle glyph, no third-party icons)

## Requirements
1. `TrayIcon : INotifier, IDisposable`. **Constructor is frozen:** `TrayIcon(Dispatcher, Func<AppSettings>, FormatRegistry)` (already called by `CompositionRoot`). `Notify` is callable from any thread: marshal with the dispatcher. Map `NotificationKind` to `ToolTipIcon` (Info / Warning / Error). Don't show success balloons if `ShowSuccessNotifications` is false. Load the icon from the embedded resource `WheelConvert.App.wheelconvert.ico` (the csproj already includes `assets/wheelconvert.ico` once the file exists); fall back to `SystemIcons.Application`.
2. Clicking a balloon with `pathToReveal` opens Explorer **with the file selected**: `Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { "/select,", path } })`. Only for paths that exist. `explorer.exe` (reveal here, and opening the settings/log files in req. 3) is the only `Process.Start` allowed outside `ProcessRunner`.
3. Menu (WinForms `ContextMenuStrip`):
   - Header (disabled): "WheelConvert" + version
   - "How to use" → one notification: "Hold Shift and drag a file, or press Ctrl+Alt+Space."
   - Status submenu: one line per unavailable converter from `FormatRegistry.Unavailable` (e.g. "Audio/video: install FFmpeg (winget install Gyan.FFmpeg)"; if `AppSettings.FfmpegPath` is set, "Audio/video: the configured FFmpeg path wasn't found"), plus "Check again" → event `RefreshRequested`
   - Trigger: Shift-drag / Hotkey / Both (radio) → event `TriggerModeChanged(TriggerMode)`
   - "Start with Windows" (check) → `StartupRegistration`
   - "Open settings file", "Open log" → open with the default app via `Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path } })` (CI rejects `UseShellExecute = true` anywhere)
   - "Exit" → event `ExitRequested`
4. `StartupRegistration`: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `WheelConvert` = `"<full exe path>"`. `IsEnabled()`, `Enable()`, `Disable()`. Never touch HKLM. Use `Environment.ProcessPath`.
5. Events for WP-11: `ExitRequested`, `RefreshRequested`, `TriggerModeChanged`. TrayIcon itself never saves settings or restarts triggers.
6. Tooltip text ≤ 63 characters (NotifyIcon limit).

## Gotchas
- `ShowBalloonTip` timeout is ignored on Windows 10+; Windows decides.
- Focus Assist / Do Not Disturb hides balloons: notifications are best-effort; errors are also logged.
- Dispose the NotifyIcon on exit or a ghost icon stays until hovered.

## Acceptance tests
| # | Who | Steps | Expected |
|---|---|---|---|
| 1 | Agent | Build App | 0 warnings |
| 2 | User | Start the app | Tray icon appears with tooltip "WheelConvert" |
| 3 | User | Right-click the icon | Menu as in req. 3 |
| 4 | User | Toggle Start with Windows, check `regedit` HKCU\…\Run | Value appears / disappears |
| 5 | User | Exit | Icon disappears immediately |
| 6 | User | Trigger a success notification (after WP-06/11) and click it | Explorer opens with the file selected |

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md, docs/modules/WP-05-tray-settings.md, docs/decisions/ADR-007-tray-and-notifications.md.
You are the builder for WP-05. Create branch wp-05-tray from main. Restate the plan in 3–5 lines and wait for "go".
```
