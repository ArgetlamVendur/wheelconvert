# WP-04: Wheel window and drag/drop

**Builder:** Claude Code · **Reviewer:** Antigravity · **Depends on:** WP-01 · **Risk:** high (user-file safety)

## Goal
A circular, always-on-top, **non-activating** window that appears at a point, shows the conversion options for the file being dragged over it, and reports which segment the file was dropped on.

## Owns
- `src/WheelConvert.App/Wheel/WheelWindow.xaml` + `.xaml.cs`
- `src/WheelConvert.App/Wheel/SegmentVisuals.cs` (new, optional)
- `src/WheelConvert.App/Interop/NativeMethods.Window.cs` (new)
- **Not** `IWheelView.cs` (contract between WP-04 and WP-11; change only with both WPs' agreement)

## Requirements
1. Implement `IWheelView` exactly.
2. **Non-activating:** in `SourceInitialized`, add `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` via `GetWindowLongPtr`/`SetWindowLongPtr(GWL_EXSTYLE)`. Explorer's title bar must stay active while the wheel shows.
3. **Hit area:** every pixel of the disc has alpha ≥ 1 (fully transparent pixels don't receive drops). Outside the disc can be transparent.
4. **Position:** `ShowAt(ScreenPoint)` receives physical pixels. Convert to DIPs **for the monitor under the point** (`PresentationSource.FromVisual(this).CompositionTarget.TransformFromDevice` after first show, or `GetDpiForMonitor`/`MonitorFromPoint`), then centre. Keep the disc fully on-screen (clamp to the monitor's work area).
5. **Drag handling** (CONTRACTS C4, ADR-003):
   - `DragEnter`: first statement `e.Effects = DragDropEffects.Copy`; read `DataFormats.FileDrop` as `string[]`; if none → show "Not a file", `e.Effects = None`. Else call `TargetsProvider(paths)` (all paths; CONTRACTS C4), draw segments, centre label = file extension (e.g. `.png`) or "+N files" for several.
   - No targets → centre text "Nothing available" and `e.Effects = None`.
   - `DragOver`: `e.Effects = Copy`; hit-test with **`WheelGeometry.HitTest`** (Core) using the mouse position relative to the centre; highlight the segment; centre hole = no segment.
   - `DragLeave`: clear highlight, keep the wheel open.
   - `Drop`: `e.Effects = Copy` first (in `try/finally`); resolve segment; if none → `e.Effects = None`, hide; else hide and raise `Dropped(paths, target)` **after** returning from the handler (`Dispatcher.BeginInvoke`) so Explorer's drag loop isn't blocked.
   - Set `e.Handled = true` in all four.
6. **Draw** segments as annular sectors with `WheelGeometry.GetSegmentArc` + `PointOnCircle`; labels from `ConversionTarget.Label`. Inner radius ≈ 40 DIP, outer ≈ 125 DIP. Light and dark readable (white text on dark translucent disc is fine).
7. **Dismissal.** The wheel never has keyboard focus, so it can't see Esc itself. That's fine: Esc during an Explorer drag cancels the drag (Explorer handles it), which raises `DragLeave`; the user then releases the mouse and the Shift-drag trigger raises `WheelDismissRequested`. In hotkey mode the wheel closes on a drop, on the hotkey again, or after 30 s with no drag activity over it (a `DispatcherTimer` reset by every drag event). **Don't add a keyboard hook here.**
8. Reuse one window instance. `HideWheel()` clears segments — **but** keep the segments and the drop target alive until any in-progress `Drop` or `DragLeave` has been handled: on mouse-up the trigger's dismiss can arrive *before* Explorer delivers `Drop` (WP-11 also defers the dismiss; see WP-11 req. 3). Track `_dragInProgress` (set in DragEnter, cleared after Drop/DragLeave); if `HideWheel()` is called while true, hide visually but let the pending `Drop` still resolve its segment.
9. Never open, read or stat dropped files; only use path strings.

## Gotchas
- `AllowsTransparency=True` + `WindowStyle=None` is required for a round window; it disables some hardware acceleration, which is fine at this size.
- `ShowActivated=False` alone is not enough; the extended style is what keeps focus away.
- Multi-monitor with different DPI: test moving between a 100 % and 150 % screen.
- WPF's `DragEventArgs.GetPosition(this)` is in DIPs relative to the window: subtract the centre (Width/2, Height/2).
- `Path` is ambiguous (implicit `using System.IO` vs `System.Windows.Shapes.Path`, CS0104). Use an alias: `using ShapePath = System.Windows.Shapes.Path;`. Same for `Brush`/`Timer` if WinForms types leak in (ADR-007).

## Acceptance tests
| # | Who | Steps | Expected | Result |
|---|---|---|---|---|
| 1 | Agent | Build App; grep shows no `DragDropEffects.Move` | ✔ | PASS (2026-10-22: solution 0 warnings, Debug + Release; no `DragDropEffects.Move`/`.All` in src/ or tests/) |
| 2 | User | Open the wheel (hotkey or Shift-drag), drag `photo.png` over it | Centre `.png`, segments shown, hovered one highlights | NEEDS USER (needs a trigger: WP-02 or WP-03) |
| 3 | User | Drag `notes.txt` over it | "Nothing available", no-drop cursor | NEEDS USER (needs a trigger: WP-02 or WP-03) |
| 4 | User | Watch the cursor over the wheel during Shift-drag | **Copy** cursor (+), never Move | NEEDS USER (needs a trigger: WP-02 or WP-03) |
| 5 | User | Drop on a segment | Wheel hides; log shows the drop; **photo.png still in place** | NEEDS USER (needs a trigger: WP-02 or WP-03) |
| 6 | User | Drop in the centre hole | Nothing happens, file in place | NEEDS USER (needs a trigger: WP-02 or WP-03) |
| 7 | User | Drag 3 files | "+3 files" label | NEEDS USER (needs a trigger: WP-02 or WP-03) |
| 8 | User | 150 % display scaling; second monitor if available | Wheel centred under cursor, fully on-screen at edges | NEEDS USER (needs a trigger: WP-02 or WP-03) |
| 9 | User | While the wheel shows, look at Explorer's title bar | Still active (not greyed) | NEEDS USER (needs a trigger: WP-02 or WP-03) |
| 10 | User | Shift-drag `photo.png`, **release the mouse on a segment** (the real gesture, not a separate drop) | Converted (after WP-06/11); the dismiss didn't swallow the drop | NEEDS USER (needs a trigger: WP-02 or WP-03) |
| 11 | User | Shift-drag from a USB drive and from a network share onto a segment (ADR-003) | Source still in place on both | NEEDS USER (needs a trigger: WP-02 or WP-03) |

## Review focus
Effects never Move on any path (incl. exceptions); DPI conversion; extended styles on x64 (`GetWindowLongPtr`); `Dropped` raised asynchronously; no file access.

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md (C4), docs/modules/WP-04-wheel-window.md, docs/decisions/ADR-003-copy-only-drop.md.
You are the builder for WP-04. Create branch wp-04-wheel from main. Restate the plan in 3–5 lines and wait for "go".
```
