# Review: PR #13 `chore-toollocator-path` (ToolLocator re-reads PATH)

Date: 2026-11-12 · Reviewer: Claude Code (Fable 5.2), fresh session · Builder: Claude Opus 5.5 · Reviewed commit: `194dbc7` against `main` `0dd3aea`

## Verdict: APPROVE

No blocking findings. Six non-blocking findings below; the two worth acting on are 1 (de-duplication has no test that would catch its removal) and 2 (two rare registry exceptions are not caught).

Not verified by anyone yet: the real end-to-end case "install FFmpeg while WheelConvert is running, tray → Check again". It can't be, because the media converter (WP-07) is still a stub on `main`. It is WP-07 acceptance row 7.

## What was checked

| # | Check | Result | How |
|---|---|---|---|
| 1 | Public `Find` signature unchanged | PASS | Read the diff: `ToolLocator.cs:15` is an unchanged context line |
| 2 | Explicit `pathVariable` is deterministic, no registry read | PASS | Read (`ToolLocator.cs:19-22` returns before any registry call); scratch run |
| 3 | Configured path wins and never falls back | PASS | Read (`:25-28`, `:58-61`); scratch run; mutation M5 |
| 4a | User/machine PATH read only on Windows | PASS | Read (`:32`); Linux CI green |
| 4b | `%VARS%` expanded | PASS | Mutation M2; scratch run |
| 4c | De-duplicated | PASS by reading only; **not covered by a test** (finding 1) | Read (`:72-87`); mutation M4 |
| 5 | Order: process > user > machine > WinGet Links (local, then ProgramFiles) | PASS; the order of the two Links folders is by reading only | Mutations M3, M6; read `:134-145` |
| 6 | No new packages | PASS | Diff touches no `.csproj`; registry is read through `Environment.GetEnvironmentVariable(name, target)`, which is built into .NET |
| 7 | Cost well under 50 ms | PASS: about 1.5 ms typical, 6 ms worst | Scratch run, figures below |
| 8 | Reverted-fix check on the 3 new tests | PASS: all 3 fail when the fix is removed | Mutation M1 |
| 9 | CI green on `windows` and `core-linux` | PASS | `gh pr checks 13`; CI log |

Only `src/WheelConvert.Core/Infrastructure/ToolLocator.cs` and `tests/WheelConvert.Core.Tests/MiscTests.cs` changed. No frozen contract file, no P/Invoke, no file writes, no process launch.

### Evidence, run (Windows, this PC)

- `dotnet build WheelConvert.sln -c Release`: 0 warnings, 0 errors.
- Core tests 67/67. App tests 87/87. Conflict-marker grep prints nothing.
- **CI**: `windows` and `core-linux` pass on both the push run and the pull-request run. In the log of run 43160164686: Windows shows "No conflict markers.", "Safety grep passed (60 files).", Core 67/67, App 87, PDF 64. Linux shows Core 67/67 with all three new tests listed as PASS.
- **Reverted-fix check.** Each change was made to `ToolLocator.cs` in the working tree, the Core tests were run, and the file was restored with `git checkout`. `git status` was clean afterwards.

  | # | Change made | Tests that failed |
  |---|---|---|
  | M1 | Removed the user and machine PATH sources (the fix itself) | All 3 new tests (64/67) |
  | M2 | Removed `%VAR%` expansion | `ExpandsVariablesInRegistryPath_AndToleratesDuplicates` |
  | M3 | Machine PATH searched before user PATH | `SearchOrder_…` |
  | M5 | A bad configured path falls back to searching | `FindsConfigured_ThenPath_ThenExtraDirs` and `FindsToolOnlyInUserPath_…` |
  | M6 | Extra directories searched before the PATH sources | `SearchOrder_…` |
  | M4 | Removed de-duplication | **None, 67/67 pass** (finding 1) |
  | M7 | Removed the quote and whitespace trimming of entries | **None, 67/67 pass** (finding 6) |

- **Public method, real registry.** A throwaway console app in the scratchpad called the real public `Find`. FFmpeg on this PC is on the user PATH; it is *not* in either WinGet Links folder.

  | Call | Result |
  |---|---|
  | `Find("ffmpeg")` | found |
  | Same, after setting the process PATH to empty (simulates an app started before the install) | found, so it came from the registry |
  | Same, with `extraDirectories: []` | found, so it did not come from the Links folders |
  | `Find("ffmpeg", null, "")` (explicit empty PATH, default Links folders) | null |
  | `Find("ffmpeg", null, "", [])` | null |
  | `Find("ffmpeg", @"C:\wc-nope\ffmpeg.exe")` | null |
  | Same bad configured path, plus the real user PATH passed as `pathVariable` | null |

  Rows 2 and 4 together show the old code would have returned null on this PC after a late install, and that an explicit `pathVariable` does not reach the registry.
