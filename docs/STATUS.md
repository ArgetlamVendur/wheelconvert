# STATUS

One row per work package. **Update your row in the same PR as the work.** States: `TODO` → `IN PROGRESS (<tool>)` → `REVIEW` → `DONE` (user merged) · or `BLOCKED: <reason>`.

| WP | Module | State | Builder | Reviewer | Branch / PR | Notes |
|---|---|---|---|---|---|---|
| 01 | Core | DONE | base | Claude ×2 (independent) | — | Built + 64 tests pass on Linux; reviews: docs/reviews/2026-10-20-cp1-core.md, -cp2-final-audit.md |
| 02 | Shift-drag trigger | DONE | Claude Code (Opus) | Antigravity | PR #8 | Merged. 9 GestureStateMachine tests; Explorer tests passed. In Shift-drag mode the hotkey is off unless the hook fails |
| 03 | Hotkey trigger | DONE | Antigravity | Claude Code | PR #6 | Merged. 36 App tests (incl. STA RegisterHotKey); message-only window |
| 04 | Wheel window | DONE | Claude Code (Opus) | Antigravity | PR #4 | Merged. File-safety critical; user wheel tests passed |
| 05 | Tray + settings | DONE | Claude Code (Opus) | Claude Code (Fable) | PR #11 | Merged. 87 App tests; user tray tests passed (notifications need Do not disturb off) |
| 06 | Image converter | DONE | Antigravity; fixes Claude Code (Opus), last fix Fable | Claude Code (Sonnet ×3, Fable ×1) | PR #9 | Merged. Re-review 3 APPROVE (docs/reviews/WP-06-2026-11-12-rereview3.md); 54 xUnit tests pass (Release). Magick.NET pinned 15.0.0. User tests passed at Checkpoint 1 (2026-11-13) |
| 07 | Media converter | DONE | Claude Code (Opus); review fixes Sonnet | Claude Code (Fable) | PR #12 | Merged. Re-review 3 APPROVE (docs/reviews/WP-07-2026-11-12.md); 123/123 Media tests pass with real FFmpeg 9.0.2. User tests 6–8 passed at Checkpoint 1 (2026-11-13). Follow-ups: stderr logging (needs ADR), input-format restriction, review note N7 |
| 08 | PDF operations | DONE | Claude Code (Opus) | Antigravity | PR #7 | Merged. PDFsharp pinned 6.2.4: 6.5.0+ is AGPL |
| 09 | PDF reorder window | DONE | Sonnet | Opus (Fable first review) | PR #16 | Merged. Re-review APPROVE (docs/reviews/WP-09-2026-11-13.md); 27 PageOrderTests (6 STA window tests). User tests passed at Checkpoint 2 (2026-11-17) |
| 10 | QR reader | SHELVED (user, 2026-11-17) | Sonnet | — | wp-10-qr (draft PR #17) | Built at 0fc6443, not reviewed; QR stub stays on main so no QR segment shows |
| 11 | Integration | DONE | Claude Code (Opus) | Antigravity | PR #2 (b234417) | Merged |
| 12 | Packaging + release | DONE | Sonnet | Opus | PR #19 | Merged. Reviews: docs/reviews/WP-12-2026-11-17.md (APPROVE after 2 fix rounds). Checkpoint 3 user tests PASS 2026-11-17 (Defender clean, VirusTotal skipped); GO recorded by user. Tag `v0.1.0-rc1` pushed by the user 2026-11-18; draft prerelease check and publishing pending |
| 13 | Core: multi-input targets | DONE | Sonnet | Opus | PR #24 | Merged. APPROVE (docs/reviews/WP-13-2026-11-19.md); 9 rules revert-checked. `PdfConverterTests` `notYet` list: WP-14/18/20 remove their ID. `DropCoordinator` interaction path is PDF-only: WP-19 must add the resize branch. User tests 9-10 NEEDS USER |
| 14 | PDF merge | DONE | Sonnet | Opus | PR #28 | Merged. APPROVE (docs/reviews/WP-14-2026-11-19.md); agent tests 1-6 PASS (Pdf 75). User tests 7-9 NEEDS USER (incl. the 10-segment wheel check). Follow-ups: README note (bookmarks/metadata dropped); locked input reports an output error; no test for per-page cancel / zero-page input |
| 15 | Images → PDF | TODO | — | — | — | Image project; before WP-16 |
| 16 | Smaller (images, video) | TODO | — | — | — | Image + Media; after WP-21; before WP-19 |
| 17 | One-click FFmpeg install | TODO (needs ADR-012 Accepted after the by-hand winget check) | — | — | — | After WP-21; independent of WP-13 |
| 18 | PDF → Word (text only) | TODO | — | — | — | New Docx project |
| 19 | Image resize | TODO | — | — | — | Image + App dialog |
| 20 | Smaller PDF | TODO | — | — | — | Pdf project; no Ghostscript; direct SkiaSharp pin |
| 21 | FFmpeg hardening + error log | DONE | Sonnet | Opus | PR #25 | Merged. APPROVE (docs/reviews/WP-21-2026-11-19.md). Test 9 PASS (user, 2026-11-19). Log follow-ups (path fragment after truncation; three loggers on one file): fixed in PR #29 (APPROVE, docs/reviews/chore-log-followups-2026-11-19.md). HLS test passes even without the whitelist on FFmpeg 9.0.2 (ffconcat test covers it) |
| 22 | Release hardening + version from tag | DONE | Sonnet | Opus | PR #23 | Merged. APPROVE (docs/reviews/WP-22-2026-11-19.md). Follow-ups: no test for the job queue exit timeout; acceptance row 4 not testable on .NET 8 SDK; ProductVersion carries `+commit` |

From 2026-11-10 only Claude models are used: the reviewer must be a different model from the builder, in a fresh session (AGENTS.md §2). From 2026-11-12 the remaining work runs as an orchestrated run (AGENTS.md §9).
From 2026-11-17 Opus replaces Fable as orchestrator and reviewer.

## Not yet verified anywhere
- Shift-drag hook vs. antivirus: **GO** on Microsoft Defender (2026-11-17, see docs/GO-NO-GO.md). VirusTotal not yet checked; the user decides before publishing `v0.1.0-rc1`.
- PDF wheel with 10 segments (one PDF, after v0.2): hand test in WP-14.

## Open questions for the user
- OCR for scanned PDFs: deferred (Windows-only APIs).
- Default trigger mode: Shift-drag (current; hotkey off unless the hook fails) or Both? WP-05's tray menu lets you switch either way.
- Strip GPS location from converted photos (WP-06 follow-up)? On by default?
- Licence: get professional advice on the LGPL/MPL libraries inside Magick.Native (now shipped as a separate DLL next to the exe).
- Licence: are SkiaSharp's libmicrohttpd and gif compiled into the Windows libSkiaSharp.dll? (ask the SkiaSharp maintainers)
