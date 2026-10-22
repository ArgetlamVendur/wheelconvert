# STATUS

One row per work package. **Update your row in the same PR as the work.** States: `TODO` → `IN PROGRESS (<tool>)` → `REVIEW` → `DONE` (user merged) · or `BLOCKED: <reason>`.

| WP | Module | State | Builder | Reviewer | Branch / PR | Notes |
|---|---|---|---|---|---|---|
| 01 | Core | DONE | base | Claude ×2 (independent) | — | Built + 64 tests pass on Linux; reviews: docs/reviews/2026-10-20-cp1-core.md, -cp2-final-audit.md |
| 02 | Shift-drag trigger | TODO | Claude Code | Antigravity | | Highest risk; hotkey is the fallback |
| 03 | Hotkey trigger | DONE | Antigravity | Claude Code | PR #6 | Merged. 36 App tests (incl. STA RegisterHotKey); message-only window |
| 04 | Wheel window | TODO | Claude Code | Antigravity | | File-safety critical |
| 05 | Tray + settings | TODO | Antigravity | Claude Code | | |
| 06 | Image converter | TODO | Antigravity | Claude Code | | First real conversion |
| 07 | Media converter | TODO | Claude Code | Antigravity | | Needs FFmpeg for tests |
| 08 | PDF operations | REVIEW | Claude Code | Antigravity | wp-08-pdf | 64 xUnit tests pass (CI Windows + Linux). PDFsharp pinned 6.2.4: 6.5.0+ is AGPL |
| 09 | PDF reorder window | TODO | Antigravity | Claude Code | | Can start with a fake renderer |
| 10 | QR reader | TODO | Antigravity | Claude Code | | |
| 11 | Integration | DONE | Claude Code | Antigravity | PR #2 (b234417) | Merged. Acceptance needs WP-02/03/04/05/06 for user tests |
| 12 | Packaging + release | TODO | Antigravity | Claude Code | | Includes Defender go/no-go |

Reviewer must be a different model family from the builder.

## Not yet verified anywhere
- Shift-drag hook vs. antivirus: unknown until WP-12's go/no-go.

## Open questions for the user
- Copyright holder name for `LICENSE` (WP-12).
- Make the GitHub repo public now or at the first release?
