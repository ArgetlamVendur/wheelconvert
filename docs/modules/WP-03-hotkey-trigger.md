# WP-03: Hotkey trigger

**Builder:** Antigravity · **Reviewer:** Claude Code · **Depends on:** WP-01 · **Risk:** low

## Goal
A global hotkey (default `Ctrl+Alt+Space`) opens the wheel at the mouse cursor. The user then drags a file onto it. This is the guaranteed fallback if the Shift-drag hook fails (ADR-006).

## Owns
- `src/WheelConvert.App/Triggers/HotkeyTrigger.cs`
- `src/WheelConvert.App/Triggers/HotkeyParser.cs` (new; pure)
- `src/WheelConvert.App/Interop/NativeMethods.Hotkey.cs` (new)

## Requirements
1. Implement `ITrigger`. Constructor takes the hotkey string from `AppSettings.Hotkey`.
2. `HotkeyParser.TryParse(string, out uint modifiers, out uint virtualKey)`: `Ctrl`, `Alt`, `Shift`, `Win` + one key (`A`–`Z`, `0`–`9`, `F1`–`F24`, `Space`, `Enter`, `Tab`, `Esc`). Case-insensitive, `+` separated, whitespace tolerant. At least one modifier required. Always add `MOD_NOREPEAT`.
3. `Start()`: create a message-only window via `HwndSource` (`ParentWindow = HWND_MESSAGE` (-3)), `RegisterHotKey(hwnd, id, modifiers, vk)`. On failure (`ERROR_HOTKEY_ALREADY_REGISTERED` or parse failure) set `LastError` ("Ctrl+Alt+Space is already used by another app." / "Hotkey 'X' isn't valid.") and return `false`; never throw.
4. On `WM_HOTKEY`: `GetCursorPos` → raise `WheelRequested(point)` (already on the UI thread). **Always** raise `WheelRequested`; don't track whether the wheel is open — WP-11 turns a second press into "close" because only it knows `wheel.IsOpen`.
5. `Stop()`/`Dispose()`: `UnregisterHotKey`, dispose the `HwndSource`. Idempotent.

## Gotchas
- `GetCursorPos` returns physical pixels under PerMonitorV2: pass through unchanged.
- The wheel must stay open while the user moves to a file and drags it in. Dismissal: Esc (WP-04), hotkey again, or a drop. WP-04 owns any timeout.

## Acceptance tests
| # | Who | Steps | Expected |
|---|---|---|---|
| 1 | Agent | Build App | 0 warnings |
| 2 | Agent | `HotkeyParser` cases listed in req. 2 incl. invalid ones ("Space", "Ctrl+", "Ctrl+Foo") | Correct accept/reject (add these as a small test or a documented manual check) |
| 3 | User | Press Ctrl+Alt+Space | Wheel opens at the cursor |
| 4 | User | Press it again (after WP-11) | Wheel closes |
| 5 | User | Set `"hotkey": "Ctrl+Alt+Q"` in settings.json, restart | New hotkey works, old one doesn't |
| 6 | User | Use a hotkey another app owns | App starts; `Start()` returned false; `LastError` shown/logged by WP-11 |

## Review focus
Message-only window lifetime; unregister on exit; modifiers constants match docs.

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md, docs/modules/WP-03-hotkey-trigger.md.
You are the builder for WP-03. Create branch wp-03-hotkey from main. Restate the plan in 3–5 lines and wait for "go".
```
