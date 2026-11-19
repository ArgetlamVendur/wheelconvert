# WP-21: FFmpeg input hardening and converter error log

**Builder:** Sonnet · **Reviewer:** Opus (fresh session) · **Depends on:** WP-07 (done) · **Risk:** medium (security; changes every FFmpeg call) · **Blocks:** WP-16, WP-17

> **User approval needed before starting:** ADR-013 must be `Accepted` (it changes what the log contains). Reqs 1–4 don't need it; if ADR-013 is still `Proposed`, build 1–4 only and say so in the PR.

## Goal
WheelConvert hands files from anywhere (downloads, email attachments) to FFmpeg. A crafted file can make FFmpeg read other local files or open network connections (playlist and concat tricks hidden inside an ordinary-looking `.avi` or `.mp4`). Close that before v0.2 adds more FFmpeg targets (WP-16) and brings FFmpeg to many more users (WP-17). Also log *why* a conversion failed (ADR-013).

## Owns
- `src/WheelConvert.Converters.Media/**` (`FfmpegArguments.cs`, `MediaConverter.cs`, `FfmpegProgressParser.cs`)
- `src/WheelConvert.Converters.Image/ImageConverter.cs`: the failure paths only, for ADR-013's Magick lines
- `tests/WheelConvert.Converters.Media.Tests/**`, `tests/WheelConvert.Converters.Image.Tests/**`
- `README.md`: one line in the bug-report section ("attach `%LOCALAPPDATA%\WheelConvert\wheelconvert.log`")
- `docs/decisions/ADR-013-*.md` (status line only, after the user accepts)

## Must not touch
`ProcessRunner`, `ToolLocator`, `FileLog`, Core contracts, other converters.

## Requirements
1. **Protocols:** add `-protocol_whitelist file` as an input option (before `-i`), so FFmpeg can read local files but never open `http`, `tcp`, `udp`, `rtmp`, `data`, `concat:`, `subfile` and so on, even when a file asks it to.
2. **Input formats:** add `-format_whitelist <list>` before `-i`, listing only the demuxers for the extensions in `FormatRegistry`'s audio and video kinds (for example `mov,mp4,m4a,3gp,3g2,mj2,matroska,webm,avi,mp3,wav,flac,ogg,aac`). Build the list from FFmpeg's own names: run `ffmpeg -hide_banner -demuxers` with the tested FFmpeg (9.0.2) and put the exact names in a constant with a comment saying which extension needs each. **Never** include `hls`, `concat`, `ffconcat`, `image2`, `tee` or any playlist/meta demuxer.
3. **Input path:** pass the input as `file:` + the absolute path (`file:D:\clips\a.mp4`), so a name that looks like a protocol can never be read as one. Keep `Path.GetFullPath` (CONTRACTS C9). Check with real FFmpeg that Windows paths with spaces and non-ASCII characters (Hindi, Odia) still work.
4. **Progress lines (review note N7):** `IsProgressLine` must match only FFmpeg's known `-progress` keys: `frame`, `fps`, `stream_*_q`, `bitrate`, `total_size`, `out_time_us`, `out_time_ms`, `out_time`, `dup_frames`, `drop_frames`, `speed`, `progress`. Any other `key=value` line (real error text can look like that) stays in the error output.
5. **Error log (ADR-013):** on a failed FFmpeg run, log the last 20 non-progress stderr lines; on a failed Magick conversion, log exception type + message. Paths replaced by the file name only, lengths cut as ADR-013 says. Never on success, never in the UI.

## Gotchas
- `-format_whitelist` makes a file whose real format isn't listed fail at open. That's intended, and the failure must map to `CorruptInput` "Couldn't read <file>." (not `Unknown`).
- `.mp4`/`.m4a`/`.mov` are opened by one demuxer whose name is itself a list (`mov,mp4,m4a,3gp,3g2,mj2`); FFmpeg matches the whitelist against each part, so `mov` alone should be enough. Prove it with test 4 rather than assuming.
- `.ogg` and `.opus` both use the `ogg` demuxer.
- WP-07's 123 Media tests must still pass with real FFmpeg; the new options go before `-i`, and the argument-list tests need updating to match.

## Acceptance tests
| # | Who | Test | Expected | Result |
|---|---|---|---|---|
| 1 | Agent | Argument list for every target | Contains `-protocol_whitelist file`, `-format_whitelist <list>`, `-i file:<abs path>` in that order | PASS |
| 2 | Agent (real FFmpeg) | An HLS playlist (`.m3u8` text pointing at a local test file) renamed to `clip.mp4` | Fails, `CorruptInput`; the referenced file is never read (check FFmpeg's stderr) | PASS |
| 3 | Agent (real FFmpeg) | An `ffconcat` text file renamed to `clip.avi` | Fails, `CorruptInput` | PASS |
| 4 | Agent (real FFmpeg) | All WP-07 sample conversions | Still pass | PASS |
| 5 | Agent (real FFmpeg) | Input `D:\…\टेस्ट ଓଡ଼ିଆ clip.mp4` (space + Hindi + Odia) | Converts | PASS |
| 6 | Agent | `IsProgressLine("error=foo")`, `IsProgressLine("speed=1.2x")` | false, true | PASS |
| 7 | Agent | Failed FFmpeg run (fake runner, 50 stderr lines incl. progress) | Log gets 20 non-progress lines, file names only, no full paths | PASS |
| 8 | Agent | Failed Magick conversion (corrupt PNG) | Log line with exception type + message, no stack trace, no full path | PASS |
| 9 | User | Convert a phone video and an MP3 from `wc-test` | Same results as before | PASS (user, 2026-11-19) |

## Review focus
No path from a dropped file to a network connection or a second local file; whitelist names checked against real FFmpeg output; log contains no full paths.

## Out of scope
Sandboxing FFmpeg (job objects, low-integrity process): later, if ever. Magick.NET policy hardening (its own delegates): note it in the PR as a possible next step.

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md (C9), docs/decisions/ADR-004-ffmpeg-not-bundled.md, docs/decisions/ADR-013-converter-error-logging.md, docs/modules/WP-07-media-converter.md, docs/modules/WP-21-ffmpeg-hardening.md.
Check ADR-013's status: if Proposed, build reqs 1–4 only and say so.
You are the builder for WP-21. Create branch wp-21-ffmpeg-hardening from main. Restate the plan in 3–5 lines and wait for "go".
```
