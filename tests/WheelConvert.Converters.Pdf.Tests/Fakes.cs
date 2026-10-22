using WheelConvert.Core.Contracts;
using WheelConvert.Core.Output;

namespace WheelConvert.Converters.Pdf.Tests;

/// <summary>The real writer, except that commit number <c>failOnCommit</c> (1-based) throws like a full disk would.</summary>
internal sealed class FailingCommitWriter(int failOnCommit) : IOutputWriter
{
    private readonly SafeOutputWriter _inner = new();
    private int _commits;

    public string CreateTempPath(string sourcePath, string outputExtension) => _inner.CreateTempPath(sourcePath, outputExtension);

    public string Commit(string tempPath, string sourcePath, string outputExtension, string label) =>
        Interlocked.Increment(ref _commits) == failOnCommit
            ? throw new IOException("There is not enough space on the disk.")
            : _inner.Commit(tempPath, sourcePath, outputExtension, label);

    public bool Discard(string tempPath) => _inner.Discard(tempPath);
}

/// <summary>Cancels synchronously once progress reaches <c>threshold</c> (<see cref="Progress{T}"/> would post asynchronously).</summary>
internal sealed class CancelAtProgress(CancellationTokenSource cts, double threshold) : IProgress<double>
{
    public void Report(double value)
    {
        if (value >= threshold)
        {
            cts.Cancel();
        }
    }
}
