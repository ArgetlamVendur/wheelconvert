# Step 06 — Hardening and acceptance tests

**Builder:** Claude Code  **Reviewer:** User
**Branch:** `step-06-hardening`
**Spec refs:** Sections 6, 8, 9

## Build
1. Fix anything open in HANDOFF.md from Steps 01–05.
2. Hook resilience: every 30 s check that the hook still fires (track last-event time); if Windows dropped it, reinstall and log `hook reinstalled`.
3. Publish a single-file build: `dotnet publish src/WheelConvert -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true`.
4. Write `README.md`: what works, what doesn't, Defender result, how to run, link to spec. Placeholder for a GIF.
5. Write `docs/GO-NO-GO.md`: one paragraph — gesture or hotkey fallback for Phase 1, with the evidence.

## Acceptance tests (copy results into HANDOFF)
Run all 10 tests from Section 6 of the spec. Record PASS/FAIL + note for each.
Test 9 (Defender): right-click the published `.exe` → *Scan with Microsoft Defender*; also upload to virustotal.com and record the detection count.

## Go/no-go rule
If test 1, 2, 5 or 9 fails and can't be fixed → recommend the hotkey fallback (Section 8).

## Done when
User has run all tests, HANDOFF has the results table, GO-NO-GO.md is written, user merges and tags `v0.0.1-spike`.
