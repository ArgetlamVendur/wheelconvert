using WheelConvert.Core.Contracts;

namespace WheelConvert.Core.Formats;

/// <summary>
/// Every conversion the app can offer. Single source of truth for target IDs.
/// Adding a target: add it here + implement it in a converter's SupportedTargetIds. Renaming an ID
/// breaks users' settings files: don't (see docs/CONTRACTS.md).
/// </summary>
public static class TargetCatalog
{
    // Images
    public static readonly ConversionTarget ImageJpg = new("image.jpg", "JPG", FileKind.Image, ".jpg", 10);
    public static readonly ConversionTarget ImagePng = new("image.png", "PNG", FileKind.Image, ".png", 20);
    public static readonly ConversionTarget ImageWebp = new("image.webp", "WebP", FileKind.Image, ".webp", 30);
    public static readonly ConversionTarget ImageIco = new("image.ico", "ICO", FileKind.Image, ".ico", 40);
    public static readonly ConversionTarget ImageReadQr = new("image.read-qr", "QR", FileKind.Image, null, 50, ProducesText: true);

    // Audio
    public static readonly ConversionTarget AudioMp3 = new("audio.mp3", "MP3", FileKind.Audio, ".mp3", 10);
    public static readonly ConversionTarget AudioM4a = new("audio.m4a", "M4A", FileKind.Audio, ".m4a", 20);
    public static readonly ConversionTarget AudioFlac = new("audio.flac", "FLAC", FileKind.Audio, ".flac", 30);
    public static readonly ConversionTarget AudioWav = new("audio.wav", "WAV", FileKind.Audio, ".wav", 40);
    public static readonly ConversionTarget AudioOgg = new("audio.ogg", "OGG", FileKind.Audio, ".ogg", 50);
    public static readonly ConversionTarget AudioOpus = new("audio.opus", "Opus", FileKind.Audio, ".opus", 60);

    // Video
    public static readonly ConversionTarget VideoMp4 = new("video.mp4", "MP4", FileKind.Video, ".mp4", 10);
    public static readonly ConversionTarget VideoWebm = new("video.webm", "WebM", FileKind.Video, ".webm", 20);
    public static readonly ConversionTarget VideoGif = new("video.gif", "GIF", FileKind.Video, ".gif", 30);
    public static readonly ConversionTarget VideoExtractMp3 = new("video.extract-mp3", "Audio", FileKind.Video, ".mp3", 40);

    // PDF
    public static readonly ConversionTarget PdfSplitPages = new("pdf.split-pages", "Split", FileKind.Pdf, ".pdf", 10, IsFormatChange: false);
    public static readonly ConversionTarget PdfToImages = new("pdf.to-images", "Images", FileKind.Pdf, ".png", 20);
    public static readonly ConversionTarget PdfRotate90 = new("pdf.rotate-90", "Rotate", FileKind.Pdf, ".pdf", 30, IsFormatChange: false);
    public static readonly ConversionTarget PdfReorder = new("pdf.reorder", "Reorder", FileKind.Pdf, ".pdf", 40, RequiresInteraction: true, IsFormatChange: false);
    public static readonly ConversionTarget PdfSplitHalf = new("pdf.split-half", "Halves", FileKind.Pdf, ".pdf", 50, IsFormatChange: false);
    public static readonly ConversionTarget PdfExtractOdd = new("pdf.extract-odd", "Odd", FileKind.Pdf, ".pdf", 60, IsFormatChange: false);
    public static readonly ConversionTarget PdfExtractEven = new("pdf.extract-even", "Even", FileKind.Pdf, ".pdf", 70, IsFormatChange: false);
    public static readonly ConversionTarget PdfReverse = new("pdf.reverse", "Reverse", FileKind.Pdf, ".pdf", 80, IsFormatChange: false);

    public static IReadOnlyList<ConversionTarget> All { get; } =
    [
        ImageJpg, ImagePng, ImageWebp, ImageIco, ImageReadQr,
        AudioMp3, AudioM4a, AudioFlac, AudioWav, AudioOgg, AudioOpus,
        VideoMp4, VideoWebm, VideoGif, VideoExtractMp3,
        PdfSplitPages, PdfToImages, PdfRotate90, PdfReorder, PdfSplitHalf, PdfExtractOdd, PdfExtractEven, PdfReverse,
    ];

    private static readonly Dictionary<string, ConversionTarget> ById =
        All.ToDictionary(t => t.Id, StringComparer.Ordinal);

    public static ConversionTarget? Find(string id) => ById.GetValueOrDefault(id);
}
