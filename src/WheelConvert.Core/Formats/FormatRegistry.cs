using WheelConvert.Core.Contracts;

namespace WheelConvert.Core.Formats;

/// <summary>
/// Answers "which segments should the wheel show for this file?" and "which converter runs this target?".
/// Only converters whose <see cref="IConverter.CheckHealth"/> reported available are used, so stub
/// converters (NotImplemented) are invisible and the app works at every stage of development.
/// Thread-safe: reads use an immutable snapshot replaced by <see cref="Refresh"/>.
/// </summary>
public sealed class FormatRegistry
{
    private readonly IReadOnlyList<IConverter> _converters;
    private readonly ILog _log;
    private volatile Snapshot _snapshot;

    public FormatRegistry(IEnumerable<IConverter> converters, ILog log)
    {
        ArgumentNullException.ThrowIfNull(converters);
        _converters = converters.ToList();
        _log = log ?? throw new ArgumentNullException(nameof(log));

        var duplicateIds = _converters.GroupBy(c => c.Id, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateIds.Count > 0)
        {
            throw new ArgumentException($"Duplicate converter IDs: {string.Join(", ", duplicateIds)}", nameof(converters));
        }

        _snapshot = BuildSnapshot();
    }

    /// <summary>Converters that were unavailable at the last refresh, with the reason (for the tray "status" menu).</summary>
    public IReadOnlyList<(string ConverterId, ConverterHealth Health)> Unavailable => _snapshot.Unavailable;

    /// <summary>Re-run health checks, e.g. after the user installs FFmpeg.</summary>
    public void Refresh() => _snapshot = BuildSnapshot();

    /// <summary>Targets for one file, ordered by priority, excluding same-format, disabled and unavailable ones.</summary>
    public IReadOnlyList<ConversionTarget> GetTargetsFor(
        string path,
        IReadOnlySet<string>? disabledTargetIds = null,
        int maxCount = int.MaxValue)
    {
        var kind = FileClassifier.Classify(path);
        if (kind == FileKind.Unsupported || maxCount <= 0)
        {
            return Array.Empty<ConversionTarget>();
        }

        var ext = FileClassifier.NormalizeExtension(path);
        var snapshot = _snapshot;
        return TargetCatalog.All
            .Where(t => t.InputKind == kind)
            .Where(t => !t.IsRedundantFor(ext))
            .Where(t => disabledTargetIds is null || !disabledTargetIds.Contains(t.Id))
            .Where(t => FindConverter(snapshot, ext, t.Id) is not null)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .Take(maxCount)
            .ToList();
    }

    /// <summary>
    /// Targets for a multi-file drag. Rule: the FIRST path's kind decides the wheel; a target is shown if at least
    /// one dragged file of that kind can use it. Files of other kinds are skipped later by <see cref="Jobs.DropPlanner"/>.
    /// </summary>
    public IReadOnlyList<ConversionTarget> GetTargetsFor(
        IReadOnlyList<string> paths,
        IReadOnlySet<string>? disabledTargetIds = null,
        int maxCount = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return Array.Empty<ConversionTarget>();
        }

        if (paths.Count == 1)
        {
            return GetTargetsFor(paths[0], disabledTargetIds, maxCount);
        }

        var kind = FileClassifier.Classify(paths[0]);
        return paths
            .Where(p => FileClassifier.Classify(p) == kind)
            .SelectMany(p => GetTargetsFor(p, disabledTargetIds))
            .DistinctBy(t => t.Id)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .Take(maxCount)
            .ToList();
    }

    /// <summary>The converter for this file + target, or null if none is available.</summary>
    public IConverter? ResolveConverter(string path, string targetId) =>
        FindConverter(_snapshot, FileClassifier.NormalizeExtension(path), targetId);

    private static IConverter? FindConverter(Snapshot snapshot, string ext, string targetId)
    {
        if (!snapshot.ByTarget.TryGetValue(targetId, out var candidates))
        {
            return null;
        }

        foreach (var converter in candidates)
        {
            if (converter.CanConvert(ext, targetId))
            {
                return converter;
            }
        }

        return null;
    }

    private Snapshot BuildSnapshot()
    {
        var byTarget = new Dictionary<string, List<IConverter>>(StringComparer.Ordinal);
        var unavailable = new List<(string, ConverterHealth)>();

        foreach (var converter in _converters)
        {
            ConverterHealth health;
            try
            {
                health = converter.CheckHealth();
            }
            catch (Exception ex)
            {
                _log.Error($"Health check threw for converter '{converter.Id}'.", ex);
                health = new ConverterHealth(false, ConversionError.Unknown, ex.Message);
            }

            if (!health.IsAvailable)
            {
                unavailable.Add((converter.Id, health));
                _log.Info($"Converter '{converter.Id}' unavailable: {health.Reason} {health.Message}");
                continue;
            }

            foreach (var targetId in converter.SupportedTargetIds)
            {
                if (TargetCatalog.Find(targetId) is null)
                {
                    _log.Warn($"Converter '{converter.Id}' declares unknown target '{targetId}'; ignored.");
                    continue;
                }

                if (!byTarget.TryGetValue(targetId, out var list))
                {
                    byTarget[targetId] = list = [];
                }

                list.Add(converter);
            }
        }

        return new Snapshot(byTarget, unavailable);
    }

    private sealed record Snapshot(
        IReadOnlyDictionary<string, List<IConverter>> ByTarget,
        IReadOnlyList<(string ConverterId, ConverterHealth Health)> Unavailable);
}
