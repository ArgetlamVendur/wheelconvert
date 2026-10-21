# Phase 0 Spike Spec — Shift-Drag Conversion Wheel (Windows)

Working name: **WheelConvert** (placeholder, rename later)
Goal of this phase: **prove the gesture works.** No real file conversion yet.
Time box: 2–4 days. If the acceptance tests fail after that, switch to the fallback (Section 8).

---

## 1. Instructions for the AI coding agent (paste this block first)

> You are building a throwaway technical spike, not the product. Follow this spec exactly.
> - Do NOT add file conversion, settings, installers, tray menus, or UI polish.
> - Work in the numbered steps in Section 5. After each step, stop, tell me how to test it, and wait for me to confirm before continuing.
> - Use only the stack in Section 3. Do not add NuGet packages beyond those listed without asking.
> - All Win32 P/Invoke signatures must be checked against the official Microsoft docs. Do not guess struct layouts or constants.
> - Never block inside the low-level hook callback. Do the minimum and hand off work to the UI thread asynchronously.
> - If something in this spec is technically impossible, say so instead of working around it silently.

---

## 2. What the spike must demonstrate

1. The app runs in the background with no main window.
2. The user starts dragging a file in File Explorer or on the desktop and holds **Shift**.
3. A circular wheel appears centred on the cursor.
4. When the dragged file enters the wheel, the app reads the file path and shows the **file extension** in the centre of the wheel.
5. The wheel shows 3–4 placeholder segments chosen by file type (e.g. `.png` → "JPG / WebP / ICO"; `.pdf` → "Split / Images / QR"; anything else → "Unsupported").
6. The user drops the file on a segment. The app writes one line to a log: timestamp, full path, chosen segment.
7. **The original file is not moved, deleted, or altered.**
8. The wheel disappears on drop, on Esc, or if the mouse button is released anywhere else.

---

## 3. Stack

- .NET 8, C#, WPF, Windows 10/11 x64
- No NuGet packages needed for the spike (Win32 via P/Invoke)
- Logging: plain text file at `%LOCALAPPDATA%\WheelConvert\spike.log`
- Repo layout:
  ```
  /src/WheelConvert/          WPF project
  /docs/phase0-spike-spec.md  this file
  README.md                   one paragraph: "spike, not usable"
  .gitignore                  standard VisualStudio template
  ```

---

## 4. Technical design (the parts AI tools usually get wrong)

### 4.1 Detecting the gesture
- Install a global low-level mouse hook (`SetWindowsHookEx`, `WH_MOUSE_LL`) and check Shift state with `GetAsyncKeyState(VK_SHIFT)`.
- Trigger condition: left button is down **and** the cursor has moved more than 10 px since mouse-down **and** Shift is held.
- **Only trigger if the mouse-down happened over Explorer or the desktop.** At mouse-down, get the window under the cursor (`WindowFromPoint` → `GetAncestor(GA_ROOT)`) and check its class name: `CabinetWClass` (Explorer), `Progman` or `WorkerW` (desktop). Otherwise ignore. This stops the wheel popping up during Shift-click text selection in other apps.
- The hook callback must return immediately: post a message or `Dispatcher.BeginInvoke` and call `CallNextHookEx`. Windows silently removes hooks that take too long.

### 4.2 The wheel window
- Borderless, `Topmost`, `ShowInTaskbar=false`, `AllowsTransparency=true`, ~260×260 px, centred on the cursor.
- **Must not steal focus:** set the `WS_EX_NOACTIVATE` and `WS_EX_TOOLWINDOW` extended styles.
- **Background must not be fully transparent.** Fully transparent pixels don't receive drag-drop. Use a background with alpha ≥ 1 (e.g. `#01000000`) across the whole circle.
- `AllowDrop=true`.

### 4.3 Receiving the drop
- Handle `DragEnter` / `DragOver` / `Drop` on the wheel.
- In `DragEnter`, read `DataFormats.FileDrop` → `string[]` paths. Use the first path's extension to build the segments. If there's no file-drop data, show "Unsupported".
- In `DragOver`, hit-test which segment is under the cursor and highlight it.
- **Always set `e.Effects = DragDropEffects.Copy`.** Shift+drag in Explorer normally means *Move*. If the app reports Move, Explorer may delete the source file. This is the single most important line in the spike.

### 4.4 Privilege level
- The app must run **non-elevated** (normal user). Windows blocks drag-drop from Explorer into an elevated (admin) app. The `app.manifest` must say `asInvoker`.

### 4.5 Lifecycle
- Single instance (named mutex).
- Unhook on exit.
- For the spike, quit via a console window or Task Manager. No tray icon yet.

---

## 5. Build steps (agent stops after each)

| Step | Build | How I test it |
|---|---|---|
| 1 | Empty WPF app, no main window, single instance, writes "started" to the log | Run it; log line appears; second launch exits |
| 2 | Global mouse hook that logs "trigger" when the 4.1 conditions are met | Shift-drag a file in Explorer → "trigger" logged. Shift-drag text in Notepad → nothing logged |
| 3 | Wheel window appears at the cursor on trigger; hides on mouse-up or Esc | Wheel appears under cursor, Explorer keeps focus, wheel disappears on release |
| 4 | Drag events: show the extension in the centre, placeholder segments by type, highlight on hover | Drag a .png, a .pdf, and a .txt into the wheel; correct labels show |
| 5 | Drop: log path and segment, force Copy effect, close wheel | Drop on a segment; log line correct; **source file still in its original folder** |
| 6 | Hardening pass | Run every acceptance test in Section 6 |

---

## 6. Acceptance tests (all must pass)

1. Shift-drag `photo.png` from Explorer → wheel appears within ~150 ms, centre shows `.png`.
2. Drop on a segment → correct log line; **`photo.png` still exists, unmoved**.
3. Same test from the desktop.
4. Shift-drag of 3 files at once → wheel shows the first file's extension, log lists all 3 paths.
5. Shift-select text in Notepad or a browser → **no wheel**.
6. Normal drag without Shift → no wheel; Explorer behaves normally.
7. Press Esc mid-drag → wheel closes; Explorer drag continues normally.
8. Run for 30 minutes of normal use → no lag, no hook dropped (check that the trigger still logs at the end).
9. **Windows Defender** full scan of the built `.exe` → no detection. Record the result in the README.
10. Works on a second machine or a fresh Windows user account (catches "works on my PC" setups).

---

## 7. Out of scope for Phase 0
Conversions, FFmpeg/ImageMagick, PDF/QR, settings, tray icon, installer, startup launch, code signing, dark mode, animations.

---

## 8. Fallback if the spike fails
Fail triggers: test 1, 2, 5, or 9 can't be made to pass within the time box.
Fallback design: no mouse hook. A global hotkey (e.g. `Ctrl+Alt+Space`, via `RegisterHotKey`) opens the wheel at the cursor; the user then drags the file onto it. Sections 4.2–4.4 are unchanged.

---

## 9. Deliverables at end of Phase 0
- Working spike on a GitHub branch `spike/gesture`
- README with: what works, what doesn't, Defender result, a short screen recording (GIF)
- A one-paragraph go/no-go note: gesture or fallback for Phase 1
