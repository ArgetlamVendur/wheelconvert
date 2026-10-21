# Step 04 — Drag events and segment labels

**Builder:** Antigravity  **Reviewer:** Claude Code
**Branch:** `step-04-drag`
**Spec refs:** Section 4.3 (except Drop); AGENTS.md guardrails 1, 8

## Build
1. Wheel: `AllowDrop=True`. Handle `DragEnter`, `DragOver`, `DragLeave`.
2. **In every handler set `e.Effects = DragDropEffects.Copy` and `e.Handled = true`.** (Guardrail 1.)
3. `DragEnter`: read `DataFormats.FileDrop` → `string[]`. Centre label = first file's extension (e.g. `.png`). If no file data → label `Unsupported`.
4. `Wheel/SegmentMap.cs` — static lookup, placeholder only:
   - `.png .jpg .jpeg .heic .webp` → JPG, PNG, WebP, ICO
   - `.wav .flac .mp3 .m4a .ogg` → MP3, FLAC, WAV, OGG
   - `.pdf` → Split, Images, Rotate, QR
   - anything else → single segment `Unsupported`
5. `DragOver`: hit-test angle from centre → highlight the segment under the cursor; centre area (radius < 40 px) = no segment.
6. `DragLeave`: clear highlight (do not hide wheel).

## Do NOT
- Implement `Drop`. Do not change the hook.

## How to test
| # | Who | Steps | Expected |
|---|---|---|---|
| 1 | Agent | Build | 0 warnings |
| 2 | User | Shift-drag `photo.png` into the wheel | Centre `.png`; segments JPG/PNG/WebP/ICO |
| 3 | User | Same with `doc.pdf`, `notes.txt` | Correct labels / `Unsupported` |
| 4 | User | Move around the wheel | Segment under cursor highlights; centre highlights nothing |
| 5 | User | Watch the drag cursor while over the wheel | Shows **copy** cursor (+), never move |

## Reviewer focus (Claude Code)
Copy effect set in all three handlers; no `Drop` handler yet; hit-test math correct at all four quadrants.
