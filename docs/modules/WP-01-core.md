# WP-01: Core (contracts and pure logic)

**Status:** DONE in the base (2026-10-20). Changes here are contract changes: user approval + ADR.
**Owns:** `src/WheelConvert.Core/**`, `tests/WheelConvert.Core.Tests/**`, `docs/CONTRACTS.md`

## What exists
| Area | Type | Purpose |
|---|---|---|
| Contracts | `IConverter`, `ConverterHealth`, `IOutputWriter`, `ITrigger`, `INotifier`, `IClipboardService`, `IPageOrderPrompt`, `IPdfPageRenderer`, `ILog`, `ConversionRequest`, `ConversionResult`, `ConversionTarget`, `FileKind` | Interfaces every module codes against |
| Formats | `FileClassifier`, `TargetCatalog`, `FormatRegistry` | Extension → kind; all target IDs; which segments to show and which converter runs them |
| Output | `SafeOutputWriter`, `OutputLabels` | Never-overwrite temp + commit; standard labels |
| Jobs | `DropPlanner`, `ConversionJobQueue` | Drop → requests; background execution with cancellation |
| Wheel | `WheelGeometry` | Segment hit-testing and arc maths |
| Settings | `AppSettings`, `JsonSettingsStore` | JSON settings with clamping and corrupt-file recovery |
| Infrastructure | `ProcessRunner`, `ToolLocator`, `FileLog` | Safe external processes, finding FFmpeg, logging |

## Verified
Built and tested on Linux with .NET 8.0.132: 0 warnings, all tests passing (`dotnet run --project tests/WheelConvert.Core.Tests`). Independent review: `docs/reviews/`.

## When to touch Core
Only when a WP genuinely can't be done with the current contracts. Write the proposal (what, why, which WPs are affected) in the PR, get user approval, add an ADR, update `docs/CONTRACTS.md` and the tests in the same PR.
