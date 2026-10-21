# Step 05 — Drop handling (Copy-only)

**Builder:** Claude Code  **Reviewer:** Antigravity
**Branch:** `step-05-drop`
**Spec refs:** Section 4.3; AGENTS.md guardrails 1, 9 — **highest-risk step**

## Build
1. `Drop` handler: set `e.Effects = DragDropEffects.Copy` **first line**, then read all paths, determine segment under the drop point.
2. Log one line per drop: `drop segment=<name> files=<count> paths=<p1>|<p2>|...`
3. If dropped in the centre or on `Unsupported`: log `drop cancelled` and set `e.Effects = DragDropEffects.None`.
4. Hide the wheel after drop.
5. Do not open, read, copy, move, or write the dropped files. Only their paths.
6. Add `docs/TESTING.md` with the scratch-folder setup: create `C:\wc-test\` with `photo.png`, `song.wav`, `doc.pdf`, `notes.txt`.

## How to test
| # | Who | Steps | Expected |
|---|---|---|---|
| 1 | Agent | Build | 0 warnings |
| 2 | User | Shift-drag `C:\wc-test\photo.png` onto "WebP" | Log line with `segment=WebP`; **photo.png still in C:\wc-test** |
| 3 | User | Same from the desktop | File still on desktop |
| 4 | User | Select 3 files, Shift-drag together onto a segment | `files=3`, all paths logged, all 3 still in place |
| 5 | User | Drop on centre | `drop cancelled`, nothing moved |
| 6 | User | Drop a file from a **different drive** (USB) | File still on USB |

## Reviewer focus (Antigravity)
`e.Effects` never ends as `Move` on any code path (including exceptions — wrap in try/finally). No file I/O on dropped paths.
