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
| 09 | PDF reorder window | TODO | Sonnet | Fable | | Can start with a fake renderer |
| 10 | QR reader | IN PROGRESS (Sonnet) | Sonnet | Fable | wp-10-qr | |
| 11 | Integration | DONE | Claude Code (Opus) | Antigravity | PR #2 (b234417) | Merged |
| 12 | Packaging + release | TODO | Sonnet | Fable | | Includes Defender go/no-go |

From 2026-11-10 only Claude models are used: the reviewer must be a different model from the builder, in a fresh session (AGENTS.md §2). From 2026-11-12 the remaining work runs as an orchestrated run (AGENTS.md §9).

## Not yet verified anywhere
- Shift-drag hook vs. antivirus: unknown until WP-12's go/no-go.

## Open questions for the user
- Copyright holder name for `LICENSE` (WP-12).
- Make the GitHub repo public now or at the first release?
- Default trigger mode: Shift-drag (current; hotkey off unless the hook fails) or Both? WP-05's tray menu lets you switch either way.
- Strip GPS location from converted photos (WP-06 follow-up)? On by default?
