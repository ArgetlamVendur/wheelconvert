# ADR-007: Tray icon and notifications
Status: Accepted · Date: 2026-10-20

## Context
WPF has no tray icon. Options: `System.Windows.Forms.NotifyIcon` (in-box), the `H.NotifyIcon.Wpf` package, or WinRT toast APIs (need an AppUserModelID / shortcut registration for unpackaged apps).

## Decision
- Use **WinForms `NotifyIcon`** (`<UseWindowsForms>true</UseWindowsForms>` in the App project) for the tray icon, context menu and `ShowBalloonTip` notifications. Windows 10/11 render balloon tips as toasts.
- No extra package.

## Consequences
- Notifications can't have buttons; clicking the balloon opens Explorer with the output file selected (via `BalloonTipClicked`).
- The App project removes the implicit `System.Windows.Forms` and `System.Drawing` usings; reach WinForms types through `using Forms = System.Windows.Forms;`. Even so, use explicit aliases or fully qualified names for types that clash between WPF, WinForms, `System.IO` and `System.Threading`: `Application`, `MessageBox`, `Clipboard`, `Point`, `DragEventArgs`, `Path`, `Brush`, `Timer`.
- Richer toasts can replace this later behind `INotifier` without touching other modules.
