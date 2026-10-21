using System.Collections.Concurrent;
using System.Threading.Channels;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Output;

namespace WheelConvert.Core.Jobs;

public sealed class JobEventArgs(Guid jobId, Guid batchId, ConversionRequest request, ConversionResult? result) : EventArgs
{
    public Guid JobId { get; } = jobId;

    public Guid BatchId { get; } = batchId;

    public ConversionRequest Request { get; } = request;

    /// <summary>Null for JobStarted.</summary>
    public ConversionResult? Result { get; } = result;
}

public sealed class JobProgressEventArgs(Guid jobId, Guid batchId, double fraction) : EventArgs
{
    public Guid JobId { get; } = jobId;

    public Guid BatchId { get; } = batchId;

    /// <summary>0.0–1.0.</summary>
    public double Fraction { get; } = fraction;
}

public sealed class BatchEventArgs(Guid batchId, IReadOnlyList<JobEventArgs> jobs) : EventArgs
{
    public Guid BatchId { get; } = batchId;

    /// <summary>Every job of the batch with its result, in enqueue order.</summary>
    public IReadOnlyList<JobEventArgs> Jobs { get; } = jobs;
}

public sealed record BatchHandle(Guid BatchId, IReadOnlyList<Guid> JobIds);

/// <summary>
/// Runs conversions in the background with bounded concurrency.
/// Guarantees: every enqueued job ends in exactly one JobCompleted (even across CancelAll and DisposeAsync);
/// every batch ends in exactly one BatchCompleted after its last JobCompleted; temp files a converter
/// leaves uncommitted are discarded when its job ends.
/// EVENTS ARE RAISED ON WORKER THREADS: UI subscribers must marshal (Dispatcher.BeginInvoke).
/// </summary>
public sealed class ConversionJobQueue : IAsyncDisposable
{
    private static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(5);

    private readonly FormatRegistry _registry;
    private readonly IOutputWriter _output;
    private readonly ILog _log;
    private readonly Channel<Job> _channel = Channel.CreateUnbounded<Job>(new UnboundedChannelOptions { SingleReader = false });
    private readonly ConcurrentDictionary<Guid, Job> _live = new();
    private readonly ConcurrentDictionary<Guid, Batch> _batches = new();
    private readonly List<CancellationTokenSource> _retiredGenerations = [];
    private readonly Task[] _workers;
    private readonly object _gate = new();
    private CancellationTokenSource _generation = new();
    private TaskCompletionSource _idle = CompletedSource();
    private int _pending;
    private bool _disposed;

