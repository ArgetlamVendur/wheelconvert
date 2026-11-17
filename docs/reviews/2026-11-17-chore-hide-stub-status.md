# Review: PR #20 "chore: hide not-implemented converters from tray Status" (2026-11-17)

**Verdict: APPROVE**

- Commit reviewed: `757daa7` (branch `chore-hide-stub-status`), diff `origin/main...HEAD`.
- Reviewer: Claude Opus 5.5, fresh session, did not write the change.
- CI on 757daa7 (`gh pr checks 20`, both the push and the pull_request runs): windows PASS, core-linux PASS.

## Runs (executed by the reviewer)
| Check | Result |
|---|---|
| `dotnet build WheelConvert.sln -c Release --no-incremental` | 0 warnings, 0 errors |
| `dotnet run --project tests/WheelConvert.Core.Tests -c Release` | 69/69, real exit code 0 |
| `dotnet test tests/WheelConvert.App.Tests -c Release` | 118/118 passed |
| Conflict-marker `git grep` | prints nothing |
| `git status` after build | clean (no lock-file changes) |
| Revert check: deleted the `.Where(...NotImplemented)` line | `Not_implemented_converter_is_left_out_and_menu_shows_all_ready` and `Not_implemented_converter_is_left_out_next_to_a_real_problem` FAIL (2 failed, 116 passed); restored with `git checkout`, tree clean |

## What was checked (by reading)
- `TrayMenuModel.Create` filters `Health.Reason == NotImplemented` before building lines; empty → `AllReadyLine`, `AllReady = true`. Same predicate as `AppHost.cs:157` start-up warning.
- Only consumer of `Create` is `TrayIcon.CurrentModel()` → Status submenu lines. `AllReady` has no production consumer (tooltip is the fixed app name; icon state does not depend on it). No other behaviour change.
- Real problems still show: a converter whose `CheckHealth` throws is recorded by `FormatRegistry` as `ConversionError.Unknown`, not `NotImplemented`, so it keeps its line (covered by the updated `One_line_per_unavailable_converter_in_order` with a PDF `Unknown` health).
- Scope: only `TrayMenuModel.cs` and `TrayMenuModelTests.cs` changed. No change to the QR stub, `TargetCatalog.cs`, `Contracts/`, `CompositionRoot.cs`, STATUS, packages or lock files.

## Remaining user-visible "QR" mentions while the stub is NotImplemented
| Place | Reachable? |
|---|---|
| Wheel segment "QR" (`TargetCatalog.cs:17`) | No. Only `QrConverter` declares `image.read-qr`; it is unavailable, so `FormatRegistry` never offers the target. |
| `DropCoordinator.cs:151/155` "QR found…" / "QR copied…" | No. Only for text results, which only the QR target produces. |
| Tray Status line "QR codes: …" | No (fixed by this PR). |
| How-to text, settings defaults | No QR text found. |
| Log file (tray → Open log) | **Yes.** `FormatRegistry.cs:137` logs `Converter 'qr' unavailable: NotImplemented Not implemented yet.` on every registry refresh (start-up and "Check again"). |

## Findings
Required: none.

Optional:
1. `src/WheelConvert.Core/Formats/FormatRegistry.cs:137` — the log still names the QR converter on each refresh, and the log is user-openable from the tray. It is a diagnostic file, not UI, so it does not block this PR; but if "no QR reference to users" is meant strictly, a follow-up could skip or downgrade the Info line for `NotImplemented` (Core change, outside this PR's scope).
2. `src/WheelConvert.App/Tray/TrayMenuModel.cs:105` (NotImplemented branch of `StatusLine`) and `:122` (`"qr" => "QR codes"`) are now unreachable from `Create`. Keeping them is harmless and the PR explains why; removing them is fine in a later cleanup. No action needed.

## Hand check for the user
Install/start the app with FFmpeg installed, right-click the tray icon → Status: it should show only "All converters are ready". Without FFmpeg it should show only "Audio/video: install FFmpeg (winget install Gyan.FFmpeg)" and no QR line.
