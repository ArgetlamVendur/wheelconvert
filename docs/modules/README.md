# Work packages

Each WP is self-contained: an agent can implement it with only this repo, the WP file, `AGENTS.md` and `docs/CONTRACTS.md`.

| WP | Module | Builder → Reviewer | Depends on | Risk |
|---|---|---|---|---|
| [01](WP-01-core.md) | Core: contracts, formats, naming, queue, settings | done (base) | — | — |
| [02](WP-02-shift-drag-trigger.md) | Shift-drag trigger (global mouse hook) | Claude Code → Antigravity | 01 | **High** |
| [03](WP-03-hotkey-trigger.md) | Hotkey trigger | Antigravity → Claude Code | 01 | Low |
| [04](WP-04-wheel-window.md) | Wheel window + drag/drop | Antigravity → Claude Code | 01 | **High** (file safety) |
| [05](WP-05-tray-settings.md) | Tray icon, menu, notifications, start-up | Antigravity → Claude Code | 01 | Low |
| [06](WP-06-image-converter.md) | Image converter (Magick.NET) | Antigravity → Claude Code | 01 | Medium |
| [07](WP-07-media-converter.md) | Audio/video converter (FFmpeg) | Claude Code → Antigravity | 01 | Medium |
| [08](WP-08-pdf-operations.md) | PDF operations + page renderer | Claude Code → Antigravity | 01 | Medium |
| [09](WP-09-pdf-reorder-window.md) | PDF reorder window | Antigravity → Claude Code | 01, 08 (renderer) | Low |
| [10](WP-10-qr-reader.md) | QR reader | Antigravity → Claude Code | 01 | Low |
| [11](WP-11-integration.md) | Integration: wiring, coordinator, fallback, clean-up | Claude Code → Antigravity | 01; stubs of 02–10 suffice | **High** |
| [12](WP-12-packaging-release.md) | Packaging, release, licences, Defender check | Claude Code → User | all | Medium |

## Suggested order
1. **WP-11** (wiring) + **WP-05** (tray) + **WP-03** (hotkey) + **WP-04** (wheel) → the app runs and the wheel opens.
2. **WP-06** → first real conversion end-to-end.
3. **WP-02** → the gesture. If it fails the Defender check, the hotkey keeps the app usable (ADR-006).
4. **WP-07, WP-08, WP-10** in any order, in parallel if you like (separate projects, no shared files).
5. **WP-09**, then **WP-12**.

## Parallel-safe pairs
Converters (06, 07, 08, 10) never share files and can run at the same time in different tools. App-shell WPs (02, 03, 04, 05, 09, 11) each own separate folders but all touch `WheelConvert.App`: run at most two at once and rebase before the PR.

## WP file template
Goal · Builder/Reviewer · Depends on · Owns · Must not touch · Requirements · Gotchas · Acceptance tests · Review focus · Out of scope · Start prompt.
