# WP-07: Audio/video converter (FFmpeg)

**Builder:** Claude Code · **Reviewer:** Antigravity · **Depends on:** WP-01 · **Risk:** medium

## Goal
Audio and video conversions through the user's own FFmpeg (ADR-004).

## Owns
- `src/WheelConvert.Converters.Media/**`
- `tests/WheelConvert.Converters.Media.Tests/**` (new) + add to `WheelConvert.sln`

## Requirements
1. `CheckHealth()`: `ToolLocator.Find("ffmpeg", provider())`; null → `Missing("Install FFmpeg: winget install Gyan.FFmpeg")`. Cache the found path until the next `CheckHealth()`; must not run ffmpeg (keep it < 50 ms).
2. Build arguments in a pure, unit-testable `FfmpegArguments.For(targetId, input, output)` returning `IReadOnlyList<string>`. Always include: `-hide_banner -nostdin -loglevel info -nostats -progress pipe:2 -n -i <input> … <output>`. Everything (the `Duration:` line, `out_time_us=` progress lines, errors) arrives on **stderr**, which `ProcessRunner` streams line by line and keeps the last 64 KB of. `-n` = never overwrite (belt and braces with ADR-002; the temp file doesn't exist yet).

   | Target | Args (after `-i`) |
   |---|---|
   | audio.mp3 / video.extract-mp3 | `-vn -map 0:a:0 -c:a libmp3lame -q:a 2` |
   | audio.m4a | `-vn -map 0:a:0 -c:a aac -b:a 192k` |
   | audio.flac | `-vn -map 0:a:0 -c:a flac` |
   | audio.wav | `-vn -map 0:a:0 -c:a pcm_s16le` |
   | audio.ogg | `-vn -map 0:a:0 -c:a libvorbis -q:a 5` |
   | audio.opus | `-vn -map 0:a:0 -c:a libopus -b:a 128k` |
   | video.mp4 | `-c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p -c:a aac -b:a 160k -movflags +faststart` |
   | video.webm | `-c:v libvpx-vp9 -crf 32 -b:v 0 -c:a libopus -b:a 128k` |
   | video.gif | `-vf fps=12,scale='min(720,iw)':-2:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse -loop 0` |

3. Run with `ProcessRunner.RunAsync(ffmpeg, args, timeout, ct, onStdErrLine)`. Timeout: 2 h for video, 30 min for audio.
4. Progress: in the `onStdErrLine` callback parse the first `Duration: hh:mm:ss.xx` line, then each `out_time_us=<n>` line; report `n / durationUs` through the `IProgress<double>` (the queue throttles it). No duration (e.g. some streams) → no progress. **Don't add ffprobe.** Never fail because progress parsing failed. For error messages, use the last non-progress stderr lines (skip `key=value` lines).
5. Exit code ≠ 0 → `Fail(ToolFailed, "FFmpeg could not convert this file.")`; log the last 5 lines of stderr. Stderr containing "Invalid data found" → `CorruptInput`; "Unknown encoder" → `Fail(ToolMissing, "Your FFmpeg build lacks the <codec> encoder.")`.
6. Output label: `video.extract-mp3` uses `OutputLabels.Audio`; others use the label option (default "converted").
7. Temp/commit/discard exactly per CONTRACTS C2 (`try/finally`).
8. `CanConvert`: as in the stub; also refuse `audio.*` → same container (handled by registry) and `video.gif` from inputs longer than… (can't know without probing — allow, rely on timeout).

## Gotchas
- `ProcessRunner` closes stdin; `-nostdin` makes FFmpeg not read it anyway.
- Paths with Unicode/spaces are safe because `ArgumentList` quotes correctly; never build a single argument string.
- An input file named like `-something.mp3` would be parsed as an option. Pass inputs as **full paths** (they start with a drive letter) — `Path.GetFullPath` guarantees it.
- GPL encoders (libx264) are in the user's FFmpeg build, not ours (ADR-004).

## Acceptance tests
| # | Who | Test | Expected |
|---|---|---|---|
| 1 | Agent (xUnit) | `FfmpegArguments` for every target | Exact expected list; input/output are separate elements |
| 2 | Agent (xUnit) | `CheckHealth` with a fake PATH | Missing / Ready |
| 3 | Agent (xUnit, skipped if no ffmpeg) | Generate a 1 s sine WAV with ffmpeg, convert to each audio target | Valid outputs, source unchanged, no temp left |
| 4 | Agent (xUnit, skipped if no ffmpeg) | Corrupt input | `CorruptInput` or `ToolFailed`, no temp left |
| 5 | Agent (xUnit) | Cancel mid-conversion | Process killed, `OperationCanceledException`, temp discarded |
| 6 | User | Uninstall FFmpeg / rename it, restart | Audio segments gone; tray status explains |
| 7 | User | `winget install Gyan.FFmpeg`, tray → Check again | Segments appear without restart |
| 8 | User | MOV from phone → MP4; MP4 → GIF; MP4 → Audio | Plays correctly |

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md (C1, C2, C9), docs/modules/WP-07-media-converter.md, docs/decisions/ADR-004-ffmpeg-not-bundled.md.
You are the builder for WP-07. Create branch wp-07-media from main. Restate the plan in 3–5 lines and wait for "go".
```
