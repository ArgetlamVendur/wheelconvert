using WheelConvert.Core.Contracts;

namespace WheelConvert.App.Pdf;

/// <summary>
/// STUB: WP-09 (docs/modules/WP-09-pdf-reorder-window.md). Opens a window of page thumbnails the user can
/// drag into a new order. Uses IPdfPageRenderer (WP-08) for thumbnails. Constructor is FROZEN (CompositionRoot calls it).
/// </summary>
public sealed class PageOrderPrompt : IPageOrderPrompt
{
    private readonly IPdfPageRenderer _renderer;
    private readonly ILog _log;

    public PageOrderPrompt(IPdfPageRenderer renderer, ILog log)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    // TODO(WP-09): show PageOrderWindow; return 1-based order, or null if cancelled.
    public Task<IReadOnlyList<int>?> PromptAsync(string pdfPath, CancellationToken cancellationToken)
    {
        _ = (_renderer, _log);
        return Task.FromResult<IReadOnlyList<int>?>(null);
    }
}
