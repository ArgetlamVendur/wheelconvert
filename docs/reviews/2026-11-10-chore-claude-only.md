# Review: PR #10 `chore-claude-only` (docs + CI only)

Date: 2026-11-10 · Reviewer: Claude Code (Fable), fresh session · Reviewed commit: `885ba08` against `main` `70e5815`

## Verdict: CHANGES NEEDED

One blocking finding (1): the WP-06 row in `docs/STATUS.md` says `TODO` while WP-06 is in review in PR #9. It is a one-line fix. Everything else asked for in this review passes.

## What was checked

| # | Check | Result | How |
|---|---|---|---|
| 1 | No safety rule weakened | PASS, with one note (finding 2) | Read the full diff |
| 2 | STATUS.md rows match reality | **FAIL** for WP-06 (finding 1); 02, 03, 04, 08, 11 correct | Compared with `gh pr list` |
| 3a | Marker step passes when `git grep` finds nothing | PASS | Ran the script; CI log |
| 3b | Marker step fails on a real marker | PASS | Ran the script on scratch files |
| 3c | App tests step uses `--no-build` after the Release build | PASS | Read; CI log |
| 4 | AGENTS.md under 12,000 characters | PASS: 6,998 | `wc -m` |
| 5 | CI on the PR is green | PASS | `gh pr checks` |

Only docs and `.github/workflows/build.yml` changed (12 files); no app code, no frozen contract file.

### Evidence, run

- **CI**: `gh pr checks chore-claude-only` shows `windows` and `core-linux` passing on both the push and the pull-request run. In the Windows log of run 42799411376: "No conflict markers.", "Safety grep passed (58 files).", App tests "Passed: 45, Failed: 0", PDF converter tests "Passed: 64, Failed: 0".
- **Marker step**: copied the step's script unchanged into a scratch clone outside the repo and ran it on files added with `git add`:

  | Case | Output | Exit code |
  |---|---|---|
  | Clean tree | "No conflict markers." | 0 |
  | Full conflict block, LF line endings | 3 error lines | 1 |
  | Full conflict block, CRLF line endings | 2 error lines (`<<<<<<<`, `>>>>>>>`) | 1 |
  | A lone `=======` line, CRLF | "No conflict markers." | 0 (see finding 4) |
  | A lone `=======` line, LF | 1 error line | 1 (see finding 4) |

  Limit of this test: this machine has only Windows PowerShell 5.1, not the `pwsh` 7 that GitHub runs. The passing case is confirmed on real `pwsh` by the green CI run. The failing case is confirmed on 5.1 only; the script uses nothing that differs between the two versions.
- **Reverted-fix check**: the failing cases above are that check for the marker step. For the App tests step it is not run: proving the step goes red would need a deliberately failing test pushed to the PR. The log shows the step ran all 45 tests, so it is not a silent no-op.

### Evidence, read only

- **Why the marker step is correct**: `git grep` exits 1 when nothing matches. PowerShell does not stop on a non-zero exit from a program, so the script carries on, and the explicit `exit 0` stops GitHub from picking up the leftover exit code 1. Without that `exit 0` the step would fail on a clean tree.
- **App tests step**: `WheelConvert.App.Tests` is in `WheelConvert.sln` (line 26), so the earlier `dotnet build WheelConvert.sln -c Release` has built it and `--no-build` is valid.
- **Safety rules**: AGENTS.md §4 (hard guardrails) is untouched. The Safety, Contract and Windows sections of `docs/checklists/review.md` are untouched. Changes that tighten: "your own STATUS.md row" instead of all of STATUS.md; Release build of the whole solution instead of "every project you touched"; App tests and marker check before REVIEW; never APPROVE on red CI; reverted-fix check; WP-12 licence inventory must come from the packages' own notice files.

## Findings

1. **Blocking. `docs/STATUS.md:12`: WP-06 is shown as `TODO` with no PR, but it is open as PR #9.**
   Why: AGENTS.md §2 step 1 tells an agent to claim any WP that is `TODO`. A new session could start WP-06 a second time. The row is also edited on the `wp-06-image` branch (`REVIEW | Antigravity | Claude Code | wp-06-image`), so the two PRs will conflict on this line whichever merges second.
   Fix: set State to `REVIEW` and Branch / PR to `PR #9`. Keep the new Builder and Reviewer text. When the conflict appears in the second PR, keep `REVIEW`, `PR #9`, and the newer Notes from `wp-06-image`.

2. **Not blocking, needs the user's confirmation. `AGENTS.md:30`, `docs/STATUS.md:20`, `docs/modules/README.md:20`: reviewer independence is reduced.**
   Why: the old rule was "a different model family from the builder" (Claude vs. Antigravity). The new rule is "a different model", and Opus and Fable are the same family, so they are more likely to share blind spots. The PR adds compensating rules (fresh session, green CI, reverted-fix check). This is the stated purpose of the PR, so it is recorded here as a decision, not a defect.
   Fix: none if this is intended. Optional: a short ADR recording the decision and why.

3. **Not blocking. `AGENTS.md:63` and `.github/workflows/build.yml:47` use different patterns.**
   Why: AGENTS.md §5 gives `'^(<<<<<<<|=======|>>>>>>>)'`; CI adds `( |$)` on the end. The AGENTS.md version also matches any line that starts with seven or more `=`, such as an underlined Markdown heading, so a local check could fail where CI passes. Both print nothing on the current tree (run).
   Fix: use the CI pattern in AGENTS.md §5.

4. **Not blocking. `.github/workflows/build.yml:47`: the `=======` part of the pattern behaves differently with line endings.**
   Why: with CRLF line endings a lone `=======` line is not matched; with LF line endings a line of exactly seven `=` is matched, even if it is a legitimate Markdown heading underline. Real conflicts are still caught in both cases because the `<<<<<<< ` and `>>>>>>> ` lines always match.
   Fix: either accept as is, or drop `=======` from the pattern and rely on the other two markers.

5. **Not blocking. PR #5 ("chore: CI guards (conflict markers, App tests)") is still open and changes the same two things in `build.yml` and `AGENTS.md`.**
   Why: this PR supersedes it; merging both will conflict.
   Fix: close PR #5 without merging once this PR is merged.

6. **Not blocking. `docs/checklists/review.md:15` was not updated to match `AGENTS.md:26`.**
   Why: AGENTS.md now also lets a builder edit the Acceptance table of its own WP file; the checklist line still lists only `docs/STATUS.md` and `docs/reviews/`. A reviewer following the checklist literally would flag a filled-in acceptance table.
   Fix: add "the Acceptance table of the WP file" to the checklist line.

7. **Not blocking. `AGENTS.md:30` says reviews are named `docs/reviews/WP-NN-<date>.md`.**
   Why: a chore PR has no WP number; this file uses the date-first name the user asked for, as the two 2026-10-20 reviews do.
   Fix: optional: add "(chore PRs: `<date>-<branch>.md`)".

## Not checked

- PR #9 (WP-06) itself. Its CI checks show no result in `gh pr view 9`; that is for the WP-06 review.
- The test counts quoted in the Notes column for merged WPs (for example "36 App tests" for WP-03; the suite now has 45). These were treated as history.
