# Morning checklist — 2026-11-19

Every user ("NEEDS USER") test from the overnight run, in order. Nothing was merged overnight (see the run log: the `main` ruleset needs your approving review on each PR). So every test below runs on a **PR branch**, not on an installed release.

**WP-14 test 9 (the 15-hit PDF wheel test) is not here:** WP-14 was not built, because it needs WP-13 merged first.

## Before you start (once)
1. Right-click the WheelConvert tray icon → **Quit**. Only one copy can run at a time.
2. Open PowerShell in `D:\Claude Base\Cowork Playground\APPS\wheelconvert`.
3. To run a branch, use `git switch <branch>` then `dotnet run --project src/WheelConvert.App -c Release`. The tray icon appears. Close it from the tray (**Quit**) before you switch branch.
4. Use only files in `D:\Claude Base\Cowork Playground\APPS\wc-test\`.

## Merge order, once you're happy
Merge **#22 first** (docs only: ADR-011 and ADR-013 Accepted). #24 and #25 are stacked on it. Then merge #23, #24 and #25 in any order. On each PR: **Files changed → Review changes → Approve**, then **Squash and merge**. Merge a PR only if its review file in `docs/reviews/` says APPROVE. Review status at handoff:
- #23 (WP-22): APPROVE.
- #24 (WP-13): APPROVE.
- #25 (WP-21): APPROVE (arrived 07:30).

---

## WP-21 — FFmpeg hardening (PR #25, branch `wp-21-ffmpeg-hardening`). Touches user files: run first.
**Test 9: normal conversions still work.**
1. `git switch wp-21-ffmpeg-hardening`, then `dotnet run --project src/WheelConvert.App -c Release`.
2. Copy a phone video (`.mp4` or `.mov`) and an `.mp3` into `wc-test`.
3. In File Explorer, open `wc-test`, hold **Shift**, drag the video a little, and drop it on **MP4** (or **GIF**) on the wheel.
   - Expected: a new file appears next to it, named like before (for example `clip (converted).mp4`). The original is unchanged.
4. Do the same with the MP3 → **WAV**. Expected: the same as before this change.
5. Extra check (optional): rename any `.txt` file to `fake.mp4` and drop it on MP4.
   - Expected: a short "couldn't convert" message.
   - Open `%LOCALAPPDATA%\WheelConvert\wheelconvert.log`. The last lines show FFmpeg's error text, with **file names only, no full paths**.

## WP-13 — Multi-input core (PR #24, branch `wp-13-core-multi-input`)
**Test 9: the single-file wheels look the same.**
1. `git switch wp-13-core-multi-input`, then `dotnet run --project src/WheelConvert.App -c Release`.
2. Shift-drag one image, then one PDF, then one video from `wc-test`.
   - Expected: the same segments as on the current release. There are no new segments yet, because the converters for them come in later WPs.

**Test 10: two PDFs.**
1. Select two PDFs in `wc-test`, hold Shift and drag both.
   - Expected: only **Split**, **Images** and **Rotate**. Reorder, Split half, Odd pages, Even pages and Reverse are gone.
2. Drop on **Rotate**. Expected: both PDFs are rotated, as two separate output files.

## WP-22 — Release hardening (PR #23, branch `wp-22-release-hardening`). No app change to try. These two are GitHub settings.
**Test 5: create the `release` environment.** Do this before the next tag.
1. On GitHub, open the repo → **Settings → Environments → New environment**, name it `release`.
2. Tick **Required reviewers** and add yourself.
3. Under **Deployment branches and tags**, choose **Selected** and add a tag rule `v*`. Save.
   - Expected: the environment is listed with 1 reviewer.
   - Until you do this, the approval gate does nothing: GitHub would create an empty `release` environment on the first run.

**Test 6: on the next tag only.** No need to do this today.
1. Push the next tag (for example `v0.1.0-rc2`).
   - Expected: the release run pauses and waits for your approval.
2. Approve it.
   - Expected: a draft release appears. In the installed exe, **Properties → Details** shows the tag's version.

---

## Also found overnight
- **Flaky test:** `HotkeyParserTests.HotkeyTrigger_StartAndDispose_AllowsSubsequentRegistrationOnSameHotkey` timed out once on CI (5 s STA timeout) on a docs-only PR, then passed on re-run. Worth a follow-up WP.
- **Branch `docs-rc1-results`** (your rc1 Defender/VirusTotal notes, commit 6fe7df2) is pushed but has **no PR**. Open one if you want it on `main`.
- WP-22's ProductVersion carries a `+<commit>` suffix in the exe's file properties. The tray hides it.
