# Third-party notices

WheelConvert itself is MIT-licensed (see [`LICENSE`](LICENSE)). The managed libraries below are compiled into the single file `WheelConvert.exe`. The native libraries (`Magick.Native-Q8-x64.dll`, `libSkiaSharp.dll`, `pdfium.dll`) are **separate files next to `WheelConvert.exe`** in the installer, in the portable zip and in the install folder. This is a user decision of 2026-11-17 (WP-12 requirement 2a changed): the LGPL libraries inside `Magick.Native-Q8-x64.dll` must stay replaceable by the user.

**How this file was made (WP-12, 2026-11-17).** Every entry comes from a file inside the package itself (nuspec, `Notice.txt`, `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`), read from the local NuGet cache for the exact version pinned in `src/*/packages.lock.json` and ADR-010. PDFium is the exception: its NuGet package ships no notices, so its licence texts come from the pdfium-binaries release for the same version (section 4). Nothing here comes from PR descriptions, reviews or agent reports. This is an inventory, not legal advice. Items that could not be settled are listed in [Open licence questions](#open-licence-questions-for-the-user).

The release installer and the portable zip include the full texts in a `licenses/` folder (copied by `.github/workflows/release.yml`; a missing file fails the build):

| Shipped file | Copied from |
|---|---|
| `licenses/Magick.NET-Q8-AnyCPU-15.0.0-Notice.txt` | `magick.net-q8-anycpu/15.0.0/Notice.txt` (NuGet cache). Contains the full LGPL-2.0, LGPL-2.1 and LGPL-3.0 texts (and the GPL-3.0 text that LGPL-3.0 refers to) and the MPL-1.1 text. |
| `licenses/Magick.NET.Core-15.0.0-Copyright.txt` | `magick.net.core/15.0.0/Copyright.txt` |
| `licenses/SkiaSharp-4.150.1-LICENSE.txt` | `skiasharp.nativeassets.win32/4.150.1/LICENSE.txt` |
| `licenses/SkiaSharp-4.150.1-THIRD-PARTY-NOTICES.txt` | `skiasharp.nativeassets.win32/4.150.1/THIRD-PARTY-NOTICES.txt` |
| `licenses/Microsoft.Extensions.DependencyInjection.Abstractions-8.0.2-LICENSE.txt` and `-THIRD-PARTY-NOTICES.txt` | that package's `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` |
| `licenses/Microsoft.Extensions.Logging.Abstractions-8.0.3-LICENSE.txt` and `-THIRD-PARTY-NOTICES.txt` | that package's `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` |
| `licenses/System.Security.Cryptography.Pkcs-8.0.1-LICENSE.txt` and `-THIRD-PARTY-NOTICES.txt` | that package's `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` |
| `licenses/PdfPig-0.1.16.nuspec`, `licenses/DocumentFormat.OpenXml-3.5.1.nuspec`, `licenses/DocumentFormat.OpenXml.Framework-3.5.1.nuspec` | that package's `.nuspec` (the only file in each package that names the licence and the copyright holder; none ships a licence text) |
| `licenses/System.IO.Packaging-8.0.1-LICENSE.txt` and `-THIRD-PARTY-NOTICES.txt` | that package's `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` |
| `licenses/pdfium/*` (15 files, listed in section 4) | copied once into the repo (`installer/licenses/pdfium/`) from the pdfium-binaries release, see section 4 |

**Credit lines the licences require.** Both libraries are inside the shipped native DLLs. The sentences are quoted from the files named.

- FreeType (FreeType Project License, which asks for this credit in the documentation): "Portions of this software are copyright © 1996-2002, 2006 The FreeType Project (https://freetype.org).  All rights reserved."
  Source of the wording: `licenses/Magick.NET-Q8-AnyCPU-15.0.0-Notice.txt` lines 2335-2336 (section "[ freetype 2.14.3 (2026-03-22) ]", line 2283, the only shipped file that names a FreeType version): "Portions of this software are copyright © <year> The FreeType Project (https://freetype.org).  All rights reserved." (the URL is split over two lines there). The same sentence with "(www.freetype.org)" is in `licenses/SkiaSharp-4.150.1-THIRD-PARTY-NOTICES.txt` lines 726-727 and `licenses/pdfium/freetype.txt` lines 51-52; this file uses the Magick form. The FTL says to replace `<year>` with "the value from the FreeType version you actually use". None of the three shipped files states a copyright year range for FreeType 2.14.3 itself (checked: the only FreeType year ranges in them are the FTL document's own "Copyright 1996-2002, 2006 by David Turner, Robert Wilhelm, and Werner Lemberg" at Notice.txt line 2290, SkiaSharp notices line 681 and `licenses/pdfium/freetype.txt` line 6, and "1996-2000" in the FTL body). So no year is invented: `<year>` is filled with the range written in the FTL text itself. See open question 6.
- PDFtoImage by David Sungaila (MIT), <https://github.com/sungaila/PDFtoImage>. Source: `pdftoimage.nuspec` in the NuGet package 5.4.0 (`authors` = "David Sungaila", `projectUrl` = "https://github.com/sungaila/PDFtoImage", `licenseUrl` = https://licenses.nuget.org/MIT). The nuspec has no copyright line, so none is quoted.
- libjpeg-turbo (IJG licence, binary-only distribution): "This software is based in part on the work of the Independent JPEG Group." Source: `licenses/SkiaSharp-4.150.1-THIRD-PARTY-NOTICES.txt` lines 1397-1405 ("Your product documentation must include a message stating: ..."); the same clause is in `licenses/pdfium/libjpeg_turbo.md` line 64.

**Not distributed:** FFmpeg. The app only starts an FFmpeg that the user installed separately (ADR-004), so FFmpeg's LGPL/GPL terms do not apply to this download. ZXing.Net is not used and is not in the release.

## 1. Managed packages in the published exe

| Package | Version | Licence (from the package's nuspec) | Source file |
|---|---|---|---|
| Magick.NET-Q8-AnyCPU | 15.0.0 | Apache-2.0; copyright 2013-2026 Dirk Lemstra | `magick.net-q8-anycpu.nuspec` |
| Magick.NET.Core | 15.0.0 | Apache-2.0 | `magick.net.core.nuspec`, `Copyright.txt` |
| PDFsharp | 6.2.4 (exact pin; 6.5.0 and later are AGPL, see ADR-010) | MIT; (c) 2026 empira | `pdfsharp.nuspec` |
| PDFtoImage | 5.4.0 | MIT; PDFtoImage by David Sungaila (MIT), <https://github.com/sungaila/PDFtoImage> | `pdftoimage.nuspec` (`authors`, `projectUrl`) |
| SkiaSharp | 4.150.1 | MIT; (c) Microsoft Corporation | `skiasharp.nuspec`, `LICENSE.txt` |
| SkiaSharp.NativeAssets.Win32 | 4.150.1 | MIT (native `libSkiaSharp.dll`; notices in section 3) | `skiasharp.nativeassets.win32.nuspec`, `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt` |
| bblanchon.PDFium.Win32 | 152.0.7961 | Apache-2.0 (nuspec; native `pdfium.dll`, section 4) | `bblanchon.pdfium.win32.nuspec` |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | MIT | nuspec, `LICENSE.TXT`, `THIRD-PARTY-NOTICES.TXT` (shipped) |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | MIT | nuspec, `LICENSE.TXT`, `THIRD-PARTY-NOTICES.TXT` (shipped) |
| System.Security.Cryptography.Pkcs | 8.0.1 | MIT | nuspec, `LICENSE.TXT`, `THIRD-PARTY-NOTICES.TXT` (shipped) |
| PdfPig | 0.1.16 (exact pin; NuGet ID `PdfPig`, namespace `UglyToad.PdfPig`) | Apache-2.0 (nuspec `license` expression); author UglyToad; <https://github.com/UglyToad/PdfPig>. The package contains no licence or notice file; no dependencies for net8.0 | `pdfpig.nuspec` (shipped) |
| DocumentFormat.OpenXml | 3.5.1 | MIT; Copyright (c) .NET Foundation and Contributors (upstream LICENSE; the nuspec says (c) Microsoft Corporation); <https://github.com/dotnet/Open-XML-SDK>. The package contains no licence file | `documentformat.openxml.nuspec` (shipped) |
| DocumentFormat.OpenXml.Framework | 3.5.1 | MIT; Copyright (c) .NET Foundation and Contributors (same repository and commit as DocumentFormat.OpenXml; the nuspec says (c) Microsoft Corporation). The package contains no licence file | `documentformat.openxml.framework.nuspec` (shipped) |
| System.IO.Packaging | 8.0.1 | MIT | nuspec, `LICENSE.TXT`, `THIRD-PARTY-NOTICES.TXT` (shipped) |

The Linux and macOS native assets that restore also pulls in (bblanchon.PDFium.Linux and .macOS, SkiaSharp.NativeAssets.Linux.NoDependencies and .macOS) are not part of the `win-x64` publish.

MIT and Apache-2.0 require that the licence text and copyright notice travel with the software. Apache-2.0 text: <https://www.apache.org/licenses/LICENSE-2.0>. MIT text: see `LICENSE` (same wording; each package names its own copyright holder above).

### Test-only packages (not distributed)
Microsoft.NET.Test.Sdk 18.11.0 (MIT), xunit 2.9.3 (Apache-2.0), xunit.runner.visualstudio 4.0.1 (Apache-2.0), PDFsharp 6.2.4 (MIT; also used by the Docx tests). Used only by `tests/`; not in the exe, installer or zip.

## 2. Native libraries bundled in Magick.NET: `Magick.Native-Q8-x64.dll`

Source: `Notice.txt` at the root of `magick.net-q8-anycpu` 15.0.0 (428 KB, 39 sections). "Licence" is what the Notice text itself says; where it names none, the row describes the text. "Line" is where the section starts in that file.

The package contains **one** native file for win-x64, `runtimes/win-x64/native/Magick.Native-Q8-x64.dll` (24,753,328 bytes), and no separate DLLs for the libraries below. That file is installed as its own file next to `WheelConvert.exe` (not embedded in the exe). The Notice does not say whether the libraries are linked statically or dynamically; whichever it is, the user can replace the whole DLL (see below).

| Line | Library (version) | Licence per Notice | Obligation |
|---|---|---|---|
| 3 | Magick.NET | Apache-2.0 | Keep notice and licence |
| 8 | ImageMagick 7.1.2-33 | ImageMagick License | Keep notice and licence |
| 117 | aom 3.15.1 | BSD-style, 2 conditions (no name given) | Keep notice, licence, disclaimer |
| 146 | brotli 1.2.0 | MIT text | Keep notice |
| 168 | libbzip2 1.0.8 | bzip2 licence text | Keep notice |
| 212 | cairo 1.18.6 | **MPL-1.1** (only the MPL text is included) | Weak copyleft: changes to cairo files stay under MPL; licence text must accompany |
| 684 | libcroco 0.6.13 | **LGPL-2.0** ("GNU Library GPL v2") | LGPL, see below |
| 1168 | libde265 1.1.3 | **LGPL-3.0** (library; text includes the GPL-3.0 it refers to); sample apps MIT | LGPL, see below |
| 2042 | openexr 3.5.2 | BSD-style, 3 conditions | Keep notice |
| 2056 | libffi 3.8.0 | MIT-style | Keep notice |
| 2080 | fontconfig 2.18.3 | several MIT-style permission notices; Unicode, Inc. data notice | Keep notices |
| 2283 | freetype 2.14.3 | FreeType Project LICENSE (FTL) | FTL credit (quoted above) |
| 2455 | fribidi 1.0.17 | **LGPL-2.1** | LGPL, see below |
| 2959 | gdk-pixbuf 2.44.8 | **LGPL-2.1** | LGPL, see below |
| 3464 | glib 2.64.3 | **LGPL-2.1** | LGPL, see below |
| 3969 | harfbuzz 14.6.0 | "Old MIT" | Keep notice |
| 4014 | libheif 1.23.6 | **LGPL-3.0** (library; text includes the GPL-3.0 it refers to); samples and wrappers MIT | LGPL, see below |
| 4888 | libhwy 1.4.0 | Apache-2.0 or BSD-3-Clause (your choice); one file CC0-1.0 | Keep notice |
| 5262 | imath 3.2.3 | BSD-style, 3 conditions | Keep notice |
| 5276 | libjpeg-turbo 3.2.0 | IJG licence and Modified (3-clause) BSD; zlib licence section | Keep notices; IJG credit (quoted above) |
| 5426 | libjxl 0.12.0 | BSD-style, 3 conditions | Keep notice |
| 5456 | lcms 2.19.1 | MIT | Keep notice |
| 5481 | liblqr 0.4.2 | **LGPL-3.0** | LGPL, see below |
| 5649 | liblzma 5.8.4 | **LGPL-2.1** text (open question 3) | LGPL, see below |
| 6153 | openh264 2.6.0 | BSD-style, 2 conditions (Cisco) | Keep notice |
| 6179 | openjpeg 2.5.4 | 2-clause BSD | Keep notice |
| 6221 | openjph 0.32.0 | BSD 2-Clause | Keep notice |
| 6251 | pango 1.45.3 | **LGPL-2.0** | LGPL, see below |
| 6736 | pixman 0.46.4 | MIT | Keep notice |
| 6781 | libpng 1.6.59 | PNG Reference Library License v2 | Keep notice |
| 6918 | libraqm 0.11.0 | MIT | Keep notice |
| 6943 | libraw 0.22.2 | **LGPL-2.1** (no CDDL text in the Notice; open question 3) | LGPL, see below |
| 7404 | librsvg 2.40.20 | **LGPL-2.0** | LGPL, see below |
| 7889 | libtiff 4.7.2 | LibTIFF licence | Keep notice |
| 7941 | libwebp 1.6.0 | BSD-style, 3 conditions (Google) | Keep notice |
| 7973 | libxml2 2.15.4 | MIT-style | Keep notice |
| 8000 | libzip 1.11.4 | BSD-style, 3 conditions | Keep notice |
| 8034 | zlib 1.3.2 | zlib licence | Keep notice |
| 8059 | zstd 1.5.7 | BSD | Keep notice |

### LGPL and MPL libraries in Magick.Native

**Obligations (from the LGPL texts in the Notice).** Distributing a program that uses an LGPL library means: (a) a notice that the library is used and is covered by the LGPL, (b) the LGPL text (and for LGPL-3.0 the GPL-3.0 text it incorporates), and (c) a way for the user to replace the library with a modified version. (a) and (b) are met by this file and the shipped `licenses/` folder: the Notice contains the full LGPL-2.0, LGPL-2.1 and LGPL-3.0 texts and the MPL-1.1 text. (c) is met by shipping `Magick.Native-Q8-x64.dll` as a separate, replaceable file.

**User decision, 2026-11-17 (WP-12 requirement 2a changed):** ship the native libraries as separate files next to the exe instead of embedding them in the single-file exe, so the LGPL libraries in `Magick.Native-Q8-x64.dll` can be replaced. Whether this is legally sufficient has not been checked by a lawyer (open question 1).

Twelve libraries are weak copyleft. None is GPL-only or AGPL, so none is banned by ADR-005. The versions below are the ones the package's own `Notice.txt` states (section titles `[ name version (date) ]`); the source links point to that exact version and each link was checked on 2026-11-17 to resolve over HTTPS.

| Library | Exact version (Notice line) | Licence per Notice | Source code of that version |
|---|---|---|---|
| cairo | 1.18.6 (212) | MPL-1.1 | <https://www.cairographics.org/releases/cairo-1.18.6.tar.xz>; tagged tree <https://gitlab.freedesktop.org/cairo/cairo/-/tree/1.18.6> |
| libcroco | 0.6.13 (684) | LGPL-2.0 | <https://download.gnome.org/sources/libcroco/0.6/libcroco-0.6.13.tar.xz> |
| libde265 | 1.1.3 (1168) | LGPL-3.0 | <https://github.com/strukturag/libde265/releases/tag/v1.1.3> |
| fribidi | 1.0.17 (2455) | LGPL-2.1 | <https://github.com/fribidi/fribidi/releases/tag/v1.0.17> |
| gdk-pixbuf | 2.44.8 (2959) | LGPL-2.1 | <https://download.gnome.org/sources/gdk-pixbuf/2.44/gdk-pixbuf-2.44.8.tar.xz> |
| glib | 2.64.3 (3464) | LGPL-2.1 | <https://download.gnome.org/sources/glib/2.64/glib-2.64.3.tar.xz> |
| libheif | 1.23.6 (4014) | LGPL-3.0 | <https://github.com/strukturag/libheif/releases/tag/v1.23.6> |
| liblqr | 0.4.2 (5481) | LGPL-3.0 | <https://github.com/carlobaldassi/liblqr/releases/tag/v0.4.2> |
| liblzma (xz) | 5.8.4 (5649) | LGPL-2.1 text (open question 3) | <https://github.com/tukaani-project/xz/releases/tag/v5.8.4> |
| pango | 1.45.3 (6251) | LGPL-2.0 | <https://download.gnome.org/sources/pango/1.45/pango-1.45.3.tar.xz> |
| libraw | 0.22.2 (6943) | LGPL-2.1 text (open question 3) | <https://github.com/LibRaw/LibRaw/releases/tag/0.22.2> |
| librsvg | 2.40.20 (7404) | LGPL-2.0 | <https://download.gnome.org/sources/librsvg/2.40/librsvg-2.40.20.tar.xz> |

All twelve versions are stated in the package's own Notice, so no Magick.Native build files were needed. The links are the upstream projects' official release pages or tarballs. They were not compared byte for byte with what Magick.Native compiled, and Magick.Native may carry patches (the package does not say). Magick.Native's own source: <https://github.com/dlemstra/Magick.Native>.

**How to replace an LGPL library.** `Magick.Native-Q8-x64.dll` sits next to `WheelConvert.exe` in the install folder (installer default: `%LOCALAPPDATA%/Programs/WheelConvert`; zip: the folder you unpacked). Close WheelConvert (tray menu, Exit), then replace that DLL with a compatible build of Magick.Native for Magick.NET 15.0.0 (same file name, 64-bit, Q8) that contains your modified library. The app loads the DLL from its own folder; nothing is embedded in `WheelConvert.exe`. WheelConvert does not check the DLL, so an incompatible build can make image conversion fail.

## 3. Native libraries bundled in SkiaSharp: `libSkiaSharp.dll`

Source: `THIRD-PARTY-NOTICES.txt` in `skiasharp.nativeassets.win32` 4.150.1 ("SkiaSharp and HarfBuzzSharp incorporate third party material from the projects listed below"). The file lists the projects below. It does **not** say which are compiled into the Windows `libSkiaSharp.dll`, or how they are linked. `libSkiaSharp.dll` is a separate file next to the exe, so it is replaceable like the Magick.Native DLL. For the two LGPL/MPL candidates (gif, libmicrohttpd) the file states no version, so no source link can be given: **version not stated by the package, open question 2**. The other entries are permissive (MIT, BSD, Apache-2.0, zlib, libpng, FTL, IJG, Unicode) and need only the notice, which `licenses/SkiaSharp-4.150.1-THIRD-PARTY-NOTICES.txt` provides.

| Project | Licence per the file | Obligation |
|---|---|---|
| ANGLE | BSD (3 conditions) | Keep notice |
| HarfBuzz | MIT-style (Mozilla Foundation copyright shown) | Keep notice |
| skia | BSD-style | Keep notice |
| etc1, piex, sfntly, SPIR-V Tools | Apache-2.0 | Keep notice and licence |
| gif | tri-licence "MPL 1.1 / GPL 2.0 / LGPL 2.1"; version not stated by the package, open question | If MPL-1.1 is chosen: file-level copyleft |
| libpng | libpng licence | Keep notice |
| DNG SDK | text in the file (not summarised here) | Keep notice |
| expat | MIT | Keep notice |
| freetype | FTL | FTL credit (quoted above) |
| ICU | Unicode licence | Keep notice |
| imgui | MIT | Keep notice |
| jsoncpp | text in the file | Keep notice |
| libjpeg-turbo | IJG + BSD | IJG credit (quoted above) |
| libwebp | BSD | Keep notice |
| **libmicrohttpd** | "LGPL and the eCos License" (without HTTPS: your choice; with HTTPS: LGPL); version not stated by the package, open question | LGPL if used (open question 2) |
| SDL | zlib-style | Keep notice |
| SPIR-V Headers | MIT-style (Khronos) | Keep notice |
| zlib | zlib licence | Keep notice |

## 4. PDFium: `pdfium.dll` (package bblanchon.PDFium.Win32 152.0.7961)

The NuGet package ships **no** notice files (ADR-010); its nuspec gives licence expression `Apache-2.0` and copyright "Benoit Blanchon 2017-2025". The licence texts are therefore taken from the pdfium-binaries release that matches this package version (`chromium/7961`), **copied once into this repository** (`installer/licenses/pdfium/`, 15 files) and staged into the installer and the zip as `licenses/pdfium/`:

- Release page: <https://github.com/bblanchon/pdfium-binaries/releases/tag/chromium/7961>
- Archive: `pdfium-win-x64.tgz`, folder `licenses/` inside it. Download: <https://github.com/bblanchon/pdfium-binaries/releases/download/chromium/7961/pdfium-win-x64.tgz>
- Archive SHA-256 (computed after download; equal to the digest GitHub lists for that release asset): `88276459349b291c41f10422dad0210f007c04d919c8fa56472b6b7c6406adf4`
- The archive's `bin/pdfium.dll` has SHA-256 `d3d9f4b7c9dabe3363f30779c5c3c715c47332749fa590e4b4a2b8b6780cb1c4`, identical to `runtimes/win-x64/native/pdfium.dll` in NuGet package 152.0.7961, so these texts belong to the DLL that ships.
- Build scripts of that release: <https://raw.githubusercontent.com/bblanchon/pdfium-binaries/chromium/7961/steps/08-licenses.sh> (collects each `third_party/<name>` licence) and <https://raw.githubusercontent.com/bblanchon/pdfium-binaries/chromium/7961/steps/05-configure.sh> (`is_component_build = false`: the libraries are compiled into the one `pdfium.dll`).
- pdfium-binaries' own `LICENSE` (MIT, "Copyright 2014-2025 Benoit Blanchon") covers the build scripts, not PDFium: <https://raw.githubusercontent.com/bblanchon/pdfium-binaries/chromium/7961/LICENSE>.

Files copied: `pdfium.txt` (PDFium itself), `abseil.txt`, `agg23.txt`, `fast_float.txt`, `freetype.txt` (FTL), `icu.txt`, `lcms.txt`, `libjpeg_turbo.md`, `libjpeg_turbo.ijg`, `libopenjpeg.txt`, `libpng.txt`, `libtiff.txt`, `llvm-libc.txt`, `simdutf.txt`, `zlib.txt`.

I searched these 15 files for "Lesser", "LGPL", "Mozilla Public" and "GNU General". The only hits are in `icu.txt`, lines 462-520: they quote the GPL-2.0/GPL-3.0 with a linking exception for ICU's `aclocal.m4` and `config.guess` build scripts (marked "only for ICU4C"). Those are build-time files, not compiled into `pdfium.dll`. No LGPL or MPL library is listed for PDFium, so there is nothing to source-link for it. This was a text search, not legal advice.

## Open licence questions for the user

1. **Professional advice on the LGPL/MPL libraries in Magick.Native.** Twelve libraries (section 2) sit inside `Magick.Native-Q8-x64.dll`, which now ships as a separate file so the user can replace it. Whether that meets the LGPL terms for each of them has not been checked by a lawyer; the package does not say whether they are linked statically or dynamically and ships no relinking material. Also listed in `docs/STATUS.md`.
2. **libmicrohttpd (LGPL / eCos) and gif (MPL / GPL / LGPL) in SkiaSharp's list.** The file does not say if they are compiled into the Windows `libSkiaSharp.dll`, and states no version for either. Ask the SkiaSharp maintainers. Also listed in `docs/STATUS.md`.
3. **liblzma and libraw licence names.** Accepted as written (user decision 2026-11-17): the Notice includes only an LGPL-2.1 text for liblzma 5.8.4 and libraw 0.22.2, and the full LGPL-2.1 text ships in `licenses/Magick.NET-Q8-AnyCPU-15.0.0-Notice.txt`. From general knowledge (not from the package files): libraw is offered upstream under LGPL-2.1 or CDDL-1.0, and liblzma is not normally LGPL.
4. **SignPath "OSI-approved licence for all components".** See `docs/GO-NO-GO.md`. The repository is public, but the bundled LGPL/MPL libraries should be checked against that condition before applying.
5. **PDFtoImage credit.** Closed (user decision 2026-11-17): the credit line "PDFtoImage by David Sungaila (MIT)" with the project URL is in the credit lines above.
6. **FreeType credit year. Closed (user decision 2026-11-17).** The shipped files state no year range for FreeType 2.14.3, so the FTL's own range "1996-2002, 2006" is kept; no invented years. (Earlier text: The shipped files state no copyright year range for FreeType 2.14.3 itself (details in the FreeType credit line above), so the credit sentence uses the range written in the FTL text. Keep that, or ask the FreeType project which year to use.)

7. **PdfPig and DocumentFormat.OpenXml licence texts (WP-18).** PdfPig (Apache-2.0), DocumentFormat.OpenXml and DocumentFormat.OpenXml.Framework (MIT) ship no licence text in their NuGet packages, only the nuspec with the licence expression and, for the Microsoft packages, the copyright line. The nuspecs are shipped in `licenses/`; the MIT text is in `LICENSE` and the Apache-2.0 text is linked above. Decide whether the upstream licence files should be copied into `installer/licenses/` before the next release.
