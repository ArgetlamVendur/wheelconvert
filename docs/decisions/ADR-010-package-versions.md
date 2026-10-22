# ADR-010: Package version register (living document)
Status: Living · Date: 2026-10-20

The base was created without NuGet access, so **no versions are pinned yet.** The agent implementing each module:
1. Adds the package with the latest **stable** version (`dotnet add package <Name>`), never a preview.
2. Records it below in the same PR, with the licence checked against ADR-005.
3. Commits `packages.lock.json` (lock files are enabled per converter project).

| Package | Project | Version | Licence verified | Added in |
|---|---|---|---|---|
| Magick.NET-Q8-AnyCPU | Converters.Image | _TBD_ | | WP-06 |
| PDFsharp | Converters.Pdf | _TBD_ | | WP-08 |
| PDFtoImage | Converters.Pdf | _TBD_ | | WP-08 |
| ZXing.Net | Converters.Qr | _TBD_ | | WP-10 |
| Microsoft.NET.Test.Sdk | WheelConvert.App.Tests | 18.11.0 | MIT | WP-03 |
| xunit | WheelConvert.App.Tests | 2.9.3 | Apache-2.0 | WP-03 |
| xunit.runner.visualstudio | WheelConvert.App.Tests | 4.0.1 | Apache-2.0 | WP-03 |

Upgrades: one package per PR, note breaking changes, re-run the module's tests.
