# Step 01 — Skeleton

**Builder:** Antigravity  **Reviewer:** Claude Code
**Branch:** `step-01-skeleton`
**Spec refs:** Sections 3, 4.4, 4.5 of `docs/2026-10-19-phase0-spike-spec.md`

## Build
1. Create WPF project at `src/WheelConvert/` (.NET 8, `net8.0-windows`, `UseWPF=true`, Nullable enabled).
2. No main window: remove `StartupUri`; app starts and stays running with no visible window (`ShutdownMode=OnExplicitShutdown`).
3. Single instance via named mutex `Global\WheelConvert.Spike` — second launch writes "already running" to the log and exits.
4. `Logging/SpikeLog.cs`: appends `<ISO-8601 timestamp> <message>` to `%LOCALAPPDATA%\WheelConvert\spike.log`; creates the folder if missing; thread-safe.
5. On startup log `started`. On exit log `stopped`.
6. Add `app.manifest` with `requestedExecutionLevel level="asInvoker"`.
7. Create folders `Interop/`, `Input/`, `Wheel/` (empty `.gitkeep` is fine).

## Do NOT
- Add a tray icon, any window, any hook, any NuGet package.

## How to test
| # | Who | Steps | Expected |
|---|---|---|---|
| 1 | Agent | `dotnet build src/WheelConvert -c Debug` | 0 errors |
| 2 | User | `dotnet run --project src/WheelConvert` | No window appears; `%LOCALAPPDATA%\WheelConvert\spike.log` has a `started` line |
| 3 | User | Run the same command in a second terminal | Second process exits within 2 s; log has `already running` |
| 4 | User | End the process in Task Manager | (no `stopped` line is fine for a killed process) |

## Done when
All 4 tests pass and HANDOFF entry is written.

## Reviewer focus
Manifest is `asInvoker`; mutex is released on exit; logger cannot throw on a missing folder.
