namespace WheelConvert.Converters.Pdf;

/// <summary>The embedded 1-page PDF used by <see cref="PdfConverter.CheckHealth"/>, so the check never touches user files.</summary>
internal static class PdfHealthSample
{
    private const string ResourceName = "WheelConvert.Converters.Pdf.health-check.pdf";

    public static Stream Open() =>
        typeof(PdfHealthSample).Assembly.GetManifestResourceStream(ResourceName)
        ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
}
