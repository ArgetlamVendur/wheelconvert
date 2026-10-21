using WheelConvert.Core.Contracts;

namespace WheelConvert.Converters.Pdf;

/// <summary>STUB: WP-08. Renders pages for the reorder window (WP-09) and pdf.to-images. Must be thread-safe.</summary>
public sealed class PdfPageRenderer : IPdfPageRenderer
{
    // TODO(WP-08): PDFtoImage page count. Encrypted → PdfPasswordProtectedException, unreadable → InvalidDataException.
    public Task<int> GetPageCountAsync(string pdfPath, CancellationToken cancellationToken) =>
        Task.FromException<int>(new NotSupportedException("PDF rendering is not implemented yet (WP-08)."));

    // TODO(WP-08): render with PDFtoImage, longest side ≤ maxPixels, encode PNG. Serialise PDFium calls.
    public Task<byte[]> RenderPagePngAsync(string pdfPath, int pageIndex, int maxPixels, CancellationToken cancellationToken) =>
        Task.FromException<byte[]>(new NotSupportedException("PDF rendering is not implemented yet (WP-08)."));
}
