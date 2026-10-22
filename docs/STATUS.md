# STATUS

One row per work package. **Update your row in the same PR as the work.** States: `TODO` → `IN PROGRESS (<tool>)` → `REVIEW` → `DONE` (user merged) · or `BLOCKED: <reason>`.

| WP | Module | State | Builder | Reviewer | Branch / PR | Notes |
|---|---|---|---|---|---|---|
| 01 | Core | DONE | base | Claude ×2 (independent) | — | Built + 64 tests pass on Linux; reviews: docs/reviews/2026-10-20-cp1-core.md, -cp2-final-audit.md |
| 02 | Shift-drag trigger | TODO | Claude Code | Antigravity | | Highest risk; hotkey is the fallback |
| 03 | Hotkey trigger | TODO | Antigravity | Claude Code | | |
| 04 | Wheel window | TODO | Antigravity | Claude Code | | File-safety critical |
| 05 | Tray + settings | TODO | Antigravity | Claude Code | | |
| 06 | Image converter | TODO | Antigravity | Claude Code | | First real conversion |
| 07 | Media converter | TODO | Claude Code | Antigravity | | Needs FFmpeg for tests |
| 08 | PDF operations | TODO | Claude Code | Antigravity | | |
| 09 | PDF reorder window | TODO | Antigravity | Claude Code | | Can start with a fake renderer |
| 10 | QR reader | TODO | Antigravity | Claude Code | | |
| 11 | Integration | IN PROGRESS (Claude Code) | Claude Code | Antigravity | | Start first: makes the app runnable |
| 12 | Packaging + release | TODO | Claude Code | User | | Includes Defender go/no-go |

## Not yet verified anywhere
- `WheelConvert.App` has **never been compiled** (written without Windows). First CI run will likely need small fixes: that's WP-11's first task.
- Shift-drag hook vs. antivirus: unknown until WP-12's go/no-go.

## Open questions for the user
- Copyright holder name for `LICENSE` (WP-12).
- Make the GitHub repo public now or at the first release?
