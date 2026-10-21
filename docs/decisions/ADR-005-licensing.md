# ADR-005: Licensing
Status: Accepted · Date: 2026-10-20

## Decision
Project licence: **MIT**.

| Use | Library | Licence | Status |
|---|---|---|---|
| Images (incl. HEIC/WebP/ICO) | Magick.NET (Q8, AnyCPU or x64) | Apache-2.0 (Magick.NET) + ImageMagick licence; **bundled native delegates carry their own licences** — HEIC support comes from libheif/libde265, believed LGPL-3.0 (verify from the package notices) | Allowed; LGPL notices required (WP-12) |
| PDF edit (split/rotate/reorder) | PDFsharp 6.x | MIT | Allowed |
| PDF render (thumbnails, to-images) | PDFtoImage (wraps PDFium + SkiaSharp) | MIT; PDFium BSD-3/Apache-2.0, bundling FreeType (FTL, needs credit), ICU, libjpeg-turbo | Allowed; notices required (WP-12) |
| QR decoding | ZXing.Net | Apache-2.0 | Allowed |
| Audio/video | FFmpeg (external, not bundled — ADR-004) | LGPL/GPL | Allowed as external tool only |
| — | Ghostscript, iText 7, MuPDF | AGPL | **Banned** |
| — | Anything GPL linked into the app | GPL | **Banned** |
| — | Packages with no licence or "free for non-commercial" only | — | **Banned** |

## Rules
- Adding any package: check its licence and its native dependencies' licences, record the package + version + licences in **ADR-010** (the only file converter WPs edit for this). WP-12 compiles `THIRD-PARTY-NOTICES.md` from ADR-010. This table is updated only by an ADR that supersedes this one.
- Licence notes above marked "believed" were written without internet access: WP-12 verifies them from the packages' own files before release.
- HEIC decoding relies on libheif inside Magick.NET; HEVC is patent-encumbered in some countries. Don't advertise HEIC in marketing text; mention it neutrally in the README.
