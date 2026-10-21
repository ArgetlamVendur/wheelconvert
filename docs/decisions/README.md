# Architecture Decision Records

One file per locked decision. Agents must follow these; changing one needs user approval and a new ADR that supersedes it (never edit an accepted ADR's decision; add "Superseded by ADR-0xx").

| ADR | Decision | Status |
|---|---|---|
| [001](ADR-001-stack.md) | .NET 8 + WPF; Core is cross-platform with no packages | Accepted |
| [002](ADR-002-output-safety.md) | Never overwrite; temp file + atomic commit next to the source | Accepted |
| [003](ADR-003-copy-only-drop.md) | Drop effect is always Copy | Accepted |
| [004](ADR-004-ffmpeg-not-bundled.md) | FFmpeg is detected, not bundled | Accepted |
| [005](ADR-005-licensing.md) | MIT licence; allowed and banned dependencies | Accepted |
| [006](ADR-006-two-triggers.md) | Two triggers behind one setting; default Shift-drag | Accepted |
| [007](ADR-007-tray-and-notifications.md) | WinForms NotifyIcon for tray and balloon notifications | Accepted |
| [008](ADR-008-test-runner.md) | Dependency-free test runner for Core | Accepted |
| [009](ADR-009-qr-scope.md) | QR reading from images only in v1 | Accepted |
| [010](ADR-010-package-versions.md) | Package version register | Living document |

Template for a new ADR:

```
# ADR-0NN: <title>
Status: Proposed | Accepted | Superseded by ADR-0xx
Date: YYYY-MM-DD
## Context
## Decision
## Consequences
```
