# ADR-010: Package version register (living document)
Status: Living · Date: 2026-10-20

The base was created without NuGet access, so **no versions are pinned yet.** The agent implementing each module:
1. Adds the package with the latest **stable** version (`dotnet add package <Name>`), never a preview.
2. Records it below in the same PR, with the licence checked against ADR-005.
3. Commits `packages.lock.json` (lock files are enabled per converter project).

| Package | Project | Version | Licence verified | Added in |
|---|---|---|---|---|
| Magick.NET-Q8-AnyCPU | Converters.Image | 15.0.0 | Apache-2.0 (nuspec), 2026-11-02. Brings Magick.NET.Core 15.0.0 (Apache-2.0) and ImageMagick (Apache-2.0-compatible). Bundled native delegates carry permissive and LGPL licences verified from Notice.txt: libheif 1.23.6 (LGPL-3.0), libde265 1.1.3 (LGPL-3.0), libwebp 1.6.0 (BSD-3), libpng 1.6.59 (libpng/PNG Ref Lib v2), libjpeg-turbo 3.2.0 (IJG/BSD-3/zlib), freetype 2.14.3 (FTL/GPL-2.0), harfbuzz 14.6.0 (MIT), brotli 1.2.0 (MIT), zlib 1.3.2 (zlib), zstd 1.5.7 (BSD-3), aom 3.15.1 (BSD-2), openjpeg 2.5.4 (BSD-2), libtiff 4.7.2 (libtiff) | WP-06 |
| Microsoft.NET.Test.Sdk | Converters.Image.Tests (test only, not shipped) | 18.11.0 | MIT | WP-06 |
| xunit | Converters.Image.Tests (test only, not shipped) | 2.9.3 | Apache-2.0 | WP-06 |
| xunit.runner.visualstudio | Converters.Image.Tests (test only, not shipped) | 4.0.1 | Apache-2.0 | WP-06 |
| PDFsharp | Converters.Pdf | 6.2.4 (exact pin `[6.2.4]`) | MIT (nuspec), 2026-10-22. **Not latest:** 6.5.0 (2026-10-14) and later are AGPL-3.0-only, banned by ADR-005; do not upgrade without a licence re-check. Dependencies Microsoft.Extensions.Logging.Abstractions 8.0.3, System.Security.Cryptography.Pkcs 8.0.1: MIT | WP-08 |
| PDFtoImage | Converters.Pdf | 5.4.0 | MIT (nuspec), 2026-10-22. Brings SkiaSharp 4.150.1 + NativeAssets.Win32/Linux.NoDependencies/macOS (MIT; natives ship THIRD-PARTY-NOTICES.txt) and bblanchon.PDFium.* 152.0.7961 (package metadata Apache-2.0; the package ships **no** licence/notice files, so PDFium's BSD-3 and its FreeType/ICU/libjpeg-turbo notices are unverified: WP-12 must take them from the pdfium-binaries project) | WP-08 |
| Microsoft.NET.Test.Sdk | Converters.Pdf.Tests (test only, not shipped) | 18.11.0 | MIT (nuspec), 2026-10-22 | WP-08 |
| xunit | Converters.Pdf.Tests (test only, not shipped) | 2.9.3 | Apache-2.0 (nuspec), 2026-10-22 | WP-08 |
| xunit.runner.visualstudio | Converters.Pdf.Tests (test only, not shipped) | 4.0.1 | Apache-2.0 (nuspec), 2026-10-22 | WP-08 |
| ZXing.Net | Converters.Qr | _TBD_ | | WP-10 |
| Microsoft.NET.Test.Sdk | WheelConvert.App.Tests | 18.11.0 | MIT | WP-03 |
| xunit | WheelConvert.App.Tests | 2.9.3 | Apache-2.0 | WP-03 |
| xunit.runner.visualstudio | WheelConvert.App.Tests | 4.0.1 | Apache-2.0 | WP-03 |

Upgrades: one package per PR, note breaking changes, re-run the module's tests.
