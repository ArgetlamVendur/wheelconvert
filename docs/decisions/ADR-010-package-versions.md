# ADR-010: Package version register (living document)
Status: Living · Date: 2026-10-20

The base was created without NuGet access; versions are pinned per module as it lands. The agent implementing each module:
1. Adds the package with the latest **stable** version (`dotnet add package <Name>`), never a preview.
2. Records it below in the same PR, with the licence checked against ADR-005.
3. Commits `packages.lock.json` (lock files are enabled per converter project).

| Package | Project | Version | Licence verified | Added in |
|---|---|---|---|---|
| Magick.NET-Q8-AnyCPU | Converters.Image | 15.0.0 | Apache-2.0 (nuspec), 2026-11-02. Brings Magick.NET.Core 15.0.0 (Apache-2.0). The native library bundles ImageMagick plus 37 third-party libraries, 12 of them under LGPL (2.0, 2.1, 3.0) or MPL-1.1: **see the table below** (re-checked against the package Notice.txt on 2026-11-10) | WP-06 |
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

## Magick.NET-Q8-AnyCPU 15.0.0: native libraries in the package `Notice.txt` (WP-06)
Source: `Notice.txt` at the root of the `magick.net-q8-anycpu` 15.0.0 NuGet package, read 2026-11-10. One row per `[ … ]` section, in file order (39 sections). "Licence" is what the Notice text itself says; where the text carries no licence name, the row says so and describes the text. Nothing here is a legal opinion: WP-12 builds the shipped notices from this table and the Notice file itself (ADR-005).

