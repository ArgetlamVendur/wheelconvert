using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Output;

namespace WheelConvert.Core.Jobs;

public sealed record SkippedItem(string Path, string Reason);

public sealed record DropPlan(IReadOnlyList<ConversionRequest> Requests, IReadOnlyList<SkippedItem> Skipped);

/// <summary>
/// Turns "these N dropped paths onto this segment" into one request per compatible file, or into ONE request for
/// targets that combine inputs (<see cref="ConversionTarget.CombinesInputs"/>, ADR-011 section 3).
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
        var accepted = new List<string>();
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
                accepted.Add(path);
            }
        }

        if (target.MaxInputs is { } max && accepted.Count > max)
        {
            var message = max == 1
                ? $"Drop one file at a time for {target.Label}."
                : $"Drop at most {max} files for {target.Label}.";
            skipped.AddRange(accepted.Select(a => new SkippedItem(a, message)));
        }
        else if (target.CombinesInputs)
        {
            if (accepted.Count < target.MinInputs)
            {
                var message = $"Drop at least {target.MinInputs} files for {target.Label}.";
                skipped.AddRange(accepted.Select(a => new SkippedItem(a, message)));
            }
            else if (accepted.Count > 0)
            {
                accepted.Sort(NaturalFileNameComparer.Instance);
                requests.Add(new ConversionRequest(accepted[0], target, opts, accepted.Count > 1 ? accepted.Skip(1).ToList() : null));
            }
        }
        else
        {
            requests.AddRange(accepted.Select(a => new ConversionRequest(a, target, opts)));
        }

        return new DropPlan(requests, skipped);
    }
}
