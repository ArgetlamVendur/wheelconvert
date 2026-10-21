# WP-11: Integration

**Builder:** Claude Code · **Reviewer:** Antigravity · **Depends on:** WP-01; works against the stubs of 02–10 · **Risk:** high (ties everything together)

## Goal
Make the modules work as one app: start-up, wiring, drop handling, results, fallback, shutdown. After this WP, any converter that becomes available "just appears" on the wheel.

## Owns
- `src/WheelConvert.App/App.xaml`, `App.xaml.cs`
- `src/WheelConvert.App/Composition/**` (`CompositionRoot`, `AppHost`, `DropCoordinator`, `ClipboardService`, new files here)
- `src/WheelConvert.App/WheelConvert.App.csproj` (shared — coordinate: other WPs only add files, never edit it)

## Requirements
1. **Start-up** (`App.OnStartup` → `CompositionRoot.Build` → `AppHost.Start`): single instance (exists); load settings; build registry with all converters; queue; tray; wheel; triggers per `AppSettings.Trigger`. Converter health checks may load native libraries (Magick, PDFium): build the `FormatRegistry` and run every `Refresh()` via `Task.Run`, never on the UI thread; until ready the wheel shows "Starting…".
   - Constructors of `TrayIcon(Dispatcher, Func<AppSettings>, FormatRegistry)` and `PageOrderPrompt(IPdfPageRenderer, ILog)` are frozen; WP-05/WP-09 implement inside them.
2. **Global exception handling:** `DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` → log; for the dispatcher one, show one error notification and mark handled (don't crash on a UI bug). Never swallow silently.
3. **Trigger → wheel:**
   - `WheelRequested` → if `wheel.IsOpen` **and the sender is the HotkeyTrigger**, hide it (toggle; WP-03 doesn't track wheel state); otherwise `wheel.ShowAt(point)`.
   - `WheelDismissRequested` → **defer ~300 ms** (`DispatcherTimer`), then `wheel.HideWheel()` unless a drop happened meanwhile. Reason: on mouse-up the hook's dismiss can arrive before Explorer delivers `Drop` to the wheel; hiding immediately would lose the drop (WP-04 req. 8 guards the other side).
4. **Fallback (ADR-006):** if `ShiftDragTrigger.Start()` returns `false`, start a `HotkeyTrigger` too and notify once: "Shift-drag isn't available on this PC. Use Ctrl+Alt+Space." If the hotkey also fails, notify with the reason. Log both.
5. **Hook liveness:** every 60 s, if the Shift-drag trigger is active and `LastActivityUtc` is older than 10 min while `GetLastInputInfo` shows recent input, `Stop()` + `Start()` it and log "hook reinstalled". (Interop for `GetLastInputInfo` goes in `Interop/NativeMethods.Input.cs`, owned here.)
6. **Wheel → drop:** `wheel.Dropped` → `DropCoordinator.HandleDropAsync(paths, target)`:
   - If `target.RequiresInteraction` (pdf.reorder): only the first PDF is used; call `IPageOrderPrompt`; `null` → stop; else options `{ pageOrder = "3,1,2" }`.
   - Always add `OptionKeys.OutputLabel` = `AppSettings.OutputLabel` (converters read `request.OutputLabel`).
   - `await Task.Run(() => DropPlanner.Plan(...))` (off the UI thread); `queue.EnqueueBatch(plan.Requests)`; if `plan.Skipped` is non-empty, one Warning notification: "Skipped N files: <first reason>".
7. **Results** (`JobCompleted`, worker thread → `Dispatcher.BeginInvoke`):
   - Notify per **batch**, from `BatchCompleted` (not per job): one job → "Converted photo.png → JPG" (`pathToReveal` = first output); several → "Converted 5 of 6 files", reveal the first output. Respect `ShowSuccessNotifications` for successes.
   - Text results (QR): `await clipboard.SetTextAsync(text)` + "QR copied: <first 80 chars>" (or "QR found but the clipboard was busy" if it returned false). Never open it.
   - Failures: one Error notification per batch naming the first failure: "<file name>: <message>"; partial outputs (`OutputPaths` on a failed result) are revealed. `Cancelled` → no notification.
   - Optional: show `JobProgress` for jobs longer than 5 s in the tray tooltip.
8. **Tray events:** `ExitRequested` → `Application.Current.Shutdown()`; `RefreshRequested` → `await Task.Run(registry.Refresh)` + notify the result; `TriggerModeChanged` → save settings (catch `IOException` **and** `UnauthorizedAccessException`; if `Save` returns false, notify "Settings file is locked; change not saved"), dispose and recreate triggers (no restart needed).
9. **Settings reload:** `CompositionRoot` keeps one `AppSettings` in a field updated on save; `Func<AppSettings>` reads it.
10. **Stale temp clean-up (ADR-002):** keep a list (in memory, max 50) of folders written to this session; on start-up, scan only the folders recorded in `%LOCALAPPDATA%\WheelConvert\recent-folders.txt` (written by this WP), delete stale temps with `SafeOutputWriter.TryDeleteStaleTemp(path, TimeSpan.FromHours(1))` (it only touches exact `.~wc-<32 hex>.<ext>` names). Never scan whole drives. Run it in the background after start-up.
11. **Shutdown:** `AppHost.DisposeAsync`: stop triggers → hide wheel → `queue.CancelAll()` → dispose queue → dispose tray. Must finish < 5 s.
12. Once CI is green, set `CodeAnalysisTreatWarningsAsErrors` back to `true` in the App csproj and fix findings.

## Acceptance tests
| # | Who | Steps | Expected |
|---|---|---|---|
| 1 | Agent | CI green (Windows build + publish) | ✔ |
| 2 | User | Run with all converters still stubs | Tray icon; wheel opens via hotkey; any file shows "Nothing available" |
| 3 | User | After WP-06: drop `photo.png` on JPG | `photo (converted).jpg`; notification; click reveals it |
| 4 | User | Drop 3 PNGs + 1 TXT on JPG | 3 outputs; "Skipped 1 file" warning; summary |
| 5 | User | Kill Explorer during a drag, restart it | App survives |
| 6 | User | Exit from tray during a long conversion | Exits < 5 s; no `.~wc-` file left after next start |
| 7 | User | Switch trigger mode from the tray | Takes effect immediately |
| 8 | User | Start a second copy | Exits silently; first copy keeps running |
| 9 | User | Create `photo (converted).jpg` with some text in it, then convert `photo.png` → JPG | New file is `photo (converted 2).jpg`; your text file is untouched |
| 10 | User | Shift-drag and release directly on a segment | Converts (dismiss deferral works) |

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md (all), docs/ARCHITECTURE.md, docs/modules/WP-11-integration.md, ADR-002, ADR-006, ADR-007.
You are the builder for WP-11. Create branch wp-11-integration from main. Restate the plan in 3–5 lines and wait for "go".
```
