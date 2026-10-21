namespace WheelConvert.Core.Contracts;

// Small service contracts implemented by the app shell (WP-05, WP-09, WP-11) or the PDF module (WP-08).
// Kept together because each is a few lines; split if any grows.

public enum NotificationKind
{
    Success,
    Warning,
    Error,
}

/// <summary>User-visible notification (tray balloon / toast). Callable from any thread. Never throws.</summary>
public interface INotifier
{
    /// <param name="pathToReveal">File to select in Explorer when the notification is clicked (first output for multi-output results).</param>
    void Notify(NotificationKind kind, string title, string message, string? pathToReveal = null);
}

/// <summary>Puts text on the clipboard (QR results). Callable from any thread. Never throws; false if the clipboard stayed locked.</summary>
public interface IClipboardService
{
    Task<bool> SetTextAsync(string text);
}

/// <summary>Asks the user for a new page order (WP-09). Returns 1-based page numbers, or null if cancelled.</summary>
public interface IPageOrderPrompt
{
    Task<IReadOnlyList<int>?> PromptAsync(string pdfPath, CancellationToken cancellationToken);
}

/// <summary>Thrown by <see cref="IPdfPageRenderer"/> for encrypted PDFs (distinct from a real permission error).</summary>
public sealed class PdfPasswordProtectedException : Exception
{
    public PdfPasswordProtectedException()
        : base("This PDF is password-protected.")
    {
    }

    public PdfPasswordProtectedException(string message)
        : base(message)
    {
    }

    public PdfPasswordProtectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Renders PDF pages for previews. Implemented by the PDF module (WP-08), consumed by WP-09. Thread-safe.</summary>
public interface IPdfPageRenderer
{
    /// <exception cref="InvalidDataException">The file is not a readable PDF.</exception>
    /// <exception cref="PdfPasswordProtectedException">The PDF is encrypted.</exception>
    Task<int> GetPageCountAsync(string pdfPath, CancellationToken cancellationToken);

    /// <summary>PNG bytes, longest side ≤ <paramref name="maxPixels"/>. <paramref name="pageIndex"/> is 0-based.</summary>
    Task<byte[]> RenderPagePngAsync(string pdfPath, int pageIndex, int maxPixels, CancellationToken cancellationToken);
}

/// <summary>Logging. Implementations never throw.</summary>
public interface ILog
{
    void Info(string message);

    void Warn(string message, Exception? exception = null);

    void Error(string message, Exception? exception = null);
}
