namespace WheelConvert.Core.Contracts;

/// <summary>
/// One option the wheel can show, e.g. "JPG" for images. Defined centrally in
/// <see cref="Formats.TargetCatalog"/> so the wheel and the converters agree on IDs.
/// </summary>
/// <param name="Id">Stable ID, e.g. "image.jpg". Never rename: settings files store these.</param>
/// <param name="Label">Short text shown on the wheel segment (≤ 8 characters).</param>
/// <param name="InputKind">The kind of file this target accepts.</param>
/// <param name="OutputExtension">Extension of the produced file(s) incl. dot, or null when the target produces text only.</param>
/// <param name="Priority">Lower = shown first on the wheel.</param>
/// <param name="RequiresInteraction">True when the app must ask the user something before converting (e.g. page order).</param>
/// <param name="ProducesText">True when the useful result is text (e.g. QR) rather than a file.</param>
/// <param name="IsFormatChange">
/// True for "convert to another format" targets: hidden when the source is already that format (PNG→PNG).
/// False for operations that keep the format (PDF rotate/split → PDF): never hidden for that reason.
/// </param>
public sealed record ConversionTarget(
    string Id,
    string Label,
    FileKind InputKind,
    string? OutputExtension,
    int Priority,
    bool RequiresInteraction = false,
    bool ProducesText = false,
    bool IsFormatChange = true)
{
    /// <summary>True when this target makes no sense for a source with <paramref name="sourceExtension"/> (already that format).</summary>
    public bool IsRedundantFor(string sourceExtension) =>
        IsFormatChange && OutputExtension is not null && Formats.FileClassifier.AreEquivalent(OutputExtension, sourceExtension);
}
