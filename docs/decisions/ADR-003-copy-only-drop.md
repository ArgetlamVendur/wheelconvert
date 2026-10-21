# ADR-003: Drop effect is always Copy
Status: Accepted · Date: 2026-10-20

## Context
In Explorer, Shift+drag means **Move**. If our drop target reports `DragDropEffects.Move`, Explorer may delete the source after the drop. The whole gesture relies on Shift.

## Decision
Every `DragEnter`, `DragOver` and `Drop` handler on the wheel sets `e.Effects = DragDropEffects.Copy` (or `None` to cancel) as its first statement, inside `try/finally` so an exception can't leave another value. `Move` never appears in the codebase outside comments.

## Consequences
- CI greps for `DragDropEffects.Move` and fails the build if found (WP-12).
- Manual test on every release: Shift-drag from Explorer, desktop, a USB drive and a network share; the source must remain.
