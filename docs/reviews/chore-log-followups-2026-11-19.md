# chore-log-followups review — 2026-11-19

**Verdict: APPROVE** for head `e24ffa044eaa4dc7517cf83c131922582e1953c5` (PR #29, branch `chore-log-followups`).

Reviewer: Claude Opus 5.5, fresh session (builder: Sonnet). Scope: `git diff origin/main...origin/chore-log-followups`. The PR fixes findings 1 and 3 of `docs/reviews/WP-21-2026-11-19.md`.

## What I ran (verified by running)
- `dotnet build WheelConvert.sln -c Release`: 0 warnings, 0 errors.
- Core 85/85 (`dotnet run --project tests/WheelConvert.Core.Tests -c Release`, run 4 times on the PR head, the new concurrency test passed every time). App 118/118. Media 155/155, 0 skipped, with real FFmpeg 9.0.2 on PATH. Image 57/57. Pdf 64/64.
- `gh pr checks 29`: windows and core-linux both pass.
- Conflict-marker grep (CI pattern): prints nothing.
- Revert checks (below). The working tree was restored and clean afterwards (`git status --short` empty at `e24ffa0`).

## What I checked by reading only
- Truncation marker format. `ProcessRunner.BoundedBuffer.ToString()` (`ProcessRunner.cs:148`) returns `"[…truncated…]" + Environment.NewLine + _builder`. The builder starts mid-line after `Remove(0, overflow)`, so the line after the marker is the cut one. `LogFailure` splits on `\n` and trims `\r`, so on Windows the first element is exactly `TruncationMarker`. The constant (`MediaConverter.cs:264`) is the same string, with the same U+2026 characters, as the ProcessRunner literal. Dropping marker + one line matches `MapFailure` (`MediaConverter.cs:308`).
- `FileLog`: the lock is a static `ConcurrentDictionary<string, object>` keyed by `Path.GetFullPath`, and `RollIfNeeded` plus the append both run inside `lock (_gate)`. Public API and constructors are unchanged. `CompositionRoot.cs`, `Contracts/*` and `TargetCatalog.cs` are untouched.
- Scope: only `MediaConverter.cs`, `FileLog.cs`, `MediaConverterTests.cs`, `MiscTests.cs` and the WP-21 STATUS row (note "log follow-ups: PR #29") changed. No packages, no new files.

## Findings (none blocking)
1. **`tests/WheelConvert.Core.Tests/MiscTests.cs:271-308`: the roll part of the shared lock has no test that fails when it's reverted.** When I moved `RollIfNeeded()` out of the lock (append still under the shared lock), the test passed 5 of 5 runs. The race it guards against is narrow: two writers both see a file over 1 MB, and the second `File.Move(..., overwrite: true)` replaces `.1` with the freshly started file, which drops the previous 1 MB. That's hard to hit deterministically, and the code is correct as written. Record it as untestable in practice. **Fix:** none needed. Optionally, a follow-up could add a test that pre-fills `.1` with a marker and checks the marker survives.
2. **`MediaConverter.cs:264` and `ProcessRunner.cs:148`: the marker string is duplicated.** The Core test (`ReviewRegressionTests.cs:441`) pins ProcessRunner's literal, and the Media tests use their own literal. If one side changes, no test catches `LogFailure`/`MapFailure` silently no longer matching. This was already true for `MapFailure` before this PR. **Fix (follow-up):** expose the marker as one public constant (for example on `ProcessResult` or `ProcessRunner`) and use it on both sides. That's a Core change, so it belongs in its own PR.
3. **`FileLog.cs:18`: case-insensitive keys on Linux.** On a case-sensitive file system, two distinct files that differ only in case would share one lock. Harmless (it only serialises more than needed), and the app is Windows-only. No fix needed.
4. **Cross-process writes stay unprotected** (two app instances). The builder says so in the PR description and lists a named mutex as a follow-up. Out of scope for this chore. No fix needed here.

## Revert checks
| Rule | Revert (scratch) | Result |
|---|---|---|
| LogFailure drops marker + cut line | condition disabled (`if (false && …)`) | `Failed_run_log_drops_truncation_marker_and_the_cut_line_after_it` fails. |
| LogFailure drops the cut line, not only the marker | `RemoveRange(0, Math.Min(1, …))` | Same test fails on `DoesNotContain(cutTail)`. |
| One lock per file across instances | `_gate = new object()` | `FileLog_SeveralInstancesAndThreads_KeepEveryLineIntact` failed 3 of 3 runs (382, 378, 393 of 540 lines). |
| Roll inside the lock | `RollIfNeeded()` moved before `lock` | Test passed 5 of 5 runs. Not caught (finding 1). |

## Checklist
- Safety: no file operation on user paths. `FileLog`'s `File.Move` is only in the app's own log folder, which is exempt from C2 and was already there before this PR. Stderr is still logged as text only, and the redaction still runs before the 300-character cut. ✅
- Frozen contracts: untouched. No constructor or `CompositionRoot` change. ✅
- Converter behaviour: logging is still inside `try/catch` and can't change the result. ✅
- CI green; Release 0 warnings; all test projects pass. ✅
- No dead code, no commented-out code, no untagged TODOs. Comments explain why. ✅
- Reviewer differs from the builder (Opus vs Sonnet), fresh session. ✅
