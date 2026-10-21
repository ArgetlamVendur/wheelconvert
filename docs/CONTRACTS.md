# CONTRACTS — frozen interfaces between modules

Every module codes against these. **Changing anything here needs user approval** and an ADR in `docs/decisions/`.
Source of truth is the code in `src/WheelConvert.Core/Contracts/` and `src/WheelConvert.Core/Formats/TargetCatalog.cs`; this file explains the rules the code can't enforce.

---

## C1. `IConverter` (implemented by WP-06, 07, 08, 10)

| Member | Rule |
|---|---|
| `Id` | Unique, lower-case, stable (`image`, `media`, `pdf`, `qr`). |
| `SupportedTargetIds` | Only IDs that exist in `TargetCatalog`. Unknown IDs are ignored with a warning. |
| `CanConvert(ext, targetId)` | Pure and fast (called while the wheel is open). `ext` is normalised: lower-case, leading dot. |
| `CheckHealth()` | Fast (aim < 50 ms; a one-time native library load or a PATH on network drives may take longer — the app always runs health checks off the UI thread, at start-up and on "Check again", never while the wheel is open), no user-file conversion. Stubs return `ConverterHealth.NotImplemented`, which hides their targets. Missing tool → `ConverterHealth.Missing("…how to fix…")`. |
| `ConvertAsync(...)` | See safety rules S1–S6. Returns a `ConversionResult`; never throws except `OperationCanceledException`. Must be safe to run concurrently for different requests. `IOutputWriter.Commit` can throw `IOException`, `UnauthorizedAccessException`, `InvalidOperationException`, `FileNotFoundException`: **catch them and return `Fail(OutputFailed, …)`**. Report progress 0..1 through the `IProgress<double>` when you can (the queue throttles it). |
| Output label | Plain format conversions use `request.OutputLabel` (from `OptionKeys.OutputLabel`, default "converted"). Operations use the fixed labels in C2. |

**Result rules**
- File output: `ConversionResult.Ok(paths)` with every committed path, in a sensible order (page 1 first).
- Text output (QR): `ConversionResult.OkText(text)`; no files.
- Failure: `ConversionResult.Fail(error, message)`, or `Fail(error, message, committedPaths)` when some pieces were already written. `message` is shown to the user: plain language, ≤ 120 characters, no stack traces.
- Nothing found (no QR code): `Fail(NoResult, …)`.

| Situation | `ConversionError` |
|---|---|
| Source vanished | `SourceMissing` |
| Format/variant not handled | `Unsupported` |
| FFmpeg etc. not found | `ToolMissing` |
| External tool exited non-zero | `ToolFailed` (put the last stderr line in the log, not the message) |
| Encrypted PDF | `PasswordProtected` |
| File can't be decoded | `CorruptInput` |
| Folder not writable / disk full | `OutputFailed` |
| Cancellation token fired | let `OperationCanceledException` propagate (queue maps it to `Cancelled`) |
| Anything else | `Unknown` |

## C2. Output safety (all converters) — **non-negotiable**

- **S1.** Open the source **read-only**. Never move, rename, delete, or write to it.
- **S2.** Create output **only** at a path from `IOutputWriter.CreateTempPath(source, ext)`.
- **S3.** Publish **only** via `IOutputWriter.Commit(temp, source, ext, label)`. It never overwrites; it adds " 2", " 3"… to the label.
- **S4.** On every failure or cancellation path, call `IOutputWriter.Discard(temp)` for each temp file (use `try/finally`).
- **S5.** Multi-output operations (split, PDF→images) commit each piece with its own label (`"page 3"`, `"part 1"`). If one piece fails, discard the remaining temps, keep the pieces already committed, and return `Fail` with a message naming how many were written.
- **S6.** Never write anywhere except the source's folder. If that folder is read-only, return `OutputFailed`.

The writer enforces what it can: it only commits/discards temps **it issued**, only into the **same folder** as the source, and the queue gives every job its own writer scope that **discards any uncommitted temp when the job ends** (even if the converter crashed). Converters still follow S4 themselves.

Reference pattern (single output):

```csharp
var ext = request.Target.OutputExtension!;
var temp = output.CreateTempPath(request.SourcePath, ext);
var committed = false;
try
{
    await WriteConvertedFileAsync(request.SourcePath, temp, cancellationToken); // your code; opens source read-only
    var final = output.Commit(temp, request.SourcePath, ext, request.OutputLabel);
    committed = true;
    return ConversionResult.Ok(final);
}
catch (OperationCanceledException) { throw; }                       // queue reports Cancelled
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    return ConversionResult.Fail(ConversionError.OutputFailed, "Couldn't save the converted file here.");
}
finally
{
    if (!committed) { output.Discard(temp); }
}
```

Labels used by modules (so names stay consistent):

| Target | Label(s) | Example output |
|---|---|---|
| any format conversion | value of `AppSettings.OutputLabel` (default `converted`) | `photo (converted).jpg` |
| `pdf.split-pages` | `page N` | `doc (page 3).pdf` |
| `pdf.split-half` | `part 1`, `part 2` | `doc (part 1).pdf` |
| `pdf.extract-odd` / `-even` | `odd pages` / `even pages` | `doc (odd pages).pdf` |
| `pdf.rotate-90` | `rotated` | `doc (rotated).pdf` |
| `pdf.reverse` | `reversed` | `doc (reversed).pdf` |
| `pdf.reorder` | `reordered` | `doc (reordered).pdf` |
| `pdf.to-images` | `page N` | `doc (page 3).png` |
| `video.extract-mp3` | `audio` | `clip (audio).mp3` |

