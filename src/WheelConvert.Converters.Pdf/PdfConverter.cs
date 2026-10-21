using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;

namespace WheelConvert.Converters.Pdf;

/// <summary>
/// STUB: WP-08 (docs/modules/WP-08-pdf-operations.md). Split / halves / odd / even / rotate / reverse /
/// reorder / to-images.
/// </summary>
public sealed class PdfConverter : IConverter
{
    public string Id => "pdf";

    public IReadOnlyCollection<string> SupportedTargetIds { get; } =
    [
        TargetCatalog.PdfSplitPages.Id, TargetCatalog.PdfToImages.Id, TargetCatalog.PdfRotate90.Id,
        TargetCatalog.PdfReorder.Id, TargetCatalog.PdfSplitHalf.Id, TargetCatalog.PdfExtractOdd.Id,
        TargetCatalog.PdfExtractEven.Id, TargetCatalog.PdfReverse.Id,
    ];

    public bool CanConvert(string sourceExtension, string targetId) =>
        sourceExtension == ".pdf" && SupportedTargetIds.Contains(targetId);

    // TODO(WP-08): Ready once PDFsharp + PDFtoImage are wired (try loading PDFium here; Missing(...) on failure).
    public ConverterHealth CheckHealth() => ConverterHealth.NotImplemented;

    // TODO(WP-08): implement per the WP file. Multi-output rules: CONTRACTS C2 S5.
    public Task<ConversionResult> ConvertAsync(
        ConversionRequest request,
        IOutputWriter output,
        IProgress<double>? progress,
        CancellationToken cancellationToken) =>
        Task.FromResult(ConversionResult.Fail(ConversionError.NotImplemented, "PDF tools are not implemented yet."));
}
