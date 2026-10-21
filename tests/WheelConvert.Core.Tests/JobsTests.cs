using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Jobs;
using WheelConvert.Core.Output;

namespace WheelConvert.Core.Tests;

public static class JobsTests
{
    [Test]
    public static void DropPlanner_SkipsFoldersMissingTempAndWrongKind()
    {
        using var dir = new TempDir();
        var png = dir.File("a.png");
        var jpg = dir.File("b.jpg");
        var txt = dir.File("c.txt");
        var temp = dir.File($".~wc-{Guid.NewGuid():N}.png");
        var folder = Path.Combine(dir.Path, "sub");
        Directory.CreateDirectory(folder);
        var registry = new FormatRegistry([new FakeConverter("image", "image.jpg")], new NullLog());

        var plan = DropPlanner.Plan([png, png, jpg, txt, temp, folder, Path.Combine(dir.Path, "gone.png"), "  "], TargetCatalog.ImageJpg, registry);

        Assert.SequenceEqual([png], plan.Requests.Select(r => r.SourcePath));
        Assert.Equal(5, plan.Skipped.Count, "skipped count (dupes and blanks are silent)");
        Assert.True(plan.Skipped.Any(s => s.Path == jpg && s.Reason.StartsWith("Already", StringComparison.Ordinal)));
    }

    [Test]
    public static async Task Queue_RunsJobs_AndReportsEachOnce()
    {
        using var dir = new TempDir();
        var sources = Enumerable.Range(1, 5).Select(i => dir.File($"p{i}.png")).ToList();
        var registry = new FormatRegistry([new FakeConverter("image", "image.jpg")], new NullLog());
        await using var queue = new ConversionJobQueue(registry, new SafeOutputWriter(), new NullLog(), maxConcurrency: 3);
        var completed = new List<JobEventArgs>();
        queue.JobCompleted += (_, e) => { lock (completed) { completed.Add(e); } };

        var ids = sources.Select(s => queue.Enqueue(ConversionRequest.Create(s, TargetCatalog.ImageJpg))).ToList();
        await queue.WhenIdleAsync();

        Assert.Equal(5, completed.Count);
        Assert.Equal(5, completed.Select(c => c.JobId).Distinct().Count());
        Assert.True(ids.All(id => completed.Any(c => c.JobId == id)));
        Assert.True(completed.All(c => c.Result!.Success));
        Assert.True(sources.All(s => File.Exists(Path.ChangeExtension(s, null) + " (converted).jpg")));
        Assert.Equal(0, queue.PendingCount);
    }

    [Test]
    public static async Task Queue_ConverterCrash_BecomesFailure_NotException()
    {
        using var dir = new TempDir();
        var src = dir.File("a.png");
        var converter = new FakeConverter("image", "image.jpg") { Behaviour = (_, _, _) => throw new InvalidOperationException("bug") };
        var registry = new FormatRegistry([converter], new NullLog());
        await using var queue = new ConversionJobQueue(registry, new SafeOutputWriter(), new NullLog(), 1);
        ConversionResult? result = null;
        queue.JobCompleted += (_, e) => result = e.Result;

        queue.Enqueue(ConversionRequest.Create(src, TargetCatalog.ImageJpg));
        await queue.WhenIdleAsync();

        Assert.Equal(ConversionError.Unknown, result!.Error);
        Assert.True(File.Exists(src));
    }

    [Test]
    public static async Task Queue_MissingSource_FailsCleanly()
    {
        using var dir = new TempDir();
        var registry = new FormatRegistry([new FakeConverter("image", "image.jpg")], new NullLog());
        await using var queue = new ConversionJobQueue(registry, new SafeOutputWriter(), new NullLog(), 1);
        ConversionResult? result = null;
        queue.JobCompleted += (_, e) => result = e.Result;

        queue.Enqueue(ConversionRequest.Create(Path.Combine(dir.Path, "gone.png"), TargetCatalog.ImageJpg));
        await queue.WhenIdleAsync();

        Assert.Equal(ConversionError.SourceMissing, result!.Error);
    }

    [Test]
    public static async Task Queue_CancelAll_CancelsRunning_ThenAcceptsNewWork()
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
        var registry = new FormatRegistry([slow], new NullLog());
        await using var queue = new ConversionJobQueue(registry, new SafeOutputWriter(), new NullLog(), 1);
        var results = new List<ConversionResult>();
        queue.JobCompleted += (_, e) => { lock (results) { results.Add(e.Result!); } };

        queue.Enqueue(ConversionRequest.Create(src, TargetCatalog.ImageJpg));
        await started.Task;
        queue.CancelAll();
        await queue.WhenIdleAsync();
        Assert.Equal(ConversionError.Cancelled, results.Single().Error);

        slow.Behaviour = (_, _, _) => Task.FromResult(ConversionResult.OkText("ok"));
        queue.Enqueue(ConversionRequest.Create(src, TargetCatalog.ImageJpg));
        await queue.WhenIdleAsync();
        Assert.True(results[1].Success, "queue must keep working after CancelAll");
    }

    [Test]
    public static async Task Queue_SubscriberThatThrows_DoesNotKillWorker()
    {
        using var dir = new TempDir();
        var src = dir.File("a.png");
        var registry = new FormatRegistry([new FakeConverter("image", "image.jpg")], new NullLog());
        await using var queue = new ConversionJobQueue(registry, new SafeOutputWriter(), new NullLog(), 1);
        var count = 0;
        queue.JobCompleted += (_, _) =>
        {
            Interlocked.Increment(ref count);
            throw new InvalidOperationException("bad subscriber");
        };

        queue.Enqueue(ConversionRequest.Create(src, TargetCatalog.ImageJpg));
        queue.Enqueue(ConversionRequest.Create(src, TargetCatalog.ImageJpg));
        await queue.WhenIdleAsync();
        Assert.Equal(2, count);
    }

    [Test]
    public static async Task Queue_WhenIdle_CompletesImmediatelyWhenEmpty()
    {
        var registry = new FormatRegistry([], new NullLog());
        await using var queue = new ConversionJobQueue(registry, new SafeOutputWriter(), new NullLog(), 1);
        Assert.True(queue.WhenIdleAsync().IsCompleted);
    }

    [Test]
    public static async Task Queue_EnqueueAfterDispose_Throws()
    {
        var registry = new FormatRegistry([], new NullLog());
        var queue = new ConversionJobQueue(registry, new SafeOutputWriter(), new NullLog(), 1);
        await queue.DisposeAsync();
        Assert.Throws<ObjectDisposedException>(() => queue.Enqueue(ConversionRequest.Create("a.png", TargetCatalog.ImageJpg)));
    }
}
