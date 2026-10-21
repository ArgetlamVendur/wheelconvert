# ADR-001: Stack
Status: Accepted · Date: 2026-10-20

## Context
The app needs global input hooks, a transparent drop-target window and native Explorer drag-and-drop. It is built mostly by AI agents, so the stack must be well-documented and the core logic testable without Windows.

## Decision
- App shell: **C# / .NET 8 / WPF**, Windows 10/11 x64, `net8.0-windows`.
- **`WheelConvert.Core` targets plain `net8.0`, uses no Windows APIs and no NuGet packages.** All logic that can be pure (formats, naming, queue, settings, wheel maths, process running) lives there and is unit-tested on any OS.
- Each converter is its own project referencing only Core (+ its own packages), so modules can be built, reviewed and replaced independently.
- Win32 interop is confined to `WheelConvert.App/Interop/`, every declaration with its learn.microsoft.com URL.

## Consequences
- Agents can verify Core changes without Windows; App changes need Windows or CI.
- Converters must not reference each other or the App. Shared needs go into Core contracts (with approval).
