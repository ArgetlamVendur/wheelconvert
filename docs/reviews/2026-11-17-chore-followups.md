# Review: PR #18 "chore: follow-ups (reveal with commas, explorer path, WebP limit, README)"

- **Verdict: APPROVE**
- Commit reviewed: `ad1d793a5bd9e16c7f531065cc553d3b83fd93ba` (branch `chore-followups`), diff `origin/main...HEAD`
- Reviewer: Claude Code (Opus), fresh session, did not see the build.
- CI (`gh pr checks 18`, latest commit): `windows` pass, `core-linux` pass (both push and pull_request runs).

## Runs (all run by the reviewer in the worktree)
| Check | Result |
|---|---|
| `dotnet build WheelConvert.sln -c Release` | 0 warnings, 0 errors |
| `dotnet run --project tests/WheelConvert.Core.Tests -c Release` | 69/69, exit code 0 |
| `dotnet test tests/WheelConvert.App.Tests -c Release` | 90/90 pass |
| `dotnet test tests/WheelConvert.Converters.Image.Tests -c Release` | 55/55 pass |
| Conflict-marker grep (CI pattern) | prints nothing |
| `git grep -n 'UseShellExecute *= *true' -- src` | prints nothing |
| Frozen files (`Contracts/*`, `Directory.Build.props`, `CompositionRoot.cs`, `docs/CONTRACTS.md`, `app.manifest`, `*.csproj`) | unchanged; `new TrayIcon(dispatcher, CurrentSettings, registry)` call unchanged |

## Reverted-fix checks (each restored with `git checkout -- <file>`; `git status` clean afterwards)
1. (c) single-image WebP size check removed: `ConvertAsync_WebpOutputOver16383Px_...` **fails** (54/55). Pinned.
2. (c2) animated-GIF frame check removed: 55/55 still pass. **Not pinned** (optional finding O1).
3. (a) existence check in `ShellReveal.Reveal` removed (kept the fully-qualified check so no window could open): 3/3 ShellReveal tests still pass. **Not pinned**: `SHParseDisplayName` also fails for a missing file, so the test passes either way (optional finding O2). No Explorer window was opened.

Extra scratch probe (file created and deleted, not committed): on a new `Thread` with `SetApartmentState(STA)`, the first call `CoInitializeEx(NULL, COINIT_APARTMENTTHREADED)` returned `S_FALSE` (1), i.e. the .NET 8 runtime had already initialised COM as STA when the thread started. The PR's claim is verified by running, not just by reading.

