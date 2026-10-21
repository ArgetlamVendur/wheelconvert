using System.Collections.Concurrent;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Infrastructure;
using WheelConvert.Core.Jobs;
using WheelConvert.Core.Output;
using WheelConvert.Core.Settings;

namespace WheelConvert.Core.Tests;

/// <summary>Regression tests for findings of the checkpoint-1 review (docs/reviews/2026-10-20-cp1-core.md).</summary>
public static class ReviewRegressionTests
{
    private static readonly string[] AllPdfTargets = TargetCatalog.All.Where(t => t.InputKind == FileKind.Pdf).Select(t => t.Id).ToArray();

    // ── Finding 1: same-format PDF operations must be visible ──────────────────────────────

    [Test]
    public static void F01_PdfSameFormatTargets_AreShown_AndPlanned()
    {
        using var dir = new TempDir();
        var pdf = dir.File("doc.pdf");
        var registry = new FormatRegistry([new FakeConverter("pdf", AllPdfTargets)], new NullLog());

        Assert.Equal(8, registry.GetTargetsFor(pdf).Count, "all 8 PDF targets");
        var plan = DropPlanner.Plan([pdf], TargetCatalog.PdfRotate90, registry);
        Assert.Equal(1, plan.Requests.Count);
        Assert.Equal(0, plan.Skipped.Count);
    }

    [Test]
    public static void F01_FormatChangeTargets_StillHiddenForSameFormat()
    {
        Assert.True(TargetCatalog.ImagePng.IsRedundantFor(".png"));
        Assert.True(TargetCatalog.ImageJpg.IsRedundantFor(".jpeg"));
        Assert.False(TargetCatalog.PdfRotate90.IsRedundantFor(".pdf"));
        Assert.False(TargetCatalog.PdfToImages.IsRedundantFor(".pdf"));
        Assert.False(TargetCatalog.ImageReadQr.IsRedundantFor(".png"));
    }

    [Test]
    public static void F23_MultiPathTargets_UseFirstKind_UnionOfCompatible()
    {
        var image = new FakeConverter("image", "image.jpg", "image.png");
        var registry = new FormatRegistry([image], new NullLog());
        // png alone can't go to PNG; with a jpg in the drag, PNG becomes useful for that file.
        var ids = registry.GetTargetsFor(["a.png", "b.jpg", "c.pdf"]).Select(t => t.Id).ToList();
        Assert.SequenceEqual(["image.jpg", "image.png"], ids);
        Assert.Equal(0, registry.GetTargetsFor(Array.Empty<string>()).Count);
    }

    // ── Findings 2, 3, 5, 17: queue cancellation, disposal, batches ─────────────────────────

    [Test]
    public static async Task F02_CancelAll_CancelsQueuedJobsBehindRunningOne()
    {
        using var dir = new TempDir();
        var src = dir.File("a.png");
        var started = new TaskCompletionSource();
        var slow = new FakeConverter("slow", "image.jpg")
        {
            Behaviour = async (_, _, ct) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return ConversionResult.Ok();
            },
        };
        await using var queue = new ConversionJobQueue(new FormatRegistry([slow], new NullLog()), new SafeOutputWriter(), new NullLog(), 1);
        var results = new ConcurrentBag<ConversionError>();
        queue.JobCompleted += (_, e) => results.Add(e.Result!.Error);

        for (var i = 0; i < 3; i++)
        {
            queue.Enqueue(ConversionRequest.Create(src, TargetCatalog.ImageJpg));
        }