    public ConversionJobQueue(FormatRegistry registry, IOutputWriter output, ILog log, int maxConcurrency)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        var workers = Math.Clamp(maxConcurrency, 1, 8);
        _workers = Enumerable.Range(0, workers).Select(_ => Task.Run(WorkerLoopAsync)).ToArray();
    }

    public event EventHandler<JobEventArgs>? JobStarted;

    public event EventHandler<JobEventArgs>? JobCompleted;

    /// <summary>Throttled (≤ 4 per second per job, and only on ≥ 1 % change).</summary>
    public event EventHandler<JobProgressEventArgs>? JobProgress;

    public event EventHandler<BatchEventArgs>? BatchCompleted;

    public int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _pending;
            }
        }
    }

    /// <exception cref="ObjectDisposedException">The queue has been disposed.</exception>
    public Guid Enqueue(ConversionRequest request) => EnqueueBatch([request]).JobIds[0];

    /// <summary>Enqueue several requests as one batch (e.g. one drop). An empty list returns a handle with no jobs and raises nothing.</summary>
    /// <exception cref="ObjectDisposedException">The queue has been disposed.</exception>
    public BatchHandle EnqueueBatch(IReadOnlyList<ConversionRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Any(r => r is null))
        {
            throw new ArgumentException("Requests must not contain null.", nameof(requests));
        }

        var batchId = Guid.NewGuid();
        if (requests.Count == 0)
        {
            return new BatchHandle(batchId, Array.Empty<Guid>());
        }

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var batch = new Batch(requests.Count);
            _batches[batchId] = batch;
            var ids = new List<Guid>(requests.Count);
            foreach (var request in requests)
            {
                // The token is captured NOW, so CancelAll cancels everything already queued.
                var job = new Job(Guid.NewGuid(), batchId, request, CancellationTokenSource.CreateLinkedTokenSource(_generation.Token));
                _live[job.Id] = job;
                batch.Order.Add(job.Id);
                ids.Add(job.Id);
                if (_pending++ == 0)
                {
                    _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                }

                _channel.Writer.TryWrite(job); // Unbounded and not completed while !_disposed: always succeeds.
            }

            return new BatchHandle(batchId, ids);
        }
    }

    /// <summary>Cancels one queued or running job. Unknown or finished IDs are ignored.</summary>
    public void Cancel(Guid jobId)
    {
        if (_live.TryGetValue(jobId, out var job))
        {
            CancelOffThread(job.Cts);
        }
    }

    /// <summary>Cancels every job queued or running now. Jobs enqueued afterwards are unaffected. No-op after dispose.</summary>
    public void CancelAll()
    {
        CancellationTokenSource old;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            old = _generation;
            _generation = new CancellationTokenSource();
            _retiredGenerations.Add(old); // Disposed with the queue: running converters may still hold its token.
        }

        CancelOffThread(old);
    }

    /// <summary>Completes when nothing is queued or running.</summary>
    public Task WhenIdleAsync()
    {
        lock (_gate)
        {
            return _idle.Task;
        }
    }

    /// <summary>Cancels everything, lets workers report every remaining job as Cancelled, then stops (≤ ~5 s).</summary>
    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource generation;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            generation = _generation;
            _channel.Writer.TryComplete();
        }

        try
        {
            await generation.CancelAsync().ConfigureAwait(false);
        }
        catch (AggregateException ex)
        {
            _log.Error("A cancellation callback threw during shutdown.", ex);
        }

        var all = Task.WhenAll(_workers);
        if (await Task.WhenAny(all, Task.Delay(ShutdownGrace)).ConfigureAwait(false) != all)
        {
            _log.Warn("Queue shutdown timed out; a converter ignored cancellation.");
            return; // Don't dispose token sources still in use by stuck workers.
        }

        lock (_gate)
        {
            generation.Dispose();
            foreach (var cts in _retiredGenerations)
            {
                cts.Dispose();
            }
        }
    }

    private async Task WorkerLoopAsync()
    {
        var reader = _channel.Reader;

        // No cancellation token here on purpose: after DisposeAsync completes the writer, workers drain
        // remaining jobs (already cancelled) so each still gets exactly one JobCompleted.
        while (await reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (reader.TryRead(out var job))
            {
                await RunJobAsync(job).ConfigureAwait(false);
            }
        }
    }

    private async Task RunJobAsync(Job job)
    {
        var scope = new JobOutputScope(_output);
        ConversionResult result;
        try
        {
            Raise(JobStarted, new JobEventArgs(job.Id, job.BatchId, job.Request, null));
            var progress = new ThrottledProgress(f => RaiseProgress(new JobProgressEventArgs(job.Id, job.BatchId, f)));
            result = await RunOneAsync(job.Request, scope, progress, job.Cts.Token).ConfigureAwait(false);
        }
        finally
        {
            var leftovers = scope.DiscardLeftovers();
            if (leftovers > 0)
            {
                _log.Warn($"Discarded {leftovers} uncommitted temp file(s) after {job.Request.Target.Id}.");
            }
        }

        // Partial results (cancel/crash after some pieces were committed) are still reported.
        if (!result.Success && result.OutputPaths.Count == 0 && scope.Committed.Count > 0)
        {
            result = result with { OutputPaths = scope.Committed };
        }

        var completed = new JobEventArgs(job.Id, job.BatchId, job.Request, result);
        Raise(JobCompleted, completed);
        _live.TryRemove(job.Id, out _);
        job.Cts.Dispose();
        CompleteInBatch(completed);
        MarkDone();
    }

    private async Task<ConversionResult> RunOneAsync(ConversionRequest request, IOutputWriter output, IProgress<double> progress, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(request.SourcePath))
            {
                return ConversionResult.Fail(ConversionError.SourceMissing, "The file no longer exists.");
            }

            var converter = _registry.ResolveConverter(request.SourcePath, request.Target.Id);
            if (converter is null)
            {
                return ConversionResult.Fail(ConversionError.Unsupported, $"No converter for {request.Target.Label}.");
            }

            _log.Info($"Start {request.Target.Id} via {converter.Id}: {request.SourcePath}");
            var result = await converter.ConvertAsync(request, output, progress, ct).ConfigureAwait(false)
                ?? ConversionResult.Fail(ConversionError.Unknown, "Converter returned no result.");
            _log.Info($"End {request.Target.Id}: success={result.Success} error={result.Error} outputs={result.OutputPaths.Count}");
            return result;
        }
        catch (OperationCanceledException)
        {
            return ConversionResult.Fail(ConversionError.Cancelled, "Cancelled.");
        }
        catch (Exception ex)
        {
            _log.Error($"Converter crashed on {request.Target.Id}: {request.SourcePath}", ex);
            return ConversionResult.Fail(ConversionError.Unknown, "Something went wrong. Details are in the log.");
        }
    }

    private void CompleteInBatch(JobEventArgs completed)
    {
        if (!_batches.TryGetValue(completed.BatchId, out var batch))
        {
            return;
        }

        IReadOnlyList<JobEventArgs>? finished = null;
        lock (batch)
        {
            batch.Results[completed.JobId] = completed;
            if (batch.Results.Count == batch.Expected)
            {
                finished = batch.Order.Select(id => batch.Results[id]).ToList();
            }
        }

        if (finished is not null)
        {
            _batches.TryRemove(completed.BatchId, out _);
            Raise(BatchCompleted, new BatchEventArgs(completed.BatchId, finished));
        }
    }

    private void CancelOffThread(CancellationTokenSource cts) =>
        // Cancellation callbacks (e.g. killing FFmpeg) must not run on the caller's (UI) thread.
        _ = Task.Run(() =>
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Job finished meanwhile.
            }
            catch (AggregateException ex)
            {
                _log.Error("A cancellation callback threw.", ex);
            }
        });

    private void Raise<T>(EventHandler<T>? handler, T args)
    {
        try
        {
            handler?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            _log.Error("A job event subscriber threw.", ex);
        }
    }

    private void RaiseProgress(JobProgressEventArgs args) => Raise(JobProgress, args);

    private void MarkDone()
    {
        lock (_gate)
        {
            if (--_pending == 0)
            {
                _idle.TrySetResult();
            }
        }
    }

    private static TaskCompletionSource CompletedSource()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetResult();
        return tcs;
    }

    private sealed record Job(Guid Id, Guid BatchId, ConversionRequest Request, CancellationTokenSource Cts);

    private sealed class Batch(int expected)
    {
        public int Expected { get; } = expected;

        public List<Guid> Order { get; } = new(expected);

        public Dictionary<Guid, JobEventArgs> Results { get; } = [];
    }
}
