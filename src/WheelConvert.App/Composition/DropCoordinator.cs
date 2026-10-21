using System.Windows.Threading;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Jobs;
using WheelConvert.Core.Settings;

namespace WheelConvert.App.Composition;

/// <summary>
/// STUB: WP-11 (docs/modules/WP-11-integration.md). Turns a wheel drop into a queued batch and turns job
/// results into notifications / clipboard text.
/// </summary>
public sealed class DropCoordinator
{
    private readonly FormatRegistry _registry;
    private readonly ConversionJobQueue _queue;
    private readonly INotifier _notifier;
    private readonly IClipboardService _clipboard;
    private readonly IPageOrderPrompt _pageOrderPrompt;
    private readonly Func<AppSettings> _settings;
    private readonly Dispatcher _dispatcher;

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
    }

    /// <summary>Targets for the wheel for everything being dragged, filtered by the user's disabled list and max segments.</summary>
    public IReadOnlyList<ConversionTarget> GetTargets(IReadOnlyList<string> draggedPaths)
    {
        var s = _settings();
        return _registry.GetTargetsFor(draggedPaths, s.DisabledTargetIds.ToHashSet(StringComparer.Ordinal), s.MaxWheelSegments);
    }

    // TODO(WP-11): interaction prompt → options (OptionKeys.OutputLabel + PageOrder) → Task.Run(DropPlanner.Plan)
    // → _queue.EnqueueBatch → notify skipped; BatchCompleted → summary; text results → clipboard.
    public Task HandleDropAsync(IReadOnlyList<string> paths, ConversionTarget target)
    {
        _ = (_queue, _notifier, _clipboard, _pageOrderPrompt, _dispatcher, paths, target);
        return Task.CompletedTask;
    }
}
