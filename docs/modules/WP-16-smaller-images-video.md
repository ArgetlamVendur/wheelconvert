# WP-16: "Smaller" for images and video

**Builder:** Sonnet · **Reviewer:** Opus (fresh session) · **Depends on:** WP-13, **WP-21** (FFmpeg hardening merged first: this WP adds an FFmpeg target), and WP-15 merged first (same Image project) · **Risk:** medium · **Same project as:** WP-15, WP-19 (Image)

## Goal
One fixed preset that makes a photo or video small enough for email, WhatsApp and upload portals, without the user choosing anything.

## Owns
- `src/WheelConvert.Converters.Image/**` (new class `ImageShrinker.cs`; add `image.smaller`)
- `src/WheelConvert.Converters.Media/**` (add `video.smaller` to `FfmpegArguments` and `SupportedTargetIds`)
- Their test projects

## Requirements: images (`image.smaller`)
1. `AutoOrient()`, then if the long edge > **1920 px**, resize so the long edge is 1920 (never enlarge).
2. Output format (the target's `OutputExtension` is null, so the converter chooses, ADR-011 §4):

   | Source | Output | Settings |
   |---|---|---|
   | JPG/JPEG | `.jpg` | quality 75, 4:2:0 chroma, progressive |
   | WebP | `.webp` | quality 75 |
   | PNG, GIF (single frame), BMP, TIFF | same as source for PNG; others → `.png` | strip metadata, max PNG compression |
   | HEIC/HEIF, AVIF | `.jpg` | quality 75 (most apps and portals can't open HEIC) |
   | Animated GIF | — | `Fail(Unsupported, "Animated GIFs can't be made smaller yet.")` |

3. Metadata: strip everything except the colour profile (removes GPS; matches the open WP-06 privacy question — if the user later decides "keep GPS", this target still strips it).
4. **Size check:** if the output is not at least 5 % smaller than the source → discard it, `Fail(NoResult, "This image is already about as small as it gets.")`.
5. Label `OutputLabels.Smaller` → `photo (smaller).jpg`.

## Requirements: video (`video.smaller`)
6. Output `.mp4`, H.264 + AAC. Scale so the **short edge ≤ 720 px** (works for landscape and portrait phone videos), never upscale, both dimensions even. Settings: `-c:v libx264 -preset medium -crf 28 -pix_fmt yuv420p -c:a aac -b:a 96k -movflags +faststart`. Write the exact filter in `FfmpegArguments` and prove it with tests 6–7 below (no guessed filter strings: test against real FFmpeg).
7. Same progress/timeout/error handling as the other video targets (WP-07). Same size check as req. 4 → `Fail(NoResult, "This video is already about as small as it gets.")`.
8. Label `OutputLabels.Smaller` → `clip (smaller).mp4`.

## Acceptance tests
| # | Who | Test | Expected | Result |
|---|---|---|---|---|
| 1 | Agent | 4000 × 3000 JPEG q95 | 1920 × 1440 JPEG, smaller, upright, no GPS | |
| 2 | Agent | 800 × 600 JPEG q60 | Not enlarged; `NoResult` if not 5 % smaller, no temp left | |
| 3 | Agent | HEIC sample | `.jpg` output | |
| 4 | Agent | PNG with alpha | `.png`, alpha kept | |
| 5 | Agent | Animated GIF | `Unsupported` | |
| 6 | Agent (real FFmpeg) | 1920 × 1080 test video | 1280 × 720 MP4 | |
| 7 | Agent (real FFmpeg) | 1080 × 1920 (portrait) | 720 × 1280 MP4 | |
| 8 | Agent (real FFmpeg) | 640 × 360 video | Not upscaled | |
| 9 | User | A phone photo and a phone video from `wc-test` → Smaller | Both much smaller; both look fine; video plays in the Photos app | NEEDS USER |

## Out of scope
Choosing a target size ("under 2 MB"), quality sliders, audio "Smaller".

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/CONTRACTS.md (C1, C2, C9), docs/decisions/ADR-011-multi-file-targets.md, docs/modules/WP-16-smaller-images-video.md, docs/modules/WP-07-media-converter.md.
You are the builder for WP-16. Create branch wp-16-smaller from main. Restate the plan in 3–5 lines and wait for "go".
```
