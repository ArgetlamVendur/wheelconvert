# WP-09: PDF reorder window

**Builder:** Antigravity · **Reviewer:** Claude Code · **Depends on:** WP-01; WP-08's `IPdfPageRenderer` (can start against a fake renderer) · **Risk:** low

## Goal
When the user drops a PDF on "Reorder", show a window of page thumbnails they can drag into a new order (or remove), then return the order.

## Owns
- `src/WheelConvert.App/Pdf/PageOrderPrompt.cs`
- `src/WheelConvert.App/Pdf/PageOrderWindow.xaml` + `.xaml.cs` (new)
- `src/WheelConvert.App/Pdf/PageOrderViewModel.cs` (new; pure list logic)

## Requirements
0. **Constructor is frozen:** `PageOrderPrompt(IPdfPageRenderer, ILog)` (already called by `CompositionRoot`).
1. `PageOrderPrompt.PromptAsync(path, ct)` shows `PageOrderWindow` **modally on the UI thread** and returns the 1-based order, or `null` on Cancel/close.
2. Window: title "Reorder pages — <file name>"; a wrap panel of thumbnails (≈ 160 px), page number under each; reorder **within the window** by mouse drag implemented with mouse capture (`MouseDown` → `CaptureMouse` → `MouseMove` → `MouseUp`), **not** OLE drag-and-drop, so no `DragDropEffects` are involved at all; also Ctrl+←/→ moves the selected page for keyboard users; Delete key or an ✕ button removes a page; buttons: Reverse, Reset, Cancel, Save.
3. Thumbnails load lazily and asynchronously via `IPdfPageRenderer.RenderPagePngAsync(path, i, 320, ct)`; show a placeholder until ready. Cancel loading when the window closes.
4. `PageOrderViewModel`: `Move(from, to)`, `Remove(index)`, `Reverse()`, `Reset()`, `Result` (1-based list). Save disabled when the list is empty.
5. Page count via `await GetPageCountAsync(path, ct)` (never on the UI thread synchronously). Errors from the renderer: `PdfPasswordProtectedException` → message "This PDF is password-protected." and return `null`; `InvalidDataException` → "This file can't be read as a PDF."; other → log + generic message.
6. More than 300 pages → show a message "Too many pages to reorder here (N)." and return `null` (v1 limit; virtualisation is a follow-up).

## Acceptance tests
| # | Who | Steps | Expected |
|---|---|---|---|
| 1 | Agent | Build App | 0 warnings |
| 2 | Agent | View-model operations (small test or documented manual check) | Correct `Result` |
| 3 | User | Drop a 5-page PDF on Reorder | Window with 5 thumbnails |
| 4 | User | Drag page 5 to the front, remove page 2, Save | `doc (reordered).pdf` = 5,1,3,4 |
| 5 | User | Cancel | No output |
| 6 | User | Password-protected PDF | Message, no output |

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md (C6), docs/modules/WP-09-pdf-reorder-window.md.
You are the builder for WP-09. Create branch wp-09-reorder from main. Restate the plan in 3–5 lines and wait for "go".
```
