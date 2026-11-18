# ADR-013: Converter error lines in the log
Status: Accepted · 2026-11-18 (user, via overnight-run authorisation) · Date: 2026-11-18

## Context
When an FFmpeg or Magick.NET conversion fails, the user sees a short message ("Couldn't convert clip.mov.") and the log (`FileLog`) records only the failure kind. FFmpeg's own error lines and Magick's exception text, which say *why*, are thrown away. That makes bug reports from real users nearly useless. This was a WP-06/WP-07 follow-up that needed an ADR because it changes what the log contains.

## Decision
- On a **failed** conversion only (never on success), the converter writes to the log:
  - **FFmpeg:** the last **20** lines of its stderr, after removing FFmpeg's progress lines (the known `-progress` keys, see WP-21 req. 4), each line cut to 300 characters.
  - **Magick.NET:** the exception type and message (no stack trace), cut to 500 characters.
- File paths in those lines are replaced by the **file name only** (`D:\Users\alex\Photos\clip.mov` → `clip.mov`), so a shared log doesn't reveal folder names or the Windows user name. The replacement covers the input and output paths the converter knows; other paths FFmpeg prints are left as they are.
- Log text never reaches the UI. User-facing messages stay as they are (CONTRACTS C9: tool output may not be UTF-8).
- The log stays local. Nothing is sent anywhere; the README's bug-report section tells users to attach it themselves.

## Consequences
- Bug reports become useful: "attach `wheelconvert.log`" now shows the real cause.
- The log grows only on failures; `FileLog` still rolls to `wheelconvert.log.1` at 1 MB.
- Implemented in WP-21.
