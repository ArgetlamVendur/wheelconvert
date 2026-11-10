# Review checklist (every PR)

Reviewer: copy this into your review entry and tick or explain each line. Any ❌ on a **safety** line = CHANGES NEEDED.

## Safety (blocking)
- [ ] **S1–S6** (docs/CONTRACTS.md C2): sources opened read-only; output only via `CreateTempPath` → `Commit`; `Discard` on every failure path (`try/finally`); nothing written outside the source folder.
- [ ] No `File.Delete`, `File.Move`, `File.Copy(..., true)`, `FileMode.Create`/`Truncate` on any path that didn't come from `CreateTempPath`.
- [ ] No `DragDropEffects.Move` or `.All` anywhere (ADR-003). Drag handlers set `Copy`/`None` first, in `try/finally`.
- [ ] Image decoding never sniffs content: Magick reads with an explicit format from the extension and the security policy from WP-06 req. 4.
- [ ] Hook callbacks: no I/O, no logging, no locks, no allocation-heavy work; always `CallNextHookEx`; delegate kept in a field.
- [ ] External tools only via `ProcessRunner` (argument list, timeout, cancellation). No `UseShellExecute = true` for tools.
- [ ] Decoded/untrusted text (QR, file names) never executed or opened automatically.

## Contract
- [ ] Only files listed in the work package's "Owns" section changed (plus the WP's own `docs/STATUS.md` row, `docs/reviews/`, and the Acceptance table of the WP file).
- [ ] No change to `src/WheelConvert.Core/Contracts/`, `TargetCatalog.cs`, `Directory.Build.props` without an approved ADR.
- [ ] Converter: returns `Fail(...)` instead of throwing; only `OperationCanceledException` escapes; error codes per CONTRACTS C1 table.
- [ ] `CheckHealth()` is fast and never converts anything.
- [ ] Output labels via `OutputLabels` / `AppSettings.OutputLabel`, not hand-written strings.

## Windows / interop
- [ ] Every P/Invoke, struct and constant has a learn.microsoft.com URL comment and matches it (types, `SetLastError`, x64 pointer sizes, `CharSet`).
- [ ] UI work on the UI thread; worker events marshalled with `Dispatcher.BeginInvoke`.
- [ ] Physical pixels vs DIPs handled for mixed-DPI multi-monitor setups.
- [ ] Runs non-elevated (`asInvoker`).

## Quality
- [ ] **CI on the PR is green.** A red PR is never APPROVE.
- [ ] Builds with 0 warnings in Release (`dotnet build WheelConvert.sln -c Release`), as CI does.
- [ ] App tests pass: `dotnet test tests/WheelConvert.App.Tests -c Release`.
- [ ] Reverted-fix check: for each required fix or safety rule, revert it in a scratch run and confirm a test fails (or record why it can't be tested). Restore afterwards.
- [ ] Reviewer is a different model from the builder, in a fresh session (AGENTS §2).
- [ ] Core tests pass: `dotnet run --project tests/WheelConvert.Core.Tests`.
- [ ] Module's acceptance tests from its WP file: each marked PASS / FAIL / NEEDS USER.
- [ ] New packages: licence checked (ADR-005), version recorded (ADR-010).
- [ ] No dead code, no commented-out blocks, no TODOs without a WP reference.
- [ ] User-facing messages: plain language, ≤ 120 characters, no stack traces.
