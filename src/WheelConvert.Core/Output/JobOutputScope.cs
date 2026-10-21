using System.Collections.Concurrent;
using WheelConvert.Core.Contracts;

namespace WheelConvert.Core.Output;

/// <summary>
/// Per-job wrapper the queue hands to converters. Records every temp issued and every path committed, so
/// that when the job ends — success, failure, crash or cancellation — any temp left uncommitted is discarded
/// (enforces CONTRACTS C2 S4 even for buggy converters) and partial results can be reported.
/// </summary>
internal sealed class JobOutputScope(IOutputWriter inner) : IOutputWriter
{
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<string> _committed = new();

    public IReadOnlyList<string> Committed => _committed.ToArray();

    public string CreateTempPath(string sourcePath, string outputExtension)
    {
        var temp = inner.CreateTempPath(sourcePath, outputExtension);
        _pending[temp] = 0;
        return temp;
    }

    public string Commit(string tempPath, string sourcePath, string outputExtension, string label)
    {
        var final = inner.Commit(tempPath, sourcePath, outputExtension, label);
        _pending.TryRemove(Path.GetFullPath(tempPath), out _);
        _pending.TryRemove(tempPath, out _);
        _committed.Enqueue(final);
        return final;
    }

    public bool Discard(string tempPath)
    {
        _pending.TryRemove(tempPath, out _);
        return inner.Discard(tempPath);
    }

    /// <summary>Discards every temp not committed. Returns how many files were deleted.</summary>
    public int DiscardLeftovers()
    {
        var deleted = 0;
        foreach (var temp in _pending.Keys.ToList())
        {
            if (inner.Discard(temp))
            {
                deleted++;
            }

            _pending.TryRemove(temp, out _);
        }

        return deleted;
    }
}
