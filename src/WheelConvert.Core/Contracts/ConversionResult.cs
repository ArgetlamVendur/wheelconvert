namespace WheelConvert.Core.Contracts;

public enum ConversionError
{
    None = 0,
    SourceMissing,
    Unsupported,
    NotImplemented,
    ToolMissing,
    ToolFailed,
    PasswordProtected,
    CorruptInput,
    OutputFailed,
    Cancelled,

    /// <summary>The operation ran fine but found nothing (e.g. no QR code in the image).</summary>
    NoResult,
    Unknown,
}

/// <summary>
/// Outcome of one request. Converters return failures; they do not throw (except OperationCanceledException).
/// On failure, <see cref="OutputPaths"/> lists pieces that were already committed (partial multi-output results).
/// </summary>
public sealed record ConversionResult(
    bool Success,
    IReadOnlyList<string> OutputPaths,
    string? Text,
    ConversionError Error,
    string? Message)
{
    public static ConversionResult Ok(params string[] outputPaths) =>
        new(true, outputPaths, null, ConversionError.None, null);

    public static ConversionResult Ok(IReadOnlyList<string> outputPaths) =>
        new(true, outputPaths, null, ConversionError.None, null);

    public static ConversionResult OkText(string text) =>
        new(true, Array.Empty<string>(), text, ConversionError.None, null);

    public static ConversionResult Fail(ConversionError error, string message) =>
        new(false, Array.Empty<string>(), null, error, message);

    /// <summary>Failure after some outputs were committed (CONTRACTS C2 S5). They stay on disk and are listed here.</summary>
    public static ConversionResult Fail(ConversionError error, string message, IReadOnlyList<string> committedPaths) =>
        new(false, committedPaths ?? Array.Empty<string>(), null, error, message);
}
