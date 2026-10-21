# ADR-002: Output safety
Status: Accepted · Date: 2026-10-20

## Context
A converter that overwrites or corrupts a user's file destroys trust instantly. Converters will be written by different agents with different habits.

## Decision
- Output goes **next to the source**, named `<stem> (<label>)<ext>`. If taken, `(<label> 2)`, `(<label> 3)`…
- Converters write only to a temp file from `IOutputWriter.CreateTempPath` (`.~wc-<guid><ext>` in the same folder), then `Commit` renames it with `File.Move(..., overwrite: false)`.
- `Discard` refuses to delete anything that isn't a `.~wc-` temp file.
- Sources are opened read-only, never modified, moved or deleted.

## Consequences
- No half-written files under real names; no overwrites even in races.
- Interrupted conversions can leave `.~wc-*` files; the app removes stale ones (older than 1 hour) from folders it has written to, on start-up (WP-11).
- A read-only source folder means conversion fails with `OutputFailed` (no fallback location in v1).