## P/Invoke check against learn.microsoft.com (pages fetched by the reviewer)
- `SHParseDisplayName` (https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shparsedisplayname): doc `HRESULT (PCWSTR pszName, IBindCtx* pbc, PIDLIST_ABSOLUTE* ppidl, SFGAOF sfgaoIn, SFGAOF* psfgaoOut)`. Code: `int (string UTF-16, nint, out nint, uint, out uint)`. Matches (SFGAOF = ULONG = 32-bit `uint`; `ppidl` set to NULL on error; `psfgaoOut` is optional, passing a valid pointer is fine; `sfgaoIn = 0` queries no attributes). HRESULT returned as `int` and checked with `hr >= 0`, no `SetLastError` (correct, HRESULT API). Doc says call from a background thread: done.
- `SHOpenFolderAndSelectItems` (https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-shopenfolderandselectitems): doc `HRESULT (PCIDLIST_ABSOLUTE pidlFolder, UINT cidl, PCUITEMID_CHILD_ARRAY apidl, DWORD dwFlags)`. Code: `int (nint, uint, nint, uint)`. Matches. Doc: "If cidl is zero, then pidlFolder must point to a fully specified ITEMIDLIST describing a single item to select. This function opens the parent folder and selects that item." So `(pidl, 0, 0, 0)` is correct; dwFlags 0 = no OFASI_EDIT/OFASI_OPENDESKTOP. Doc requires CoInitialize(Ex) first: satisfied (probe above).
- `ILFree` (https://learn.microsoft.com/windows/win32/api/shlobj_core/nf-shlobj_core-ilfree): doc `void ILFree(PIDLIST_RELATIVE pidl)`, "This parameter can be NULL". Code: `void (nint)`. Matches; the "safe with NULL" comment is correct. The page also notes that on Windows 2000+ `CoTaskMemFree` is recommended instead (optional finding O3).
- Calling convention: `SHSTDAPI` = stdcall; `LibraryImport` default is the platform default (stdcall on x86, the only x64 convention on x64). Correct. All three in `src/WheelConvert.App/Interop/` with their URL.
- PIDL lifetime: freed in `finally` after `SHOpenFolderAndSelectItems`, in `CanResolve`, and on the error path of `Resolve`. No leak found.

## Other points checked by reading
- Threading: `RevealAsync` starts a background STA thread and returns at once; the UI thread (`OnBalloonClicked`) never waits. Exceptions inside the thread are caught, logged, and the task completes with `false`. `IsBackground = true`, so a hung Shell call can't keep the app alive at exit. One thread per notification click; no pooling needed.
- Untrusted text: the file path is only passed to `File.Exists`/`Directory.Exists` and `SHParseDisplayName`; no command line is built. Log messages do not include the path.
- (b) `OpenAppFile` now uses `%WINDIR%\explorer.exe` via `Environment.SpecialFolder.Windows`, `ArgumentList`, `UseShellExecute = false`. Only app-owned paths (settings, log) reach it. The old `/select,` launch is gone.
- (c) Check runs after `AutoOrient` (WebP path has no resize, so this is the final output size) and per GIF frame, before `WriteImage`/`WriteCollection`. Thrown as a private exception, mapped to `Fail(Unsupported, "Image is too large for WebP.")` (28 chars) before the generic handlers; temp is discarded by the existing `finally`. Test asserts no `.webp`, no temp file, source the only file left.
- (d) README: Focus/Do-not-disturb line and SDK 8.0.400 note added; no new QR mention.
- STATUS.md: WP-10 `SHELVED (user, 2026-11-17)`, `wp-10-qr (draft PR #17)`, reviewer `—`; WP-09/WP-12 reviewer Opus; Opus-replaces-Fable line. Matches the brief.
- Second `FileLog` in `TrayIcon`: `FileLog` never throws (all I/O in `try/catch`), opens with `FileShare.ReadWrite | FileShare.Delete`, writes only under `%LOCALAPPDATA%\WheelConvert\` (exempt). Two instances have separate locks, so in the rare case both write at the same moment a line could interleave or a roll-over could race; the exception is swallowed. Cannot crash the app or touch user files. Optional (O4).

## Findings

### Required
None.

### Optional
- **O1** `tests/WheelConvert.Converters.Image.Tests/ImageConverterTests.cs` (no test): the animated-GIF frame check (`ImageConverter.cs:255`) is not pinned; removing it leaves 55/55 green. Fix: add a test with a 2-frame GIF 16384x1 → WebP expecting the same Fail message and no output.
- **O2** `src/WheelConvert.App/Interop/ShellReveal.cs:55`: the missing-file test also passes without the existence check, because `SHParseDisplayName` fails for a missing path. Behaviour is safe either way (nothing opens), but the test does not prove the guard. Fix: optionally assert the "Reveal skipped" log line through a fake `ILog`.
- **O3** `src/WheelConvert.App/Interop/NativeMethods.Shell.cs:29`: the `ILFree` page recommends `CoTaskMemFree` on Windows 2000+. `ILFree` is still documented and correct; no change needed unless the team wants to follow the note.
- **O4** `src/WheelConvert.App/Tray/TrayIcon.cs:37`: second `FileLog` on the same file (frozen constructor has no `ILog`). Safe as explained above; could share the app's log if the constructor is ever reopened by ADR. Already listed in the PR follow-ups.
- **O5** `src/WheelConvert.App/Interop/ShellReveal.cs:57`: a relative path logs "the file no longer exists", which is misleading in the log only. Trivial.
- **O6** `src/WheelConvert.Converters.Image/ImageConverter.cs:302`: the WebP limit is orientation-independent (same limit on both sides), so it could be checked from the header (`info.Width/Height`) before decoding, saving memory on huge images. Current placement meets the brief ("before encoding").

## What the user must try by hand (NEEDS USER)
1. In a scratch folder (e.g. `<wc-test>\`), make a copy of a photo and rename it `a,b.png`. Convert it with the wheel (e.g. to JPG). Click the "done" notification: Explorer should open that folder with the new file selected.
2. Repeat with a file named `-x, y.png`: same result, Explorer opens the right folder, nothing else happens.
3. Convert a file, delete the output before clicking the notification, then click it: nothing should open; `%LOCALAPPDATA%\WheelConvert\wheelconvert.log` should contain "Reveal skipped".
4. Tray menu → open log / open settings: they still open.
5. Optional: convert a PNG wider than 16383 px to WebP: notification "Image is too large for WebP." and no new file.
