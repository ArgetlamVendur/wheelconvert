# WP-02: Shift-drag trigger

**Builder:** Claude Code · **Reviewer:** Antigravity · **Depends on:** WP-01 · **Risk:** high (global hook)

## Goal
Open the wheel when the user holds **Shift** and drags with the left mouse button, starting over **File Explorer or the desktop**.

## Owns
- `src/WheelConvert.App/Triggers/ShiftDragTrigger.cs`
- `src/WheelConvert.App/Triggers/GestureStateMachine.cs` (new; pure logic, see req. 3)
- `src/WheelConvert.App/Interop/NativeMethods.Hook.cs` (new)

## Must not touch
Wheel, tray, composition, Core, other triggers.

## Requirements
1. Implement `ITrigger` (CONTRACTS C3). Constructor takes `int dragThresholdPx`.
2. `Start()`: install `SetWindowsHookEx(WH_MOUSE_LL, …, GetModuleHandle(null), 0)` **on the UI thread** (the thread needs a message loop; WPF's dispatcher provides it). Keep the `HookProc` delegate in a field. If installation returns zero: set `LastError` (e.g. "Shift-drag could not start (error 5).") and return `false`; never throw. Return `true` on success.
3. Put the gesture logic in a **pure class** `GestureStateMachine` (no Win32 calls) so it can be reasoned about and tested:
   - Input: `(MouseEvent kind, ScreenPoint point, bool shiftDown, bool startedOverExplorer)`.
   - `LeftDown` → remember point, `armed = startedOverExplorer`.
   - `Move` while armed, shift down, distance > threshold, not yet fired → output `OpenWheel(point)` once per drag.
   - `LeftUp` → if fired, output `Dismiss`; reset.
4. Explorer/desktop check happens **at mouse-down only**: `WindowFromPoint` → `GetAncestor(GA_ROOT)` → `GetClassName` ∈ {`CabinetWClass`, `Progman`, `WorkerW`}. Cache nothing across drags.
5. Shift state: `GetAsyncKeyState(VK_SHIFT) & 0x8000`.
6. Hook callback: compute the state transition, `Dispatcher.BeginInvoke` the event, **always return `CallNextHookEx`**. Never swallow mouse input. No logging, no allocation-heavy work, no locks inside the callback. The `WindowFromPoint` + `GetClassName` call at mouse-down is the one allowed exception (it's fast).
7. Ignore injected events? **No** — leave `LLMHF_INJECTED` events alone (touchpads and tablets inject).
8. `Stop()`/`Dispose()`: `UnhookWindowsHookEx`; idempotent.
9. Liveness: Windows silently removes hooks that time out (`LowLevelHooksTimeout`). Track the last callback time; expose `DateTime LastActivityUtc` so WP-11 can reinstall if no events arrive for a long time while the user is clearly active. (Detection policy is WP-11's.) After a successful Start() (including a reinstall), set LastActivityUtc to now, so the liveness check does not restart the hook every minute while the user only types.

## Gotchas
- `GetModuleHandle(null)` is correct for `WH_MOUSE_LL` in .NET; don't pass `Marshal.GetHINSTANCE`.
- `MSLLHOOKSTRUCT.pt` is in **physical pixels** for a per-monitor-DPI-aware process (the manifest sets PerMonitorV2). Pass it through unchanged as `ScreenPoint`.
- Shift-click selection in Explorer itself starts with Shift down but no drag: the threshold handles it.
- Explorer's drag starts after `SM_CXDRAG`/`SM_CYDRAG` (~4 px). Our default threshold (10 px) is larger on purpose so the OLE drag is already running when the wheel appears.
- Antivirus may flag `SetWindowsHookEx`. Don't try to evade detection; WP-12 measures it.

## Acceptance tests
| # | Who | Steps | Expected |
|---|---|---|---|
| 1 | Agent | Build App | 0 warnings |
| 2 | Agent | Read-through: every P/Invoke has a doc URL and matches it | ✔ |
| 3 | User | `D:\Claude Base\Cowork Playground\APPS\wc-test\` in Explorer, hold Shift, drag `photo.png` 3 cm | Wheel opens once at the cursor |
| 4 | User | Same without Shift | No wheel; normal Explorer drag |
| 5 | User | Shift-drag on the desktop | Wheel opens |
| 6 | User | Shift+drag to select text in Notepad / a browser | No wheel |
| 7 | User | Release the mouse anywhere other than the wheel | Wheel closes, file untouched |
| 8 | User | Use the PC normally for 30 min with the app running | No mouse lag; test 3 still works afterwards |
| 9 | User | Tablet/touchpad drag with Shift, if available | Wheel opens |

## Review focus
Callback cost; delegate lifetime (GC); `CallNextHookEx` on every path; correct x64 struct layout; thread the hook is installed on; idempotent Stop.

## Out of scope
Fallback logic (WP-11), the wheel itself (WP-04), settings UI (WP-05).

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md, docs/modules/WP-02-shift-drag-trigger.md, docs/decisions/ADR-006-two-triggers.md.
You are the builder for WP-02. Create branch wp-02-shift-drag from main. Restate the plan in 3–5 lines and wait for "go".
```
