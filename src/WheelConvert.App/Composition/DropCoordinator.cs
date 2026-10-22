using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Threading;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Jobs;
using WheelConvert.Core.Settings;

namespace WheelConvert.App.Composition;

/// <summary>
/// WP-11 (docs/modules/WP-11-integration.md req. 6–7). Turns a wheel drop into a queued batch and turns each
/// finished batch into one notification and, for text results (QR), clipboard text.
/// </summary>
public sealed class DropCoordinator
{
    private const string Title = "WheelConvert";
    private const int MaxMessageLength = 120;
    private const int QrPreviewLength = 80;

    private readonly FormatRegistry _registry;
    private readonly ConversionJobQueue _queue;
    private readonly INotifier _notifier;
    private readonly IClipboardService _clipboard;
    private readonly IPageOrderPrompt _pageOrderPrompt;
    private readonly Func<AppSettings> _settings;
    private readonly Dispatcher _dispatcher;
    private volatile bool _stopped;

    public DropCoordinator(
        FormatRegistry registry,
        ConversionJobQueue queue,
        INotifier notifier,
        IClipboardService clipboard,
        IPageOrderPrompt pageOrderPrompt,
        Func<AppSettings> settings,
        Dispatcher dispatcher)
    {
        _registry = registry;
        _queue = queue;
        _notifier = notifier;
        _clipboard = clipboard;
        _pageOrderPrompt = pageOrderPrompt;
        _settings = settings;
        _dispatcher = dispatcher;
        _queue.BatchCompleted += OnBatchCompleted;
    }

    /// <summary>Targets for the wheel for everything being dragged, filtered by the user's disabled list and max segments.</summary>
    public IReadOnlyList<ConversionTarget> GetTargets(IReadOnlyList<string> draggedPaths)
    {
        var s = _settings();
        return _registry.GetTargetsFor(draggedPaths, s.DisabledTargetIds.ToHashSet(StringComparer.Ordinal), s.MaxWheelSegments);
    }

    /// <summary>
    /// UI thread. Prompts if the target needs it, plans off the UI thread, enqueues one batch and warns about skipped files.
    /// Unexpected exceptions propagate to the caller (AppHost logs them).
    /// </summary>
    public async Task HandleDropAsync(IReadOnlyList<string> paths, ConversionTarget target)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(target);

        var options = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [OptionKeys.OutputLabel] = _settings().OutputLabel,
        };

        var toPlan = paths;
        if (target.RequiresInteraction)
        {
            // CONTRACTS C6: only pdf.reorder needs interaction, and it works on one PDF at a time.
            var pdf = paths.FirstOrDefault(p => FileClassifier.Classify(p) == FileKind.Pdf);
            if (pdf is null)
            {
                _notifier.Notify(NotificationKind.Warning, Title, Fit($"{target.Label} needs a PDF file."));
                return;
            }

            var order = await _pageOrderPrompt.PromptAsync(pdf, CancellationToken.None);
            if (order is null || _stopped)
            {
                return; // The user cancelled.
            }

            options[OptionKeys.PageOrder] = string.Join(',', order.Select(n => n.ToString(CultureInfo.InvariantCulture)));
            toPlan = [pdf];
        }

        // DropPlanner touches the file system, which can stall on offline network drives.
        var plan = await Task.Run(() => DropPlanner.Plan(toPlan, target, _registry, options));
        if (_stopped)
        {
            return;
        }

        if (plan.Skipped.Count > 0)
        {
            var files = plan.Skipped.Count == 1 ? "1 file" : $"{plan.Skipped.Count} files";
            _notifier.Notify(NotificationKind.Warning, Title, Fit($"Skipped {files}: {plan.Skipped[0].Reason}"));
        }

        if (plan.Requests.Count > 0)
        {
            _queue.EnqueueBatch(plan.Requests);
        }
    }

    /// <summary>Shutdown: no more notifications or clipboard writes (jobs cancelled by the shutdown end silently).</summary>
    public void Stop()
    {
        _stopped = true;
        _queue.BatchCompleted -= OnBatchCompleted;
    }

    // Worker thread (CONTRACTS C5): marshal to the UI thread before touching anything.
    private void OnBatchCompleted(object? sender, BatchEventArgs e) =>
        _dispatcher.BeginInvoke(new Action(() => _ = ReportBatchAsync(e)));

    private async Task ReportBatchAsync(BatchEventArgs batch)
    {
        if (_stopped)
        {
            return;
        }

        // Cancelled jobs are reported nowhere; everything else counts.
        var jobs = batch.Jobs.Where(j => j.Result is { Error: not ConversionError.Cancelled }).ToList();
        if (jobs.Count == 0)
        {
            return;
        }

        var showSuccess = _settings().ShowSuccessNotifications;
        var successes = jobs.Where(j => j.Result!.Success).ToList(); // Result is non-null: filtered above.
        var failures = jobs.Where(j => !j.Result!.Success).ToList();

        var texts = successes.Select(j => j.Result!.Text).OfType<string>().Where(t => t.Length > 0).ToList();
        if (texts.Count > 0)
        {
            // Untrusted text (AGENTS §4.10): only copied and shown, never opened.
            var copied = await _clipboard.SetTextAsync(string.Join(Environment.NewLine, texts));
            if (_stopped)
            {
                return;
            }

            if (!copied)
            {
                _notifier.Notify(NotificationKind.Warning, Title, "QR found but the clipboard was busy.");
            }
            else if (showSuccess && failures.Count == 0)
            {
                _notifier.Notify(NotificationKind.Success, Title, Fit($"QR copied: {Preview(texts[0])}"));
            }
        }

        if (failures.Count > 0)
        {
            var first = failures[0];
            var title = batch.Jobs.Count == 1 ? "Conversion failed" : $"Converted {successes.Count} of {batch.Jobs.Count} files";
            var message = $"{Path.GetFileName(first.Request.SourcePath)}: {first.Result!.Message ?? "Conversion failed."}";

            // Partial outputs of a failed job first (CONTRACTS C2 S5), otherwise whatever did succeed.
            var reveal = failures.Concat(successes).SelectMany(j => j.Result!.OutputPaths).FirstOrDefault();
            _notifier.Notify(NotificationKind.Error, title, Fit(message), reveal);
            return;
        }

        var outputs = successes.SelectMany(j => j.Result!.OutputPaths).ToList();
        if (!showSuccess || outputs.Count == 0)
        {
            return;
        }

        var summary = batch.Jobs.Count == 1
            ? $"Converted {Path.GetFileName(successes[0].Request.SourcePath)} → {successes[0].Request.Target.Label}"
            : $"Converted {successes.Count} of {batch.Jobs.Count} files";
        _notifier.Notify(NotificationKind.Success, Title, Fit(summary), outputs[0]);
    }

    /// <summary>First characters of untrusted text on one line, control characters removed.</summary>
    private static string Preview(string text)
    {
        var builder = new StringBuilder(QrPreviewLength + 1);
        foreach (var c in text)
        {
            if (builder.Length == QrPreviewLength)
            {
                return builder.Append('…').ToString();
            }

            builder.Append(char.IsControl(c) ? ' ' : c);
        }

        return builder.ToString();
    }

    /// <summary>User-facing text is at most 120 characters (AGENTS §6); long file names are cut, not the app.</summary>
    private static string Fit(string message) =>
        message.Length <= MaxMessageLength ? message : string.Concat(message.AsSpan(0, MaxMessageLength - 1), "…");
}