- **Timing**, same app, Release, 35 process + 9 user + 16 machine PATH entries: first call 6.1 ms; 200 further hits median 1.2 ms, max 3.3 ms; 200 misses (every directory probed) median 1.5 ms, max 5.6 ms. All on a local SSD; a PATH entry on a disconnected network drive would be slower, but that was already true for the process PATH before this PR.

### Evidence, read only

- **Windows-only gate**: the two registry reads sit inside `if (OperatingSystem.IsWindows())` at `ToolLocator.cs:32`. Not run on Linux by me; the Linux CI job ran the tests.
- **Links order**: `%LOCALAPPDATA%\Microsoft\WinGet\Links` is yielded before `%ProgramFiles%\WinGet\Links`. No test covers the default list, and neither folder holds FFmpeg here.
- **De-duplication**: case-insensitive on Windows, case-sensitive elsewhere; surrounding quotes and trailing slashes are ignored for the comparison. The first occurrence wins, so it cannot change which file is returned, only how many folders are probed.
- **Configured path**: when one is set, the public method returns before the registry is read, and the internal method returns before any directory is listed.
- **Callers**: none on `main` yet. `MediaConverter.cs:39` is still the WP-07 TODO.

## Checklist (docs/checklists/review.md)

- Safety: no source or output file handling, no drag-drop, no hooks, no external process, no untrusted text executed. The change only calls `File.Exists`. All lines pass or do not apply.
- Contract: no frozen file touched. This is a chore PR with no WP "Owns" list; the two files are the ones the PR names. Converter lines do not apply.
- Windows / interop: no P/Invoke. Reads HKCU and HKLM environment values read-only; no elevation needed.
- Quality: CI green; 0 warnings; App and Core tests pass; reverted-fix check done; reviewer is a different model in a fresh session; no new packages; no dead code or TODOs; no user-facing text.

## Findings

1. **Not blocking. `tests/WheelConvert.Core.Tests/MiscTests.cs:204-223`: the test named `…AndToleratesDuplicates` does not test de-duplication.**
   Why: with de-duplication removed (M4) all 67 tests still pass. The test only shows that a PATH with repeated entries still finds the tool, which was true before this PR. A later edit could break the de-duplication unnoticed. The effect would be extra folder probes, never a wrong result.
   Fix: either accept it as an untested optimisation and rename the test, or make the directory list observable to tests (for example an internal method that returns the ordered, de-duplicated list) and assert on it.

2. **Not blocking. `ToolLocator.cs:114-125`: `ReadRegistryPath` catches only `SecurityException`.**
   Why: reading a registry value can also throw `UnauthorizedAccessException` or `IOException` (rare: restrictive permissions, or a key being deleted). Either would escape `Find` and reach `CheckHealth()`, where a registry problem should mean "use the process PATH and the Links folders", not an error.
   Fix: catch `UnauthorizedAccessException` and `IOException` as well, with the same `return null`.

3. **Not blocking. Stale descriptions of the search order.**
   Why: `docs/decisions/ADR-004-ffmpeg-not-bundled.md:8` and the comment at `src/WheelConvert.Core/Settings/AppSettings.cs:42` still say "PATH → `%LOCALAPPDATA%\Microsoft\WinGet\Links`". Neither file is in this PR.
   Fix: follow-up edit to both lines: process PATH → user PATH → machine PATH → the two Links folders.

4. **Not blocking, information for WP-07 (PR #12).**
   Why: I agree with the PR's plan. On this PC `Find("ffmpeg")` now finds the real FFmpeg through the registry, so a media test that expects `Missing` and relies on the search coming up empty will fail here. The WP-07 spec calls `ToolLocator.Find("ffmpeg", provider())`, which gives a test no way to pass `pathVariable`.
   Fix: in WP-07, a test that needs `Missing` should give the converter a configured path that does not exist; that returns null on every machine (checked above). Do not change the locator.

5. **Not blocking. `ToolLocator.cs:68-69`: the `%VAR%` expansion is a second pass in the real code path.**
   Why: .NET already expands the registry value. Checked here: the raw user PATH in the registry contains `%`, the value returned by `Environment.GetEnvironmentVariable("PATH", User)` does not. The extra pass is harmless and is what the injected test exercises. Both passes use the running app's own variables, so a PATH entry that refers to a variable created after the app started stays unexpanded and is skipped until restart. That is an uncommon case and no worse than before.
   Fix: none needed.

6. **Not blocking, older than this PR. `ToolLocator.cs:76`: stripping quotes from PATH entries has no test.**
   Why: M7 passes 67/67. The quoted entry in the new test is followed by the same folder unquoted, so the tool is found either way.
   Fix: optional: one assert with only a quoted entry.

## Not checked

- Linux behaviour on a real Linux machine (CI only).
- Machine-scope winget installs into `%ProgramFiles%\WinGet\Links`: no such install on this PC.
- The real app flow (tray → Check again) after a late FFmpeg install: needs WP-07.
- PR #12 (WP-07) itself.
