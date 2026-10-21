# ADR-006: Two triggers behind one setting
Status: Accepted · Date: 2026-10-20

## Context
The Shift-drag gesture needs a global low-level mouse hook. It is unproven: antivirus may flag it, managed laptops may block it, and Windows silently removes slow hooks. The spike gate (docs/archive/phase0) was skipped to build the base, so the risk is still open.

## Decision
- `ITrigger` has two implementations: `ShiftDragTrigger` (WP-02) and `HotkeyTrigger` (WP-03).
- `AppSettings.Trigger` = `ShiftDrag` (default) | `Hotkey` | `Both`. The composition root (WP-11) starts the selected ones.
- If the hook fails to install, the app logs it, shows one notification, and starts the hotkey trigger automatically.

## Consequences
- The wheel, converters and integration never depend on how the wheel was opened.
- **Go/no-go test still required** before the first public release: Windows Defender scan + VirusTotal of the published exe (WP-12). If the hook is flagged, flip the default to `Hotkey` with a new ADR.