`N` is 1-based and zero-padded to the page count's width (`page 03` for a 40-page PDF) so files sort correctly.

## C3. `ITrigger` (implemented by WP-02, WP-03)

- `Start()` never throws; returns `false` and sets `LastError` (plain language) on failure, e.g. hotkey taken.
- Raise `WheelRequested` / `WheelDismissRequested` **on the UI thread** (`Dispatcher.BeginInvoke` from hook callbacks).
- `ScreenPoint` is **physical pixels**. The wheel (WP-04) converts to DIPs for the monitor under the point.
- `Start`/`Stop` idempotent; `Dispose` releases every hook/hotkey.
- A trigger never touches files and never decides the drop effect.

## C4. Wheel drop contract (WP-04 → WP-11)

- Shared type: `src/WheelConvert.App/Wheel/IWheelView.cs` (`ShowAt`, `HideWheel`, `IsOpen`, `TargetsProvider`, `Dropped`). Changing it needs both WP-04 and WP-11 to agree.
- Segments for a multi-file drag come from `FormatRegistry.GetTargetsFor(paths, …)`: the **first** file's kind decides; a target shows if any dragged file of that kind can use it. Other files are skipped by `DropPlanner` with a reason.
- The wheel reports a drop as `(IReadOnlyList<string> paths, ConversionTarget target)`.
- **Every** `DragEnter`, `DragOver` and `Drop` handler sets `e.Effects = DragDropEffects.Copy` (or `None` for a cancelled drop). **Never `Move`.** Set it first, in a `try/finally`.
- The wheel never opens dropped files; it only reads paths.

## C5. Job queue (`ConversionJobQueue`, WP-01) → consumers (WP-11)

- One `JobCompleted` per enqueued job, always — including after `CancelAll` and `DisposeAsync` (leftovers end as `Cancelled`). Events fire on **worker threads**: marshal to UI before touching WPF.
- One drop = one `EnqueueBatch(requests)` → `BatchHandle(BatchId, JobIds)`. `BatchCompleted` fires once, after the batch's last `JobCompleted`, with every job's result in enqueue order: use it for the summary notification.
- `Cancel(jobId)` cancels one job; `CancelAll()` cancels everything queued or running at that moment; the queue keeps working afterwards; after dispose it's a no-op.
- `JobProgress` (0..1, throttled) for long jobs.
- Plan drops with `DropPlanner.Plan` **off the UI thread** (it touches the file system); enqueue only `plan.Requests`; tell the user about `plan.Skipped`.
- Put `OptionKeys.OutputLabel` = `AppSettings.OutputLabel` into the options you pass to `DropPlanner.Plan`.

## C6. Interaction targets (`RequiresInteraction = true`)

- Currently only `pdf.reorder`. WP-11 calls `IPageOrderPrompt.PromptAsync(path)` **before** enqueueing.
- Result goes into `Options[OptionKeys.PageOrder]` as comma-separated 1-based numbers, e.g. `"3,1,2"`.
- The converter validates it: each number in `1..pageCount`, **no duplicates**, **at least one page**. A subset is allowed and means "leave the other pages out". The identity order (`1,2,…,n`) is allowed and produces a copy. Empty, out-of-range or duplicate → `Fail(Unsupported, "Invalid page order.")`.

## C7. Settings (`AppSettings`, `JsonSettingsStore`)

- Add properties with safe defaults; clamp them in `Normalize()`. Never rename or remove a property.
- Loading is tolerant: a bad field falls back to its default without losing the others; names are case-insensitive; a locked file is never overwritten (`Save` returns false while `SavesBlocked`).
- File: `%LOCALAPPDATA%\WheelConvert\settings.json`. Log: `%LOCALAPPDATA%\WheelConvert\wheelconvert.log`.

## C8. Target IDs

- Defined only in `TargetCatalog`. **Never rename an ID** (settings store them). To retire one, keep it in the catalog and stop implementing it.
- Labels ≤ 8 characters (they must fit on a wheel segment).
- `IsFormatChange: false` for operations that keep the format (PDF → PDF); those are never hidden as "already that format".

## C9. External processes

- Use `ProcessRunner.RunAsync` (argument list, no shell, timeout, kills the process tree). Never `Process.Start` with a concatenated string; never `UseShellExecute = true` for tools.
- Find tools with `ToolLocator.Find`. Pass input files as **full paths** (a file named `-x.mp3` would otherwise look like an option).
- FFmpeg: always `-hide_banner -nostdin -n` (`-n` = never overwrite, a second line of defence behind S3). For progress use `-loglevel info -nostats -progress pipe:2` and parse **stderr** via `onStdErrLine` (`ProcessRunner` streams only stderr live).
- `ProcessRunner` decodes output as UTF-8 (right for FFmpeg); other tools may use the OEM code page.
