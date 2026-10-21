# ARCHITECTURE

## 1. What happens when the user drops a file

```
 Explorer / desktop                         WheelConvert.App (WPF, UI thread)
 ─────────────────                          ─────────────────────────────────────────────
 user holds Shift, drags photo.png ──► [WP-02 ShiftDragTrigger]  or  [WP-03 HotkeyTrigger]
                                              │ ITrigger.WheelRequested(ScreenPoint)
                                              ▼
                                       [WP-04 WheelWindow]  ◄── FormatRegistry.GetTargetsFor(paths)
                                              │ DragEnter/Over: show segments, Effects = Copy
                                              │ Drop on "JPG" → (paths, ConversionTarget)
                                              ▼
                                       [WP-11 DropCoordinator]
                                              │ RequiresInteraction? → [WP-09 IPageOrderPrompt]
                                              │ Task.Run(DropPlanner.Plan(paths, target)) → requests + skipped
                                              ▼
 WheelConvert.Core (worker threads)    ConversionJobQueue.EnqueueBatch(requests)  (per-job temp scope)
                                              │ FormatRegistry.ResolveConverter
                                              ▼
 Converters                            [WP-06 Image] [WP-07 Media] [WP-08 Pdf] [WP-10 Qr]
                                              │ IOutputWriter: CreateTempPath → write → Commit
                                              ▼
                                       photo (converted).jpg  next to the source
                                              │ JobCompleted / JobProgress / BatchCompleted (worker threads)
                                              ▼
                                       [WP-11] marshal to UI → one notification per batch (WP-05) / IClipboardService
```

## 2. Projects and dependencies

```
WheelConvert.App ──► WheelConvert.Core
       │
       ├──► WheelConvert.Converters.Image ──► Core   (+ Magick.NET)
       ├──► WheelConvert.Converters.Media ──► Core   (FFmpeg external)
       ├──► WheelConvert.Converters.Pdf   ──► Core   (+ PDFsharp, PDFtoImage)
       └──► WheelConvert.Converters.Qr    ──► Core   (+ ZXing.Net, Magick.NET for decoding pixels)

tests/WheelConvert.Core.Tests ──► Core
```

Rules:
- **Arrows only point to Core.** Converters never reference each other or the App.
- Only the App references Windows-only APIs (`net8.0-windows`). **Converters target plain `net8.0`** (Magick.NET, PDFsharp, PDFtoImage, ZXing.Net and FFmpeg all run cross-platform), contain no UI code, and their tests run on Linux or Windows.
- The App's composition root (`WheelConvert.App/Composition/CompositionRoot.cs`, WP-11) is the only place that knows every module.

## 3. Threads

| Thread | Runs | Must not |
|---|---|---|
| UI (WPF dispatcher) | triggers' events, wheel, tray, prompts, notifications | block > 50 ms, do file conversion |
| Low-level hook callback (on UI thread, inside the message loop) | WP-02 gesture detection | allocate heavily, log, do I/O, or take > a few ms (Windows silently removes slow hooks) |
| Queue workers (thread pool, `MaxConcurrentJobs`) | converters | touch WPF objects (marshal via Dispatcher) |
| External processes | FFmpeg | run without timeout/cancellation (`ProcessRunner`) |

## 4. Stages: the app runs at every stage

Converters start as stubs whose `CheckHealth()` returns `NotImplemented`, so their targets don't appear. The app is usable as soon as WP-02/03 + WP-04 + WP-05 + WP-11 exist, and each converter "lights up" its segments when implemented. Order of value:

1. **Shell** — WP-05 tray + WP-03 hotkey + WP-04 wheel + WP-11 integration → wheel opens, shows "Nothing available yet".
2. **First conversion** — WP-06 image.
3. **Gesture** — WP-02 Shift-drag (riskiest; hotkey keeps the app usable if it fails).
4. **Breadth** — WP-07 media, WP-08 PDF, WP-10 QR.
5. **Polish** — WP-09 reorder window, WP-12 release.

## 5. Files the app creates

| Path | Purpose |
|---|---|
| `%LOCALAPPDATA%\WheelConvert\settings.json` | settings (`JsonSettingsStore`) |
| `%LOCALAPPDATA%\WheelConvert\wheelconvert.log` (+ `.1`) | log (`FileLog`) |
| `<source folder>\<stem> (<label>)<ext>` | outputs |
| `<source folder>\.~wc-<guid><ext>` | in-progress temp files (removed on success/failure; stale ones cleaned on start-up) |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\WheelConvert` | only if "Start with Windows" is on (WP-05) |
