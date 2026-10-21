# Checkpoint 2 — Final audit of the whole base

**Reviewer:** a second, fresh independent Claude agent (had not seen checkpoint 1), 2026-10-20
**Scope:** everything: App stubs (read for compile errors), converters, Core re-check, CI, all docs and work packages.
**Verdict:** CHANGES NEEDED (1 blocker, 9 major, 11 minor) → all fixed below. No compile errors predicted in the App stubs. No new Core bugs found.

**Verified by running:** the solution parses with all 7 projects; `CodeAnalysisTreatWarningsAsErrors=false` keeps CA findings as warnings while compiler warnings still fail; App language patterns compile in a scratch net8.0 project; all relative doc links resolve; AGENTS.md < 12,000 chars.
**Not verified:** WPF compile (no Windows), the PowerShell grep (rewritten defensively), licence details (no internet: marked "believed" for WP-12 to verify).

| # | Sev | Finding | Fix |
|---|---|---|---|
| 1 | BLOCKER | `LibraryImport` (recommended for Interop) needs `AllowUnsafeBlocks`; csproj had it off, and only WP-11 may edit the csproj | `AllowUnsafeBlocks=true` in the base |
| 2 | MAJOR | Mouse-up: hook's dismiss can hide the wheel before Explorer delivers `Drop` → the main gesture converts nothing | WP-11 defers dismiss ~300 ms; WP-04 keeps segments until Drop/DragLeave handled; acceptance tests "release on a segment converts" added to both |
| 3 | MAJOR (safety) | Magick sniffs content: a `.png` that's really MVG/SVG can read/write other files | WP-06/WP-10: explicit format from extension + security policy denying risky coders; MVG-payload tests; review checklist line |
| 4 | MAJOR | Converter tests never ran in CI | `build.yml` runs any `tests/WheelConvert.Converters.*.Tests` on Windows and Linux |
| 5 | MAJOR | C9 said progress on stdout; WP-07 said stderr; `-loglevel error` hides the duration | One rule everywhere: `-loglevel info -nostats -progress pipe:2`, parse stderr |
| 6 | MAJOR | WP-05/WP-09 needed constructor/csproj changes owned by WP-11 | Final constructors `TrayIcon(Dispatcher, Func<AppSettings>, FormatRegistry)` and `PageOrderPrompt(IPdfPageRenderer, ILog)` wired now; icon resource pre-added (conditional) |
| 7 | MAJOR | Hotkey "press again to close" needs wheel state the trigger doesn't have | WP-03 always raises `WheelRequested`; WP-11 toggles using `wheel.IsOpen` |
| 8 | MAJOR | PowerShell grep fragile; missed `DragDropEffects.All` | Rewritten with `@()`/`Where-Object`; pattern `Move|All`; also scans XAML |
| 9 | MAJOR | ADR-005 understated native delegate licences (LGPL HEIC libs, FreeType…) | ADR-005 updated; WP-06 lists delegates; WP-12 inventories native licences before release |
| 10 | MAJOR | sln/ADR-010 edited by parallel WPs; ADR-005 told WPs to edit files they don't own | AGENTS §2: sln + ADR-010 are shared append-only; ADR-005: converter WPs edit ADR-010 only, WP-12 writes notices |
| 11 | MINOR | Tray disposed on a pool thread at exit | `AppHost`: CancelAll, dispose tray on UI thread before the await |
| 12 | MINOR | Health checks (native loads) on the UI thread | WP-11 builds registry/Refresh via `Task.Run`; C1 wording allows one-time native load |
| 13 | MINOR | `Path` ambiguity (CS0104) in WP-04 | Gotcha + alias; ADR-007 list extended |
| 14 | MINOR | PDF opened with `FileShare.Read` fails if another app has it open | `ReadWrite | Delete` |
| 15 | MINOR | Contradictions on allowed `Process.Start` and balloon-click behaviour | WP-05 and ADR-007 aligned (explorer.exe only; selects the file) |
| 16 | MINOR | ARCHITECTURE diagram used old API names | Updated (`GetTargetsFor(paths)`, `EnqueueBatch`, `BatchCompleted`) |
| 17 | MINOR | Frozen list missed `IWheelView`; file-op rule too broad | AGENTS §4.1/4.2 updated |
| 18 | MINOR | `Save` can also throw `UnauthorizedAccessException` | Documented; WP-11 catches both |
| 19 | MINOR | Progress could be delivered out of order | Forwarded inside the lock |
| 20 | MINOR | Single-file publish left native DLLs beside the exe | `IncludeNativeLibrariesForSelfExtract=true` in CI; WP-12 note |
| 21 | MINOR | Typo in test csproj comment; FFmpeg configured-path status message | Fixed; WP-05 status text added |
