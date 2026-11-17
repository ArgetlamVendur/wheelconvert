using System.IO;
using System.Windows;
using WheelConvert.Core.Contracts;

namespace WheelConvert.App.Pdf;

/// <summary>Outcome of opening a PDF for reordering: a page count, or the message to show the user.</summary>
public sealed record PageProbe(int PageCount, string? ErrorMessage);

/// <summary>
/// Shows <see cref="PageOrderWindow"/> modally and returns the 1-based page order, or null on Cancel/close
/// (CONTRACTS C6). Never writes anything itself: the PDF converter produces the output.
/// Constructor is FROZEN (CompositionRoot calls it).
/// </summary>
public sealed class PageOrderPrompt : IPageOrderPrompt
{
    public const int MaxPages = 300;

    private const string Caption = "WheelConvert";

    private readonly IPdfPageRenderer _renderer;
    private readonly ILog _log;

    public PageOrderPrompt(IPdfPageRenderer renderer, ILog log)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Reads the page count and maps renderer errors to user text (requirements 5 and 6). Never throws except on cancellation.</summary>
    public static async Task<PageProbe> ProbeAsync(IPdfPageRenderer renderer, ILog log, string pdfPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(log);
        try
        {
            var count = await renderer.GetPageCountAsync(pdfPath, cancellationToken).ConfigureAwait(false);
            if (count > MaxPages)
            {
                return new PageProbe(count, $"Too many pages to reorder here ({count}).");
            }

            return count < 1
                ? new PageProbe(count, "This file can't be read as a PDF.")
                : new PageProbe(count, null);
        }
        catch (PdfPasswordProtectedException)
        {
            return new PageProbe(0, "This PDF is password-protected.");
        }
        catch (InvalidDataException)
        {
            return new PageProbe(0, "This file can't be read as a PDF.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            log.Error("Could not read PDF for reordering.", ex);
            return new PageProbe(0, "Couldn't open this PDF for reordering. Details are in the log.");
        }
    }

    public async Task<IReadOnlyList<int>?> PromptAsync(string pdfPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(pdfPath);
        var dispatcher = Application.Current.Dispatcher;

        var probe = await ProbeAsync(_renderer, _log, pdfPath, cancellationToken).ConfigureAwait(true);
        if (probe.ErrorMessage is not null)
        {
            await dispatcher.InvokeAsync(() => MessageBox.Show(probe.ErrorMessage, Caption, MessageBoxButton.OK, MessageBoxImage.Warning));
            return null;
        }

        return await dispatcher.InvokeAsync(() =>
        {
            var viewModel = new PageOrderViewModel(probe.PageCount);
            var window = new PageOrderWindow(viewModel, _renderer, _log, pdfPath);
            window.ShowDialog();
            return viewModel.FinalResult;
        });
    }
}
