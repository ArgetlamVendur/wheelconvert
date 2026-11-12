# Review: chore-orchestrated-run (PR #14) - 2026-11-12

**Verdict: APPROVE**

Commit reviewed: `dcc2d6536c4d4a81afd951a964fcdd50143ad894` (also the PR head per `gh pr view`).
Reviewer: Sonnet sub-agent, fresh session, no build context. Last commit author is the repo user; the change was docs only.

## CI (seen on this commit)
- windows: pass (2m0s), both runs (push and PR)
- core-linux: pass (46s / 38s), both runs

## Checked by running
- `git diff origin/main...HEAD --stat`: only `AGENTS.md` (+10) and `docs/STATUS.md` (+5/-5) changed. No code.
- `wc -m AGENTS.md`: 7859 characters (limit 12,000).
- Conflict-marker grep (CI pattern): prints nothing.
- `git log origin/main`: `0dd3aea WP-05: Tray + settings (#11)` is on main, so WP-05 = DONE with PR #11 is correct.
- STATUS.md table: one row per WP (01-12 as before), same column count, well-formed. WP-09, 10, 12 now Sonnet / Fable. Only those rows and WP-05 changed, plus the trailing sentence.
- Runbook `APPS\2026-11-12-runbook.md` exists; its merge rules match §9's summary (CI green, APPROVE on latest commit, checkpoint PASS).

## Checked by reading only
- §9 wording against §2, §4, §7. No §4 guardrail is touched or weakened. The "never publishes" line is consistent with §4 and the runbook "Never" list.
- Reverted-fix check (AGENTS §2): not applicable, no code or safety rule changed.

## Findings (all non-blocking)
1. `AGENTS.md` §9, line "In §2 step 3, the orchestrator's dispatch stands in for the user's go": this is a real relaxation of §2 step 3, and not in the PR's stated list. It is reasonable for an authorised run, and it is visible, but the PR description should say so. No fix needed.
2. `AGENTS.md` §9, last two bullets: the "user merges PRs" exception to §7 is stated explicitly, which is good. Suggest adding "Everything in §4 still applies" to §9 so no sub-agent reads §9 as relaxing guardrails. Optional.
3. `AGENTS.md` §9 header: the rules apply only "when the user authorises one", but the authorisation lives in a file outside the repo (`APPS\2026-11-12-runbook.md`). A reader of the repo alone cannot check it. Acceptable (the user owns that file); noting it only.
4. `docs/STATUS.md` WP-05 note says "user tray tests not recorded here": accurate and honest, no change needed.

No blocking findings.
