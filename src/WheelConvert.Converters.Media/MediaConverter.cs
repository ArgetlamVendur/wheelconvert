using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;

namespace WheelConvert.Converters.Media;

/// <summary>
/// STUB: WP-07 (docs/modules/WP-07-media-converter.md). Audio/video conversion via external FFmpeg.
/// </summary>
public sealed class MediaConverter : IConverter
{
    private readonly Func<string?> _ffmpegPathProvider;

    /// <param name="ffmpegPathProvider">Returns the configured FFmpeg path (AppSettings.FfmpegPath) or null to auto-detect.</param>
    public MediaConverter(Func<string?> ffmpegPathProvider)
    {
        _ffmpegPathProvider = ffmpegPathProvider ?? throw new ArgumentNullException(nameof(ffmpegPathProvider));
    }

    public string Id => "media";

    public IReadOnlyCollection<string> SupportedTargetIds { get; } =
    [
        TargetCatalog.AudioMp3.Id, TargetCatalog.AudioM4a.Id, TargetCatalog.AudioFlac.Id,
        TargetCatalog.AudioWav.Id, TargetCatalog.AudioOgg.Id, TargetCatalog.AudioOpus.Id,
        TargetCatalog.VideoMp4.Id, TargetCatalog.VideoWebm.Id, TargetCatalog.VideoGif.Id,
        TargetCatalog.VideoExtractMp3.Id,
    ];

    public bool CanConvert(string sourceExtension, string targetId)
    {
        var kind = FileClassifier.Classify(sourceExtension);
        var target = TargetCatalog.Find(targetId);
        return target is not null
            && SupportedTargetIds.Contains(targetId)
            && (kind == FileKind.Audio || kind == FileKind.Video)
            && kind == target.InputKind;
    }

    // TODO(WP-07): ToolLocator.Find("ffmpeg", _ffmpegPathProvider()) → Ready, or Missing("Install FFmpeg: winget install Gyan.FFmpeg").
    public ConverterHealth CheckHealth()
    {
        _ = _ffmpegPathProvider;
        return ConverterHealth.NotImplemented;
    }

    // TODO(WP-07): implement per the WP file. Use ProcessRunner only (CONTRACTS C9).
    public Task<ConversionResult> ConvertAsync(
        ConversionRequest request,
        IOutputWriter output,
        IProgress<double>? progress,
        CancellationToken cancellationToken) =>
        Task.FromResult(ConversionResult.Fail(ConversionError.NotImplemented, "Audio/video conversion is not implemented yet."));
}
