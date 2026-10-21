using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Output;

namespace WheelConvert.Core.Jobs;

public sealed record SkippedItem(string Path, string Reason);

public sealed record DropPlan(IReadOnlyList<ConversionRequest> Requests, IReadOnlyList<SkippedItem> Skipped);

/// <summary>
/// Turns "these N dropped paths onto this segment" into one request per compatible file.
/// Folders, missing files, our own temp files and files of another kind are skipped with a reason.
/// Touches the file system (Exists checks), which can stall on offline network shares:
/// CALL IT OFF THE UI THREAD (Task.Run).
/// </summary>
public static class DropPlanner
{
    public static DropPlan Plan(
        IReadOnlyList<string> droppedPaths,
        ConversionTarget target,
        FormatRegistry registry,
        IReadOnlyDictionary<string, string>? options = null)
    {
        ArgumentNullException.ThrowIfNull(droppedPaths);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(registry);

        var requests = new List<ConversionRequest>();
        var skipped = new List<SkippedItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Copy so later changes by the caller can't affect queued requests.
        IReadOnlyDictionary<string, string> opts = new Dictionary<string, string>(options ?? new Dictionary<string, string>(), StringComparer.Ordinal);

        foreach (var raw in droppedPaths)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            string path;
            try
            {
                path = Path.GetFullPath(raw);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                skipped.Add(new SkippedItem(raw, "Invalid path."));
                continue;
            }

            if (!seen.Add(path))
            {
                continue;
            }

            if (Directory.Exists(path))
            {
                skipped.Add(new SkippedItem(path, "Folders are not supported."));
            }
            else if (!File.Exists(path))
            {
                skipped.Add(new SkippedItem(path, "File not found."));
            }
            else if (SafeOutputWriter.IsTempFile(path))
            {
                skipped.Add(new SkippedItem(path, "Temporary file."));
            }
            else if (FileClassifier.Classify(path) != target.InputKind)
            {
                skipped.Add(new SkippedItem(path, $"Not a {target.InputKind} file."));
            }
            else if (target.IsRedundantFor(FileClassifier.NormalizeExtension(path)))
            {
                skipped.Add(new SkippedItem(path, $"Already {target.Label}."));
            }
            else if (registry.ResolveConverter(path, target.Id) is null)
            {
                skipped.Add(new SkippedItem(path, $"No converter available for {target.Label}."));
            }
            else
            {
                requests.Add(new ConversionRequest(path, target, opts));
            }
        }

        return new DropPlan(requests, skipped);
    }
}
