namespace WheelConvert.Core.Contracts;

/// <summary>
/// One file + one target. A multi-file drop becomes several requests (see <see cref="Jobs.DropPlanner"/>),
/// except for targets with <see cref="ConversionTarget.CombinesInputs"/>: those get one request whose
/// <see cref="SourcePath"/> is the first file and whose other files are in <see cref="AdditionalSourcePaths"/>.
/// Output is named after, and written next to, <see cref="SourcePath"/> (CONTRACTS C2 S6). Every input is read-only.
/// </summary>
public sealed record ConversionRequest(
    string SourcePath,
    ConversionTarget Target,
    IReadOnlyDictionary<string, string> Options,
    IReadOnlyList<string>? AdditionalSourcePaths = null)
{
    /// <summary><see cref="SourcePath"/> followed by <see cref="AdditionalSourcePaths"/>.</summary>
    public IReadOnlyList<string> AllSourcePaths =>
        AdditionalSourcePaths is { Count: > 0 } more ? [SourcePath, .. more] : [SourcePath];

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

    /// <summary>image.resize: longest edge in pixels. Exactly one of LongEdge, Percent or Width/Height is set (ADR-011 section 5).</summary>
    public const string ResizeLongEdge = "resizeLongEdge";

    /// <summary>image.resize: scale in percent.</summary>
    public const string ResizePercent = "resizePercent";

    /// <summary>image.resize: target width in pixels (may be combined with <see cref="ResizeHeight"/>).</summary>
    public const string ResizeWidth = "resizeWidth";

    /// <summary>image.resize: target height in pixels (may be combined with <see cref="ResizeWidth"/>).</summary>
    public const string ResizeHeight = "resizeHeight";

    /// <summary>image.resize: "true" or "false"; whether images may be made larger.</summary>
    public const string ResizeAllowEnlarge = "resizeAllowEnlarge";
}
