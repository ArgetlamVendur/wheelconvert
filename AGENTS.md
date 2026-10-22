# AGENTS.md — WheelConvert (rules for ALL coding agents)

Single source of truth for Claude Code, Google Antigravity and any other agent. Claude Code reads it via `CLAUDE.md`; Antigravity reads it directly. **Never create `GEMINI.md`** (it would override this file for Antigravity only). Keep this file under 12,000 characters.

---

## 1. The project
WheelConvert is a Windows tray app. Shift-drag a file in Explorer (or press a hotkey) → a wheel appears at the cursor → drop the file on a segment → it's converted next to the original. Images, audio/video (external FFmpeg), PDF tools, QR reading. All local. MIT, public GitHub release.

The repo is a **base of independent modules**. Each module is a work package (WP) in `docs/modules/`. You implement or fix **one WP at a time**.

Read before coding (in this order, every session):
1. `docs/STATUS.md` — what's done, what's claimed, what's next.
2. Your WP file in `docs/modules/`.
3. `docs/CONTRACTS.md` — the rules every module follows.
4. Any ADR your WP links to (`docs/decisions/`).

You have no memory of earlier sessions or of other agents. The repo is the only shared state.

---

## 2. Claiming and finishing a WP
1. In `docs/STATUS.md`, check the WP is `TODO` and all its dependencies are `DONE` (or explicitly marked "can start with stubs").
2. Create branch `wp-NN-<short-name>` from the latest `main`. Set the WP to `IN PROGRESS (<your tool>)` in STATUS.md, commit that first.
3. Restate to the user in 3–5 lines what you will build and which files you will touch. Wait for "go".
4. Build only what the WP says. Touch only files in its **Owns** list (plus STATUS.md and `docs/reviews/`). **Shared, append-only files** any WP may add to (never edit others' lines; expect merge conflicts and resolve by keeping both): `WheelConvert.sln` (add your test project) and `docs/decisions/ADR-010-package-versions.md` (fill your row).
5. Run the checks in §5. Fill the acceptance table in your own WP file (docs/modules/WP-NN-*.md, only that table) with PASS / FAIL / NEEDS USER.
6. Set the WP to `REVIEW` in STATUS.md, push, open a PR titled `WP-NN: <name>`. Stop.

Reviewing: use `docs/checklists/review.md`. Write the result to `docs/reviews/WP-NN-<date>.md` as **APPROVE** or **CHANGES NEEDED** with numbered findings (file:line, why, fix). Don't rewrite the author's code; real problems go back to the author. The reviewer must not be the same tool that built the WP.

If you can't finish: set the WP to `BLOCKED: <reason>` in STATUS.md, commit what compiles, explain exactly where you stopped. Never leave the build broken on `main`.

---

## 3. Stack
- .NET 8. `WheelConvert.Core` and converters: `net8.0`, cross-platform. `WheelConvert.App`: `net8.0-windows`, WPF (+ WinForms for the tray icon only, ADR-007).
- Build everything: `dotnet build WheelConvert.sln` (App needs Windows).
- Core tests (any OS): `dotnet run --project tests/WheelConvert.Core.Tests` — exit code 0 = pass.
- CI: `.github/workflows/build.yml` (Windows build + publish, Linux Core build).

---

## 4. Hard guardrails (violation = PR rejected)
1. **Never change the frozen contracts** without user approval + a new ADR: `src/WheelConvert.Core/Contracts/*`, `TargetCatalog.cs`, `Directory.Build.props`, `docs/CONTRACTS.md`, `src/WheelConvert.App/Wheel/IWheelView.cs`, and the constructors called in `CompositionRoot.cs`. If your WP seems to need a change, stop and write the proposal in your PR description.
2. **User files are sacred** (CONTRACTS C2, ADR-002): sources are read-only; output only via `IOutputWriter` (CreateTempPath → Commit, Discard on failure). No other `File.Delete/Move/Copy(overwrite)` in **any folder the user's files live in**. (The app's own `%LOCALAPPDATA%\WheelConvert\` folder and the HKCU Run key are exempt.)
3. **Drop effect is always Copy** (ADR-003). `DragDropEffects.Move` / `.All` must not appear in code (CI fails if they do).
4. **Hook callbacks never block** — no I/O, logging or locks; hand off with `Dispatcher.BeginInvoke`; always `CallNextHookEx`; keep the delegate in a field.
5. **P/Invoke only in `src/WheelConvert.App/Interop/`**, each with its learn.microsoft.com URL. No guessed signatures or constants.
6. **External tools only via `ProcessRunner`** (no shell, timeout, cancellation). Never `UseShellExecute = true` for tools.
7. **Never elevated**: `app.manifest` stays `asInvoker`.
8. **Licences** (ADR-005): no AGPL/GPL libraries. New package → licence check + ADR-010 entry in the same PR.
9. **No scope creep**: no features, packages, settings or files outside your WP. Ideas go in the PR description under "Follow-ups".
10. **Untrusted text** (QR content, file names) is never executed, opened or used in a shell.
11. **Test with scratch files only** (`D:\Claude Base\Cowork Playground\APPS\wc-test\`) until WP-11 is approved.
12. **If something is impossible or the spec is wrong, say so.** Never silently work around a rule.

---

## 5. Before you mark a WP as REVIEW
- `dotnet build` for every project you touched: **0 warnings, 0 errors** (warnings are errors).
- Core tests pass.
- Your WP's own tests pass (converters: add a test project `tests/WheelConvert.Converters.<Name>.Tests` with xUnit if you need packages).
- Self-review against `docs/checklists/review.md`.
- STATUS.md updated.

---

## 6. Code conventions
- One public type per file (small related contracts may share a file, as in `AppServices.cs`).
- Nullable on; no `!` suppressions without a comment why.
- Async all the way; `ConfigureAwait(false)` in Core and converters; never `.Result`/`.Wait()` except at app exit.
- Comments explain *why*. No commented-out code. TODOs must reference a WP: `// TODO(WP-07): …`.
- User-facing text: plain English, ≤ 120 characters, no stack traces.
- Commit messages: `wp-NN: <imperative summary>`.

---

## 7. Git
- `main` always builds. Nobody commits directly to `main`; the user merges PRs.
- One branch per WP. Only one agent on a branch at a time.
- Rebase on `main` before opening the PR if `main` moved.

---

## 8. Talking to the user
- The user is not a C# developer and relies on agents for implementation. Give exact commands and expected results ("open File Explorer, hold Shift, drag photo.png…").
- Lead with what is broken, risky or untested. No praise of your own work.
- Mark clearly what you verified by running vs. by reading only.
