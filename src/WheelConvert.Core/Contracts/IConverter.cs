namespace WheelConvert.Core.Contracts;

/// <summary>
/// A converter module (image, media, PDF, QR). Implementations live in their own project and are
/// registered in the app's composition root.
///
/// RULES (see docs/CONTRACTS.md):
///  - Never open the source for writing. Never move, rename or delete the source.
///  - Write output ONLY to paths from <see cref="IOutputWriter.CreateTempPath"/>, then publish
///    with <see cref="IOutputWriter.Commit"/>. On any failure call <see cref="IOutputWriter.Discard"/>.
///  - Return failures as <see cref="ConversionResult.Fail"/>. Only OperationCanceledException may escape.
///  - Must be safe to call concurrently for different requests.
/// </summary>
public interface IConverter
{
    /// <summary>Stable ID, e.g. "image". Used in logs.</summary>
    string Id { get; }

    /// <summary>Target IDs from <see cref="Formats.TargetCatalog"/> this converter implements.</summary>
    IReadOnlyCollection<string> SupportedTargetIds { get; }

    /// <summary>Fine-grained check, e.g. an image converter may refuse ".ico" input for "image.webp".</summary>
    /// <param name="sourceExtension">Normalised extension, lower-case with dot, e.g. ".png".</param>
    bool CanConvert(string sourceExtension, string targetId);

    /// <summary>Cheap check that dependencies exist (e.g. FFmpeg found). Called at start-up and on refresh.</summary>
    ConverterHealth CheckHealth();

    Task<ConversionResult> ConvertAsync(
        ConversionRequest request,
        IOutputWriter output,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}

public sealed record ConverterHealth(bool IsAvailable, ConversionError Reason, string? Message)
{
    public static ConverterHealth Ready { get; } = new(true, ConversionError.None, null);

    public static ConverterHealth NotImplemented { get; } =
        new(false, ConversionError.NotImplemented, "Not implemented yet.");

    public static ConverterHealth Missing(string message) => new(false, ConversionError.ToolMissing, message);
}
