using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;

namespace WheelConvert.Converters.Qr;

/// <summary>STUB: WP-10 (docs/modules/WP-10-qr-reader.md). Reads QR codes from image files; returns text.</summary>
public sealed class QrConverter : IConverter
{
    public string Id => "qr";

    public IReadOnlyCollection<string> SupportedTargetIds { get; } = [TargetCatalog.ImageReadQr.Id];

    public bool CanConvert(string sourceExtension, string targetId) =>
        targetId == TargetCatalog.ImageReadQr.Id && FileClassifier.Classify(sourceExtension) == FileKind.Image;

    // TODO(WP-10): Ready once ZXing.Net + Magick.NET are wired.
    public ConverterHealth CheckHealth() => ConverterHealth.NotImplemented;

    // TODO(WP-10): return ConversionResult.OkText(...). Never open URLs (ADR-009).
    public Task<ConversionResult> ConvertAsync(
        ConversionRequest request,
        IOutputWriter output,
        IProgress<double>? progress,
        CancellationToken cancellationToken) =>
        Task.FromResult(ConversionResult.Fail(ConversionError.NotImplemented, "QR reading is not implemented yet."));
}