| # | Library (as in Notice) | Licence text in Notice | Kind |
|---|---|---|---|
| 1 | Magick.NET | "Licensed under the Apache License, Version 2.0." | permissive |
| 2 | ImageMagick 7.1.2-33 | "ImageMagick License" (https://imagemagick.org/license/) | permissive |
| 3 | aom 3.15.1 | No licence name; BSD-style text with 2 numbered conditions (Alliance for Open Media) | permissive |
| 4 | brotli 1.2.0 | No licence name; MIT text ("Permission is hereby granted, free of charge…") | permissive |
| 5 | libbzip2 1.0.8 | No licence name; bzip2 licence text with 4 numbered conditions | permissive |
| 6 | cairo 1.18.6 | "MOZILLA PUBLIC LICENSE Version 1.1" (only MPL-1.1 text is included; upstream cairo is LGPL-2.1 or MPL-1.1) | **weak copyleft** |
| 7 | libcroco 0.6.13 | "GNU LIBRARY GENERAL PUBLIC LICENSE Version 2, June 1991" (LGPL-2.0) | **weak copyleft** |
| 8 | libde265 1.1.3 | Library: "GNU Lesser General Public License"; text included is "GNU LESSER GENERAL PUBLIC LICENSE Version 3, 29 June 2007" plus the GNU GPL v3 text it incorporates. Sample applications: MIT | **weak copyleft** |
| 9 | openexr 3.5.2 | No licence name; BSD-style text with 3 numbered conditions incl. "Neither the name…" | permissive |
| 10 | libffi 3.8.0 | No licence name; MIT-style text ("Permission is hereby granted, free of charge…") | permissive |
| 11 | fontconfig 2.18.3 | No single licence name; several permission notices (MIT-style and "Permission to use, copy, modify, distribute, and sell…") plus a Unicode, Inc. data notice pointing to unicode.org terms of use | permissive |
| 12 | freetype 2.14.3 | "The FreeType Project LICENSE" (FTL). The Notice includes only the FTL text, no GPL text | permissive |
| 13 | fribidi 1.0.17 | "GNU LESSER GENERAL PUBLIC LICENSE Version 2.1, February 1999" | **weak copyleft** |
| 14 | gdk-pixbuf 2.44.8 | "GNU LESSER GENERAL PUBLIC LICENSE Version 2.1, February 1999" | **weak copyleft** |
| 15 | glib 2.64.3 | "GNU LESSER GENERAL PUBLIC LICENSE Version 2.1, February 1999" | **weak copyleft** |
| 16 | harfbuzz 14.6.0 | licensed under the so-called "Old MIT" license | permissive |
| 17 | libheif 1.23.6 | Library: "GNU Lesser General Public License"; text included is "GNU LESSER GENERAL PUBLIC LICENSE Version 3, 29 June 2007" plus the GNU GPL v3 text it incorporates. Samples and wrappers: MIT | **weak copyleft** |
| 18 | libhwy 1.4.0 | "dual-licensed under your choice of either the Apache License 2.0 or the BSD 3-Clause License"; one file (`hwy/contrib/random/random-inl.h`) "CC0 1.0 Universal" | permissive |
| 19 | imath 3.2.3 | No licence name; BSD-style text with 3 numbered conditions incl. "Neither the name…" | permissive |
| 20 | libjpeg-turbo 3.2.0 | "two compatible BSD-style open source licenses": the IJG License and the Modified (3-clause) BSD License; also a zlib License section | permissive |
| 21 | libjxl 0.12.0 | No licence name; BSD-style text with 3 numbered conditions incl. "Neither the name…" | permissive |
| 22 | lcms 2.19.1 | "MIT License" | permissive |
| 23 | liblqr 0.4.2 | "GNU LESSER GENERAL PUBLIC LICENSE Version 3, 29 June 2007" | **weak copyleft** |
| 24 | liblzma 5.8.4 | "GNU LESSER GENERAL PUBLIC LICENSE Version 2.1, February 1999" | **weak copyleft** |
| 25 | openh264 2.6.0 | No licence name; BSD-style text with 2 numbered conditions (Cisco Systems) | permissive |
| 26 | openjpeg 2.5.4 | "the 2-clauses BSD License" | permissive |
| 27 | openjph 0.32.0 | "BSD 2-Clause License" | permissive |
| 28 | pango 1.45.3 | "GNU LIBRARY GENERAL PUBLIC LICENSE Version 2, June 1991" (LGPL-2.0) | **weak copyleft** |
| 29 | pixman 0.46.4 | "the MIT license, agreed upon by most contributors" | permissive |
| 30 | libpng 1.6.59 | "PNG Reference Library License version 2" | permissive |
| 31 | libraqm 0.11.0 | "The MIT License (MIT)" | permissive |
| 32 | libraw 0.22.2 | "GNU LESSER GENERAL PUBLIC LICENSE Version 2.1, February 1999". No CDDL text in the Notice (upstream LibRaw is dual LGPL-2.1 / CDDL-1.0) | **weak copyleft** |
| 33 | librsvg 2.40.20 | "GNU LIBRARY GENERAL PUBLIC LICENSE Version 2, June 1991" (LGPL-2.0) | **weak copyleft** |
| 34 | libtiff 4.7.2 | "LibTIFF license" | permissive |
| 35 | libwebp 1.6.0 | No licence name; BSD-style text with 3 conditions incl. "Neither the name of Google…" | permissive |
| 36 | libxml2 2.15.4 | No licence name; MIT-style text ("Permission is hereby granted, free of charge…") | permissive |
| 37 | libzip 1.11.4 | No licence name; BSD-style text with 3 numbered conditions ("The names of the authors may not be used…") | permissive |
| 38 | zlib 1.3.2 | No licence name; zlib licence text ("provided 'as-is'…") | permissive |
| 39 | zstd 1.5.7 | "BSD License" (3 conditions incl. "Neither the name…") | permissive |

Rows 1–2 are Magick.NET and ImageMagick themselves; rows 3–39 are the 37 third-party libraries. **Weak copyleft (12):** cairo (MPL-1.1), libcroco, pango, librsvg (LGPL-2.0), fribidi, gdk-pixbuf, glib, liblzma, libraw (LGPL-2.1), libde265, libheif, liblqr (LGPL-3.0). None is GPL- or AGPL-only, so none is banned by ADR-005, but their notice and source-availability terms are for WP-12 to meet when packaging.

