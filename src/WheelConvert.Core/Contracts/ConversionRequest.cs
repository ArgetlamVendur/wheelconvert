namespace WheelConvert.Core.Contracts;

/// <summary>One file + one target. A multi-file drop becomes several requests (see <see cref="Jobs.DropPlanner"/>).</summary>
public sealed record ConversionRequest(
    string SourcePath,
    ConversionTarget Target,
    IReadOnlyDictionary<string, string> Options)
{
    public static ConversionRequest Create(string sourcePath, ConversionTarget target) =>
        new(sourcePath, target, new Dictionary<string, string>());

    public string? GetOption(string key) => Options.TryGetValue(key, out var value) ? value : null;

    /// <summary>Label for plain format conversions: <see cref="OptionKeys.OutputLabel"/> or "converted".</summary>
    public string OutputLabel =>
        GetOption(OptionKeys.OutputLabel) is { Length: > 0 } label ? label : OptionKeys.DefaultOutputLabel;
}

/// <summary>Well-known option keys. Add new keys here, never as string literals in converters.</summary>
public static class OptionKeys
{
    /// <summary>Comma-separated 1-based page numbers in the new order, e.g. "3,1,2". Used by pdf.reorder.</summary>
    public const string PageOrder = "pageOrder";

    /// <summary>Lossy quality 1–100. Optional; converters choose a sensible default when absent.</summary>
    public const string Quality = "quality";

    /// <summary>Label for plain format conversions ("photo (converted).jpg"). Filled from AppSettings.OutputLabel by the app (WP-11).</summary>
    public const string OutputLabel = "outputLabel";

    public const string DefaultOutputLabel = "converted";
}
