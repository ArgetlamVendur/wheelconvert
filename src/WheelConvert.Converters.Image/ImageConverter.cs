using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;

namespace WheelConvert.Converters.Image;

/// <summary>
/// STUB: WP-06 (docs/modules/WP-06-image-converter.md). Converts between image formats with Magick.NET.
/// Until implemented, CheckHealth reports NotImplemented so its wheel segments stay hidden.
/// </summary>
public sealed class ImageConverter : IConverter
{
    // Inputs this converter will accept (subset of FileClassifier's image extensions).
    private static readonly HashSet<string> Inputs = new(StringComparer.Ordinal)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif", ".bmp", ".tif", ".tiff", ".gif", ".ico", ".avif",
    };

    public string Id => "image";

    public IReadOnlyCollection<string> SupportedTargetIds { get; } =
    [
        TargetCatalog.ImageJpg.Id,
        TargetCatalog.ImagePng.Id,
        TargetCatalog.ImageWebp.Id,
        TargetCatalog.ImageIco.Id,
    ];

    public bool CanConvert(string sourceExtension, string targetId) =>
        Inputs.Contains(sourceExtension) && SupportedTargetIds.Contains(targetId);

    // TODO(WP-06): return Ready once Magick.NET is wired; Missing(...) if the native library fails to load.
    public ConverterHealth CheckHealth() => ConverterHealth.NotImplemented;

    // TODO(WP-06): implement per the WP file. Follow docs/CONTRACTS.md C1–C2.
    public Task<ConversionResult> ConvertAsync(
        ConversionRequest request,
        IOutputWriter output,
        IProgress<double>? progress,
        CancellationToken cancellationToken) =>
        Task.FromResult(ConversionResult.Fail(ConversionError.NotImplemented, "Image conversion is not implemented yet."));
}