        await started.Task;
        queue.CancelAll();
        await queue.WhenIdleAsync();
        Assert.Equal(3, results.Count);
        Assert.True(results.All(r => r == ConversionError.Cancelled), string.Join(",", results));
    }

    [Test]
    public static async Task F03_Dispose_WithQueuedAndRunningJobs_CompletesEveryJobOnce_Stress()
    {
        using var dir = new TempDir();
        var src = dir.File("a.png");
        for (var iteration = 0; iteration < 150; iteration++)
        {
            var converter = new FakeConverter("c", "image.jpg")
            {
                Behaviour = async (_, _, ct) =>
                {
                    await Task.Delay(Random.Shared.Next(0, 3), ct);
                    return ConversionResult.OkText("x");
                },
            };
            var queue = new ConversionJobQueue(new FormatRegistry([converter], new NullLog()), new SafeOutputWriter(), new NullLog(), 2);
            var completions = new ConcurrentDictionary<Guid, int>();
            queue.JobCompleted += (_, e) => completions.AddOrUpdate(e.JobId, 1, (_, n) => n + 1);

            var batch = queue.EnqueueBatch(Enumerable.Range(0, 10).Select(_ => ConversionRequest.Create(src, TargetCatalog.ImageJpg)).ToList());
            if (iteration % 3 == 0)
            {
                queue.CancelAll();
            }

            await queue.DisposeAsync();
            var idle = queue.WhenIdleAsync();
            Assert.True(await Task.WhenAny(idle, Task.Delay(5000)) == idle, $"iteration {iteration}: WhenIdle hung");
            Assert.Equal(10, completions.Count, $"iteration {iteration}: completions");
            Assert.True(completions.Values.All(n => n == 1), $"iteration {iteration}: duplicate completion");
            Assert.True(batch.JobIds.All(completions.ContainsKey));
        }
    }

    [Test]
    public static async Task F17_CancelAll_AfterDispose_IsNoOp()
    {
        var queue = new ConversionJobQueue(new FormatRegistry([], new NullLog()), new SafeOutputWriter(), new NullLog(), 1);
        await queue.DisposeAsync();
        queue.CancelAll();
        await queue.DisposeAsync(); // idempotent
    }

    [Test]
    public static async Task F05_CancelOneJob_LeavesOthersRunning()
    {
        using var dir = new TempDir();
        var src = dir.File("a.png");
        var gate = new TaskCompletionSource();
        var converter = new FakeConverter("c", "image.jpg")
        {
            Behaviour = async (_, _, ct) =>
            {
                await gate.Task.WaitAsync(ct);
                return ConversionResult.OkText("done");
            },
        };
        await using var queue = new ConversionJobQueue(new FormatRegistry([converter], new NullLog()), new SafeOutputWriter(), new NullLog(), 2);
        var results = new ConcurrentDictionary<Guid, ConversionError>();
        queue.JobCompleted += (_, e) => results[e.JobId] = e.Result!.Error;

        var a = queue.Enqueue(ConversionRequest.Create(src, TargetCatalog.ImageJpg));
        var b = queue.Enqueue(ConversionRequest.Create(src, TargetCatalog.ImageJpg));
        queue.Cancel(a);
        await Task.Delay(200);
        gate.SetResult();
        await queue.WhenIdleAsync();

        Assert.Equal(ConversionError.Cancelled, results[a]);
        Assert.Equal(ConversionError.None, results[b]);
        queue.Cancel(Guid.NewGuid()); // unknown id ignored
    }

    [Test]
    public static async Task F05_BatchCompleted_FiresOnce_InEnqueueOrder()
    {
        using var dir = new TempDir();
        var files = Enumerable.Range(1, 4).Select(i => dir.File($"p{i}.png")).ToList();
        await using var queue = new ConversionJobQueue(new FormatRegistry([new FakeConverter("image", "image.jpg")], new NullLog()), new SafeOutputWriter(), new NullLog(), 3);
        var batches = new ConcurrentBag<BatchEventArgs>();
        var jobEvents = 0;
        queue.JobCompleted += (_, _) => Interlocked.Increment(ref jobEvents);
        queue.BatchCompleted += (_, e) =>
        {
            Assert.Equal(4, Volatile.Read(ref jobEvents), "batch fires after its last job");
            batches.Add(e);
        };

        var handle = queue.EnqueueBatch(files.Select(f => ConversionRequest.Create(f, TargetCatalog.ImageJpg)).ToList());
        await queue.WhenIdleAsync();

        var batch = batches.Single();
        Assert.Equal(handle.BatchId, batch.BatchId);
        Assert.SequenceEqual(handle.JobIds, batch.Jobs.Select(j => j.JobId));
        Assert.Equal(0, queue.EnqueueBatch([]).JobIds.Count);
    }

    // ── Finding 4: progress ─────────────────────────────────────────────────────────────────

    [Test]
    public static async Task F04_Progress_IsForwarded_Throttled_AndFinalAlwaysSent()
    {
        using var dir = new TempDir();
        var src = dir.File("a.png");
        var converter = new FakeConverter("c", "image.jpg") { Behaviour = null };
        IProgress<double>? captured = null;
        var progressConverter = new ProgressConverter(p => captured = p);
        await using var queue = new ConversionJobQueue(new FormatRegistry([progressConverter], new NullLog()), new SafeOutputWriter(), new NullLog(), 1);
        var values = new ConcurrentQueue<double>();
        queue.JobProgress += (_, e) => values.Enqueue(e.Fraction);

        queue.Enqueue(ConversionRequest.Create(src, TargetCatalog.ImageJpg));
        await queue.WhenIdleAsync();

        Assert.True(captured is not null, "converter received a progress sink");
        Assert.True(values.Count is >= 2 and < 50, $"throttled: {values.Count}");
        Assert.Equal(1.0, values.Last());
        _ = converter;
    }

    // ── Findings 7, 8: temps from crashing/cancelled converters; partial results ─────────────

    [Test]
    public static async Task F08_ConverterThatCrashesAfterCreatingTemp_LeavesNoTemp_AndReportsPartials()
    {
        using var dir = new TempDir();
        var src = dir.File("doc.pdf");
        var converter = new FakeConverter("pdf", AllPdfTargets)
        {
            Behaviour = (req, output, _) =>
            {
                var first = output.CreateTempPath(req.SourcePath, ".pdf");
                File.WriteAllText(first, "page1");
                output.Commit(first, req.SourcePath, ".pdf", "page 1");
                var second = output.CreateTempPath(req.SourcePath, ".pdf");
                File.WriteAllText(second, "half-written");
                throw new InvalidOperationException("bug after writing a temp");
            },
        };
        await using var queue = new ConversionJobQueue(new FormatRegistry([converter], new NullLog()), new SafeOutputWriter(), new NullLog(), 1);
        ConversionResult? result = null;
        queue.JobCompleted += (_, e) => result = e.Result;

        queue.Enqueue(ConversionRequest.Create(src, TargetCatalog.PdfSplitPages));
        await queue.WhenIdleAsync();

        Assert.Equal(ConversionError.Unknown, result!.Error);
        Assert.Equal(1, result.OutputPaths.Count, "committed piece reported");
        Assert.Equal(0, Directory.GetFiles(dir.Path, ".~wc-*").Length, "no temp left behind");
        Assert.True(File.Exists(src));
    }

    // ── Findings 9, 10: writer only touches what it issued, in the source folder ────────────

    [Test]
    public static void F09_DiscardAndCommit_RejectUnissuedTempNames()
    {
        using var dir = new TempDir();
        var source = dir.File("photo.png");
        var userFile = dir.File(".~wc-myfile.txt", "USER DATA");
        var lookalike = dir.File($".~wc-{Guid.NewGuid():N}.jpg", "FROM ANOTHER RUN");
        var writer = new SafeOutputWriter();

        Assert.False(writer.Discard(userFile));
        Assert.False(writer.Discard(lookalike));
        Assert.True(File.Exists(userFile) && File.Exists(lookalike));
        Assert.Throws<InvalidOperationException>(() => writer.Commit(lookalike, source, ".jpg", "x"));
        Assert.False(SafeOutputWriter.IsTempFile(userFile), "strict temp-name pattern");
        Assert.True(SafeOutputWriter.IsTempFile(lookalike));
    }

    [Test]
    public static void F10_Commit_RejectsTempFromAnotherFolder()
    {
        using var dirA = new TempDir();
        using var dirB = new TempDir();
        var sourceA = dirA.File("a.png");
        var sourceB = dirB.File("b.png");
        var writer = new SafeOutputWriter();
        var tempForB = writer.CreateTempPath(sourceB, ".jpg");
        File.WriteAllText(tempForB, "x");

        Assert.Throws<InvalidOperationException>(() => writer.Commit(tempForB, sourceA, ".jpg", "converted"));
        Assert.Equal(0, Directory.GetFiles(dirA.Path).Count(f => f.EndsWith(".jpg", StringComparison.Ordinal)));
        Assert.True(writer.Discard(tempForB));
    }

    [Test]
    public static void F09_TryDeleteStaleTemp_OnlyOldExactTemps()
    {
        using var dir = new TempDir();
        var fresh = dir.File($".~wc-{Guid.NewGuid():N}.jpg");
        var old = dir.File($".~wc-{Guid.NewGuid():N}.jpg");
        var user = dir.File(".~wc-notes.txt");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-2));
        File.SetLastWriteTimeUtc(user, DateTime.UtcNow.AddHours(-2));

        Assert.False(SafeOutputWriter.TryDeleteStaleTemp(fresh, TimeSpan.FromHours(1)));
        Assert.True(SafeOutputWriter.TryDeleteStaleTemp(old, TimeSpan.FromHours(1)));
        Assert.False(SafeOutputWriter.TryDeleteStaleTemp(user, TimeSpan.FromHours(1)));
        Assert.True(File.Exists(fresh) && File.Exists(user) && !File.Exists(old));
    }

    [Test]
    public static async Task Writer_ConcurrentCommits_GetUniqueNames_SourceIntact()
    {
        using var dir = new TempDir();
        var source = dir.File("photo.png", "SOURCE");
        var writer = new SafeOutputWriter();
        var finals = await Task.WhenAll(Enumerable.Range(0, 64).Select(i => Task.Run(() =>
        {
            var temp = writer.CreateTempPath(source, ".jpg");
            File.WriteAllText(temp, i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return writer.Commit(temp, source, ".jpg", "converted");
        })));

        Assert.Equal(64, finals.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("SOURCE", File.ReadAllText(source));
    }

    [Test]
    public static void Writer_SkipsNameTakenByFolder()
    {
        using var dir = new TempDir();
        var source = dir.File("photo.png");
        Directory.CreateDirectory(Path.Combine(dir.Path, "photo (converted).jpg"));
        var writer = new SafeOutputWriter();
        var temp = writer.CreateTempPath(source, ".jpg");
        File.WriteAllText(temp, "x");
        Assert.Equal("photo (converted 2).jpg", Path.GetFileName(writer.Commit(temp, source, ".jpg", "converted")));
    }

    // ── Finding 6: label reaches converters ─────────────────────────────────────────────────

    [Test]
    public static void F06_OutputLabel_FromOptions_WithDefault()
    {
        Assert.Equal("converted", ConversionRequest.Create("a.png", TargetCatalog.ImageJpg).OutputLabel);
        var custom = new ConversionRequest("a.png", TargetCatalog.ImageJpg, new Dictionary<string, string> { [OptionKeys.OutputLabel] = "web" });
        Assert.Equal("web", custom.OutputLabel);
    }

    [Test]
    public static void F20_DropPlanner_CopiesOptions()
    {
        using var dir = new TempDir();
        var png = dir.File("a.png");
        var options = new Dictionary<string, string> { [OptionKeys.OutputLabel] = "first" };
        var plan = DropPlanner.Plan([png], TargetCatalog.ImageJpg, new FormatRegistry([new FakeConverter("image", "image.jpg")], new NullLog()), options);
        options[OptionKeys.OutputLabel] = "changed later";
        Assert.Equal("first", plan.Requests[0].OutputLabel);
    }

    // ── Findings 11–13: settings never lose user data ───────────────────────────────────────

    [Test]
    public static void F12_OneBadField_KeepsTheOthers()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "settings.json");
        File.WriteAllText(path, """{ "trigger": "Bogus", "maxConcurrentJobs": "three", "Hotkey": "Ctrl+Shift+K", "outputLabel": "web" }""");
        var settings = new JsonSettingsStore(path, new NullLog()).Load();

        Assert.Equal(TriggerMode.ShiftDrag, settings.Trigger, "bad enum → default");
        Assert.Equal(2, settings.MaxConcurrentJobs, "wrong type → default");
        Assert.Equal("Ctrl+Shift+K", settings.Hotkey, "PascalCase key accepted");
        Assert.Equal("web", settings.OutputLabel, "other fields kept");
        Assert.Equal(0, Directory.GetFiles(dir.Path, "*.corrupt-*").Length, "not quarantined");
    }

    [Test]
    public static void Settings_DuplicateKeysInDifferentCase_LastWins_NoCrash()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "settings.json");
        File.WriteAllText(path, """{ "hotkey": "Ctrl+Alt+A", "Hotkey": "Ctrl+Alt+B" }""");
        Assert.Equal("Ctrl+Alt+B", new JsonSettingsStore(path, new NullLog()).Load().Hotkey);
    }

    [Test]
    public static void F11_LockedFile_IsNotQuarantined_AndSavesAreBlocked()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // File locking semantics needed for this test exist on Windows only.
        }

        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "settings.json");
        File.WriteAllText(path, """{ "outputLabel": "keep me" }""");
        var store = new JsonSettingsStore(path, new NullLog());
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            store.Load();
            Assert.True(store.SavesBlocked);
        }

        Assert.False(store.Save(new AppSettings()), "save refused while blocked");
        Assert.True(File.ReadAllText(path).Contains("keep me", StringComparison.Ordinal));
        Assert.Equal("keep me", store.Load().OutputLabel);
        Assert.False(store.SavesBlocked);
    }

    [Test]
    public static void F13_TwoCorruptFiles_InSameSecond_BothKept()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "settings.json");
        File.WriteAllText(path, "{ broken");
        new JsonSettingsStore(path, new NullLog()).Load();
        File.WriteAllText(path, "{ broken again");
        new JsonSettingsStore(path, new NullLog()).Load();
        Assert.Equal(2, Directory.GetFiles(dir.Path, "settings.corrupt-*.json").Length);
    }

    [Test]
    public static void F13_Save_LeavesNoTempFiles()
    {
        using var dir = new TempDir();
        var store = new JsonSettingsStore(Path.Combine(dir.Path, "settings.json"), new NullLog());
        store.Load();
        Assert.True(store.Save(new AppSettings { OutputLabel = "x" }));
        Assert.True(store.Save(new AppSettings { OutputLabel = "y" }));
        Assert.SequenceEqual(["settings.json"], Directory.GetFiles(dir.Path).Select(Path.GetFileName)!);
        Assert.Equal("y", store.Load().OutputLabel);
    }

    // ── Findings 14, 15: process runner ─────────────────────────────────────────────────────

    [Test]
    public static async Task F14_MissingExecutable_ThrowsWin32Exception()
    {
        var threw = false;
        try
        {
            await ProcessRunner.RunAsync(Path.Combine(Path.GetTempPath(), "definitely-not-here-" + Guid.NewGuid().ToString("N")), [], TimeSpan.FromSeconds(5), CancellationToken.None);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    [Test]
    public static async Task F15_LargeOutput_KeepsTail()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // Same code path; the shell one-liner is POSIX.
        }

        var result = await ProcessRunner.RunAsync("/bin/sh", ["-c", "i=0; while [ $i -lt 20000 ]; do echo line$i; i=$((i+1)); done; echo LAST-LINE"], TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.True(result.StdOut.Length <= ProcessRunner.MaxCapturedChars + 100, "bounded");
        Assert.True(result.StdOut.TrimEnd().EndsWith("LAST-LINE", StringComparison.Ordinal), "tail kept");
        Assert.True(result.StdOut.StartsWith("[…truncated…]", StringComparison.Ordinal));
    }

    private sealed class ProgressConverter(Action<IProgress<double>?> onProgress) : IConverter
    {
        public string Id => "progress";

        public IReadOnlyCollection<string> SupportedTargetIds => ["image.jpg"];

        public bool CanConvert(string sourceExtension, string targetId) => true;

        public ConverterHealth CheckHealth() => ConverterHealth.Ready;

        public Task<ConversionResult> ConvertAsync(ConversionRequest request, IOutputWriter output, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            onProgress(progress);
            for (var i = 0; i <= 1000; i++)
            {
                progress?.Report(i / 1000.0);
            }

            return Task.FromResult(ConversionResult.OkText("ok"));
        }
    }
}
