# Work packages

Each WP is self-contained: an agent can implement it with only this repo, the WP file, `AGENTS.md` and `docs/CONTRACTS.md`.

| WP | Module | Builder → Reviewer | Depends on | Risk |
|---|---|---|---|---|
| [01](WP-01-core.md) | Core: contracts, formats, naming, queue, settings | done (base) | — | — |
| [02](WP-02-shift-drag-trigger.md) | Shift-drag trigger (global mouse hook) | Claude Code → Antigravity | 01 | **High** |
| [03](WP-03-hotkey-trigger.md) | Hotkey trigger | Antigravity → Claude Code | 01 | Low |
| [04](WP-04-wheel-window.md) | Wheel window + drag/drop | Claude Code → Antigravity | 01 | **High** (file safety) |
| [05](WP-05-tray-settings.md) | Tray icon, menu, notifications, start-up | Opus → Fable | 01 | Low |
| [06](WP-06-image-converter.md) | Image converter (Magick.NET) | Antigravity (fixes: Opus) → Sonnet, Fable | 01 | Medium |
| [07](WP-07-media-converter.md) | Audio/video converter (FFmpeg) | Opus → Fable | 01 | Medium |
| [08](WP-08-pdf-operations.md) | PDF operations + page renderer | Claude Code → Antigravity | 01 | Medium |
| [09](WP-09-pdf-reorder-window.md) | PDF reorder window | Opus → Fable | 01, 08 (renderer) | Low |
| [10](WP-10-qr-reader.md) | QR reader | Opus → Fable | 01 | Low |
| [11](WP-11-integration.md) | Integration: wiring, coordinator, fallback, clean-up | Claude Code → Antigravity | 01; stubs of 02–10 suffice | **High** |
| [12](WP-12-packaging-release.md) | Packaging, release, licences, Defender check | Opus → Fable | all | Medium |
| [13](WP-13-core-multi-input.md) | Core: multi-input targets, v0.2 catalog (ADR-011) | Sonnet → Opus | 01, 11 | **High** (frozen contracts) |
| [14](WP-14-pdf-merge.md) | PDF merge | Sonnet → Opus | 13 | Low |
| [15](WP-15-images-to-pdf.md) | Images → PDF | Sonnet → Opus | 13 | Medium |
| [16](WP-16-smaller-images-video.md) | "Smaller" for images and video | Sonnet → Opus | 13, 15 | Medium |
| [17](WP-17-ffmpeg-setup.md) | One-click FFmpeg install (ADR-012) | Sonnet → Opus | 05, 07 | Medium |
| [18](WP-18-pdf-to-word.md) | PDF → Word (text only) | Sonnet → Opus | 13 | Medium |
| [19](WP-19-image-resize.md) | Image resize + dialog | Sonnet → Opus | 13, 16 | Low |
| [20](WP-20-pdf-smaller.md) | Smaller PDF (no Ghostscript) | Sonnet → Opus | 13, 14 | **Medium-high** |
| [21](WP-21-ffmpeg-hardening.md) | FFmpeg input hardening + converter error log (ADR-013) | Sonnet → Opus | 07 | Medium |
| [22](WP-22-release-hardening.md) | Release hardening + version from tag | Sonnet → Opus | 12 | Low |

Remaining WPs use Claude Code only: Sonnet builds, Opus reviews in a fresh session (AGENTS.md §2; Opus replaced Fable on 2026-11-17).

## Suggested order
1. **WP-11** (wiring) + **WP-05** (tray) + **WP-03** (hotkey) + **WP-04** (wheel) → the app runs and the wheel opens.
2. **WP-06** → first real conversion end-to-end.
3. **WP-02** → the gesture. If it fails the Defender check, the hotkey keeps the app usable (ADR-006).
4. **WP-07, WP-08, WP-10** in any order, in parallel if you like (separate projects, no shared files).
5. **WP-09**, then **WP-12**.

## v0.2 order (2026-11-18)
1. **WP-22** (release hardening) and **WP-21** (FFmpeg hardening; needs ADR-013 Accepted): small, independent, make the base safer. Can run in parallel (different files).
2. User accepts **ADR-011**, and **ADR-012** after the by-hand winget check.
3. **WP-13** (contracts). **WP-17** can run any time after WP-21.
4. After WP-13, three tracks in parallel (each track shares one project, so in sequence within it):
   - PDF: **WP-14** → **WP-20**
   - Image: **WP-15** → **WP-16** (also after WP-21) → **WP-19**
   - Docx: **WP-18**
5. OCR for scanned PDFs: later (not written yet).

**Shared files:** `CompositionRoot.cs` is edited by WP-13, WP-18 and WP-19; the tray menu by WP-17; `release.yml` by WP-18 and WP-22. Whoever merges second runs `git fetch origin` and `git rebase origin/main` on their branch before the PR is reviewed, and re-runs the tests.

## Parallel-safe pairs
Converters (06, 07, 08, 10) never share files and can run at the same time in different tools. App-shell WPs (02, 03, 04, 05, 09, 11) each own separate folders but all touch `WheelConvert.App`: run at most two at once and rebase before the PR.

## WP file template
Goal · Builder/Reviewer · Depends on · Owns · Must not touch · Requirements · Gotchas · Acceptance tests · Review focus · Out of scope · Start prompt.
