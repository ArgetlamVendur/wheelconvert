# Third-party notices

WheelConvert itself is MIT-licensed (see [`LICENSE`](LICENSE)). The released `WheelConvert.exe` is a single file that also contains the libraries below.

**How this file was made (WP-12, 2026-11-17).** Every entry comes from a file inside the package itself (nuspec, `Notice.txt`, `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt`), read from the local NuGet cache for the exact version pinned in `src/*/packages.lock.json` and ADR-010. PDFium is the exception: its NuGet package ships no notices, so that section is taken from the pdfium-binaries repository (URLs below). Nothing here comes from PR descriptions, reviews or agent reports. This is an inventory, not legal advice. Items that could not be settled are listed in [Open licence questions](#open-licence-questions-for-the-user).

The release installer and the portable zip include the full texts in a `licenses\` folder (copied from the packages by `.github/workflows/release.yml`):

| Shipped file | Copied from |
|---|---|
| `licenses/Magick.NET-Q8-AnyCPU-15.0.0-Notice.txt` | `magick.net-q8-anycpu/15.0.0/Notice.txt` |
| `licenses/Magick.NET.Core-15.0.0-Copyright.txt` | `magick.net.core/15.0.0/Copyright.txt` |
| `licenses/SkiaSharp-4.150.1-LICENSE.txt` | `skiasharp.nativeassets.win32/4.150.1/LICENSE.txt` |
| `licenses/SkiaSharp-4.150.1-THIRD-PARTY-NOTICES.txt` | `skiasharp.nativeassets.win32/4.150.1/THIRD-PARTY-NOTICES.txt` |

PDFium's own licence texts are **not** in that folder yet (open question 4).

**Not distributed:** FFmpeg. The app only starts an FFmpeg that the user installed separately (ADR-004), so FFmpeg's LGPL/GPL terms do not apply to this download. ZXing.Net is not used and is not in the release.

## 1. Managed packages in the published exe

| Package | Version | Licence (from the package's nuspec) | Source file |
|---|---|---|---|
| Magick.NET-Q8-AnyCPU | 15.0.0 | Apache-2.0; copyright 2013-2026 Dirk Lemstra | `magick.net-q8-anycpu.nuspec` |
| Magick.NET.Core | 15.0.0 | Apache-2.0 | `magick.net.core.nuspec`, `Copyright.txt` |
| PDFsharp | 6.2.4 (exact pin; 6.5.0 and later are AGPL, see ADR-010) | MIT; (c) 2026 empira | `pdfsharp.nuspec` |
| PDFtoImage | 5.4.0 | MIT | `pdftoimage.nuspec` |
| SkiaSharp | 4.150.1 | MIT; (c) Microsoft Corporation | `skiasharp.nuspec`, `LICENSE.txt` |
| SkiaSharp.NativeAssets.Win32 | 4.150.1 | MIT (native `libSkiaSharp.dll`; notices in section 3) | `skiasharp.nativeassets.win32.nuspec`, `LICENSE.txt`, `THIRD-PARTY-NOTICES.txt` |
| bblanchon.PDFium.Win32 | 152.0.7961 | Apache-2.0 (nuspec; native `pdfium.dll`, section 4) | `bblanchon.pdfium.win32.nuspec` |
| Microsoft.Extensions.DependencyInjection.Abstractions | 8.0.2 | MIT | nuspec |
| Microsoft.Extensions.Logging.Abstractions | 8.0.3 | MIT | nuspec |
| System.Security.Cryptography.Pkcs | 8.0.1 | MIT | nuspec |

The Linux and macOS native assets that restore also pulls in (bblanchon.PDFium.Linux and .macOS, SkiaSharp.NativeAssets.Linux and .macOS) are not part of the `win-x64` publish.

MIT and Apache-2.0 require that the licence text and copyright notice travel with the software. Apache-2.0 text: <https://www.apache.org/licenses/LICENSE-2.0>. MIT text: see `LICENSE` (same wording; each package names its own copyright holder above).

### Test-only packages (not distributed)
Microsoft.NET.Test.Sdk 18.11.0 (MIT), xunit 2.9.3 (Apache-2.0), xunit.runner.visualstudio 4.0.1 (Apache-2.0). Used only by `tests/`; not in the exe, installer or zip.

## 2. Native libraries bundled in Magick.NET: `Magick.Native-Q8-x64.dll`

Source: `Notice.txt` at the root of `magick.net-q8-anycpu` 15.0.0 (428 KB, 39 sections). "Licence" is what the Notice text itself says; where it names none, the row describes the text. "Line" is where the section starts in that file.

The package contains **one** native file for win-x64, `runtimes/win-x64/native/Magick.Native-Q8-x64.dll` (24,753,328 bytes), and no separate DLLs for the libraries below. The Notice does **not** say whether they are linked statically or dynamically (open question 1).

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
| 2283 | freetype 2.14.3 | FreeType Project LICENSE (FTL) | FTL credit (see the FTL text in the Notice) |
| 2455 | fribidi 1.0.17 | **LGPL-2.1** | LGPL, see below |
| 2959 | gdk-pixbuf 2.44.8 | **LGPL-2.1** | LGPL, see below |
| 3464 | glib 2.64.3 | **LGPL-2.1** | LGPL, see below |
| 3969 | harfbuzz 14.6.0 | "Old MIT" | Keep notice |
| 4014 | libheif 1.23.6 | **LGPL-3.0** (library; text includes the GPL-3.0 it refers to); samples and wrappers MIT | LGPL, see below |
| 4888 | libhwy 1.4.0 | Apache-2.0 or BSD-3-Clause (your choice); one file CC0-1.0 | Keep notice |
| 5262 | imath 3.2.3 | BSD-style, 3 conditions | Keep notice |
| 5276 | libjpeg-turbo 3.2.0 | IJG licence and Modified (3-clause) BSD; zlib licence section | Keep notices; the IJG text asks for a credit in documentation |
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

**LGPL obligations (from the LGPL texts in the Notice).** Distributing a program that uses an LGPL library means: (a) a notice that the library is used and is covered by the LGPL, (b) the LGPL text (and for LGPL-3.0 the GPL-3.0 text it incorporates), and (c) a way for the user to replace the library with a modified version. The Notice's LGPL-2.1 text asks, for static linking, for object files or an equivalent so the user can relink; the LGPL-3.0 text asks for the "Minimal Corresponding Source" or a suitable shared-library mechanism. Which applies depends on how the libraries are linked into `Magick.Native-Q8-x64.dll`, which the package files do not say.

**Twelve libraries are weak copyleft:** cairo (MPL-1.1); libcroco, pango, librsvg (LGPL-2.0); fribidi, gdk-pixbuf, glib, liblzma, libraw (LGPL-2.1); libde265, libheif, liblqr (LGPL-3.0). None is GPL-only or AGPL, so none is banned by ADR-005. Parts (a) and (b) are met by shipping this file and the `licenses/` folder. Part (c) is open (question 1).

## 3. Native libraries bundled in SkiaSharp: `libSkiaSharp.dll`

Source: `THIRD-PARTY-NOTICES.txt` in `skiasharp.nativeassets.win32` 4.150.1 ("SkiaSharp and HarfBuzzSharp incorporate third party material from the projects listed below"). The file lists the projects below. It does **not** say which are compiled into the Windows `libSkiaSharp.dll`, or how they are linked.

| Project | Licence per the file | Obligation |
|---|---|---|
| ANGLE | BSD (3 conditions) | Keep notice |
| HarfBuzz | MIT-style (Mozilla Foundation copyright shown) | Keep notice |
| skia | BSD-style | Keep notice |
| etc1, piex, sfntly, SPIR-V Tools | Apache-2.0 | Keep notice and licence |
| gif | tri-licence "MPL 1.1 / GPL 2.0 / LGPL 2.1" | If MPL-1.1 is chosen: file-level copyleft |
| libpng | libpng licence | Keep notice |
| DNG SDK | text in the file (not summarised here) | Keep notice |
| expat | MIT | Keep notice |
| freetype | FTL | FTL credit |
| ICU | Unicode licence | Keep notice |
| imgui | MIT | Keep notice |
| jsoncpp | text in the file | Keep notice |
| libjpeg-turbo | IJG + BSD | IJG credit |
| libwebp | BSD | Keep notice |
| **libmicrohttpd** | "LGPL and the eCos License" (without HTTPS: your choice; with HTTPS: LGPL) | LGPL if used (open question 2) |
| SDL | zlib-style | Keep notice |
| SPIR-V Headers | MIT-style (Khronos) | Keep notice |
| zlib | zlib licence | Keep notice |

## 4. PDFium: `pdfium.dll` (package bblanchon.PDFium.Win32 152.0.7961)

The NuGet package contains only `runtimes/{win-x64,win-x86,win-arm64}/native/pdfium.dll`, `README.md`, `icon.png` and the nuspec (licence expression `Apache-2.0`, copyright "Benoit Blanchon 2017-2025"). It ships **no** notice files (ADR-010). Taken instead from the pdfium-binaries project at the tag that matches this version (`chromium/7961`), read 2026-11-17:

- Repository at that tag: <https://github.com/bblanchon/pdfium-binaries/tree/chromium/7961>. Root files: `LICENSE`, `README.md`, `build.sh`, `steps/`, `patches/`, `nuget/` and others; no third-party notice files in the repository.
- `LICENSE` at that tag: <https://raw.githubusercontent.com/bblanchon/pdfium-binaries/chromium/7961/LICENSE>. It is an **MIT** licence, "Copyright 2014-2025 Benoit Blanchon". It covers the pdfium-binaries build scripts, not PDFium itself.
- Licence collection script: <https://raw.githubusercontent.com/bblanchon/pdfium-binaries/chromium/7961/steps/08-licenses.sh>. For each `third_party/<name>` found in the build it copies that library's licence into a `licenses/` folder of the release archive. Names it can collect: pdfium (PDFium's own `LICENSE`), abseil, agg23, bigint, catapult, cpu_features, fast_float, fp16, freetype (FTL), highway, icu, lcms, libpng, libjpeg_turbo (`.md` and `.ijg`), libopenjpeg, libtiff, libunwind, llvm-libc, simdutf, zlib; with V8 also v8, fdlibm, strongtalk. It skips nasm, googletest, test_fonts, libc, vinn.
- Build settings: <https://raw.githubusercontent.com/bblanchon/pdfium-binaries/chromium/7961/steps/05-configure.sh> writes `is_component_build = false` and `pdf_enable_v8 = false` unless V8 is requested, and never sets `use_system_*`. So the third-party libraries are compiled into the one `pdfium.dll`, not shipped as separate DLLs. The project offers V8 as separate "PDFium V8" packages; this app uses the non-V8 package.

Which libraries are in this exact `pdfium.dll` is **not** recorded in the repository files; the release archive's `licenses/` folder would show it. No LGPL or GPL library appears in the script's list. I did not read the individual licence texts (open question 4).

## Open licence questions for the user

1. **Magick.Native LGPL libraries and relinking.** Twelve libraries under LGPL-2.0/2.1/3.0 or MPL-1.1 sit inside a single `Magick.Native-Q8-x64.dll`, and that DLL is embedded in the single-file exe (`IncludeNativeLibrariesForSelfExtract`), so a user cannot simply swap it. The package files do not say whether the LGPL libraries are linked statically or dynamically, and contain no relinking material (object files, build recipe). Options to decide before release: (a) ship `Magick.Native-Q8-x64.dll` as a separate file next to the exe so it can be replaced (this changes WP-12 requirement 2a), (b) add a written offer or pointer to the Magick.Native source and build instructions, (c) drop the LGPL-backed formats (for example HEIC) from the release. Not decided here.
2. **libmicrohttpd (LGPL / eCos) and gif (MPL / GPL / LGPL) in SkiaSharp's list.** The file does not say if they are compiled into the Windows `libSkiaSharp.dll`. If they are, LGPL/MPL terms apply. Needs confirmation from the SkiaSharp project.
3. **liblzma and libraw licence names.** The Notice includes only an LGPL-2.1 text for liblzma 5.8.4 and libraw 0.22.2. From general knowledge (not from the package files): libraw is offered upstream under LGPL-2.1 or CDDL-1.0, and liblzma is not normally LGPL. The Notice text is treated as binding here. Confirm with the Magick.Native maintainers if a narrower list is wanted.
4. **PDFium notices are not shipped yet.** The licence texts (PDFium, FreeType FTL, ICU, libjpeg-turbo and others) exist only in the pdfium-binaries release archive's `licenses/` folder, which was not downloaded. Decide whether the release workflow should fetch `pdfium-win-x64.tgz` for tag `chromium/7961` and copy its `licenses/` folder (needs a checksum pin), or whether the texts should be copied into the repo by hand.
5. **Copyright holder** in `LICENSE` is a placeholder.
