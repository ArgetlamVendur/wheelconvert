# Step 02 — Global mouse hook

**Builder:** Claude Code  **Reviewer:** Antigravity
**Branch:** `step-02-hook`
**Spec refs:** Section 4.1; AGENTS.md guardrails 2, 3, 7

## Build
1. `Interop/NativeMethods.cs`: `SetWindowsHookEx`, `UnhookWindowsHookEx`, `CallNextHookEx`, `GetModuleHandle`, `GetAsyncKeyState`, `WindowFromPoint`, `GetAncestor`, `GetClassName`, `MSLLHOOKSTRUCT`, `POINT`, constants `WH_MOUSE_LL`, `WM_LBUTTONDOWN`, `WM_LBUTTONUP`, `WM_MOUSEMOVE`, `VK_SHIFT`, `GA_ROOT`. **Each with its learn.microsoft.com URL in a comment.**
2. `Input/GestureHook.cs`:
   - Installs the hook on the UI thread at startup; unhooks on exit.
   - Keeps the hook delegate in a field (prevents garbage collection — a common crash).
   - On `WM_LBUTTONDOWN`: record point; resolve root window class under cursor; mark `armed` only if class is `CabinetWClass`, `Progman`, or `WorkerW`.
   - On `WM_MOUSEMOVE` while armed: if distance > 10 px **and** Shift down → raise `Triggered(point)` once per drag via `Dispatcher.BeginInvoke`.
   - On `WM_LBUTTONUP`: disarm; raise `Released` via `BeginInvoke`.
   - Callback does no logging, no allocation-heavy work, no I/O. Always returns `CallNextHookEx`.
3. App subscribes and logs `trigger x,y` and `released`.
4. Turn on `TreatWarningsAsErrors`.

## Do NOT
- Show any window yet. Do not cancel or swallow mouse events (never return non-zero without calling next hook).

## How to test
| # | Who | Steps | Expected |
|---|---|---|---|
| 1 | Agent | Build | 0 warnings, 0 errors |
| 2 | User | Run app. Open `C:\wc-test\`, hold Shift, drag `photo.png` a few cm, release | Log: one `trigger`, then `released` |
| 3 | User | Same without Shift | No `trigger` |
| 4 | User | Notepad: Shift+drag to select text | No `trigger` |
| 5 | User | Shift-drag on the desktop | One `trigger` |
| 6 | User | Normal clicking/dragging for 2 minutes | No lag in mouse movement |

## Reviewer focus (Antigravity)
Every P/Invoke matches its linked doc (types, `SetLastError`, struct layout). Delegate is kept alive. Nothing slow inside the callback. `CallNextHookEx` is always called.
