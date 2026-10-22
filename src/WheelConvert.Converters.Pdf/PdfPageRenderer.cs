using WheelConvert.Core.Contracts;

namespace WheelConvert.Converters.Pdf;

/// <summary>
/// Renders pages for the reorder window (WP-09). Thread-safe: every call is serialised through <see cref="Pdfium"/>,
/// which all instances share.
/// </summary>
public sealed class PdfPageRenderer : IPdfPageRenderer
{
    public Task<int> GetPageCountAsync(string pdfPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(pdfPath);
        if (!PdfiumPlatform.IsSupported)
        {
            throw new PlatformNotSupportedException("PDF rendering needs Windows, Linux or macOS.");
        }

        return Pdfium.GetPageCountAsync(pdfPath, cancellationToken);
    }

    public Task<byte[]> RenderPagePngAsync(string pdfPath, int pageIndex, int maxPixels, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(pdfPath);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPixels);
        if (!PdfiumPlatform.IsSupported)
        {
            throw new PlatformNotSupportedException("PDF rendering needs Windows, Linux or macOS.");
        }

        return Pdfium.RenderThumbnailPngAsync(pdfPath, pageIndex, maxPixels, cancellationToken);
    }
}
