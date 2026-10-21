# Step 03 — Wheel window

**Builder:** Antigravity  **Reviewer:** Claude Code
**Branch:** `step-03-wheel`
**Spec refs:** Section 4.2; AGENTS.md guardrails 5, 6

## Build
1. `Wheel/WheelWindow.xaml(.cs)`: borderless (`WindowStyle=None`), `AllowsTransparency=True`, `Topmost=True`, `ShowInTaskbar=False`, `ShowActivated=False`, 260×260.
2. In `SourceInitialized`, add extended styles `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` via `GetWindowLongPtr`/`SetWindowLongPtr` (P/Invoke with doc URLs, in `Interop/`).
3. Visual: a circle, fill with alpha ≥ 1 everywhere (e.g. `#CC202020` ring, `#01000000` minimum), 4 empty placeholder segments drawn as arcs, a centre label (empty for now).
4. On `Triggered(point)`: position window centred on the point (convert physical pixels → WPF DIPs using the monitor DPI; test on a scaled display), show it.
5. Hide on `Released` and on Esc (Esc detected via the existing hook or a low-level keyboard hook — if you add `WH_KEYBOARD_LL`, same guardrails as Step 02).
6. Reuse one window instance; do not create a new window per trigger.

## Do NOT
- Handle drag/drop yet. No animations.

## How to test
| # | Who | Steps | Expected |
|---|---|---|---|
| 1 | Agent | Build | 0 warnings |
| 2 | User | Shift-drag a file in Explorer | Wheel appears centred under the cursor; Explorer title bar stays active (not greyed) |
| 3 | User | Release mouse | Wheel disappears |
| 4 | User | Shift-drag, press Esc while still holding | Wheel disappears |
| 5 | User | Repeat on a second monitor or with display scaling 125%/150% | Wheel still centred under cursor |
| 6 | Agent | Screenshot of wheel visible over Explorer, attached to HANDOFF entry | — |

## Reviewer focus (Claude Code)
Focus not stolen; DPI conversion correct; P/Invoke for `GetWindowLongPtr`/`SetWindowLongPtr` correct on x64; no window leak.
