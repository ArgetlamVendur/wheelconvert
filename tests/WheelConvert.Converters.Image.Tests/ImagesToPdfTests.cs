using System.Text;
using ImageMagick;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Output;
using Xunit;

namespace WheelConvert.Converters.Image.Tests;

/// <summary>WP-15 acceptance tests 1-7. Images are generated here; nothing outside the temp folder is touched.</summary>
[Collection("MagickLimits")]
public sealed class ImagesToPdfTests : IDisposable
{
    private readonly string _dir;
    private readonly ListLog _log = new();
    private readonly ImageConverter _converter;
    private readonly SafeOutputWriter _writer = new();

    public ImagesToPdfTests()
    {
        _converter = new ImageConverter(_log);
        _dir = Path.Combine(Path.GetTempPath(), "wc-pdf-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Best effort clean-up of the temp folder.
        }
    }

    [Fact]
    public async Task OnePortraitJpeg_OnePortraitA4Page_LabelConverted()
    {
        var jpg = MakeImage("photo.jpg", 1200, 1600, MagickColors.Red);

        var result = await Run(jpg);

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(_dir, "photo (converted).pdf"), result.OutputPaths[0]);
        var pages = ReadPages(result.OutputPaths[0]);
        var page = Assert.Single(pages);
        Assert.Equal(595, page.Width, 0.5);
        Assert.Equal(842, page.Height, 0.5);
    }

    [Fact]
    public async Task ThreeImages_PortraitLandscapePortrait_AlphaOnWhite_LabelCombined()
    {
        var a = MakeImage("scan 01.jpg", 1200, 1600, MagickColors.Red);
        var b = MakeImage("scan 02.jpg", 1600, 1200, MagickColors.Blue);
        var c = Path.Combine(_dir, "scan 03.png");
        using (var transparent = new MagickImage(MagickColors.Transparent, 800, 1200))
        {
            transparent.Write(c, MagickFormat.Png32);
        }

        var result = await Run(a, b, c);

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(_dir, "scan 01 (combined).pdf"), result.OutputPaths[0]);
        var pages = ReadPages(result.OutputPaths[0]);
        Assert.Equal(3, pages.Count);
        Assert.True(pages[0].Height > pages[0].Width);
        Assert.True(pages[1].Width > pages[1].Height);
        Assert.True(pages[2].Height > pages[2].Width);

        // Fully transparent source: every embedded pixel must be white and there must be no soft mask.
        var third = pages[2].Images.Single();
        Assert.False(third.Dict.Elements.ContainsKey("/SMask"));
        Assert.True(third.Dict.Stream.TryUncompress());
        var raw = third.Dict.Stream.Value;
        Assert.NotEmpty(raw);
        Assert.True(raw.All(v => v == 255), "embedded pixels are not all white");
    }

    [Fact]
    public async Task ExifOrientation6_PageShowsImageUpright()
    {
        // Stored 1600 x 1200 (landscape) with orientation 6 (rotate 90 clockwise): upright it is 1200 x 1600.
        var path = Path.Combine(_dir, "sideways.jpg");
        using (var image = new MagickImage(MagickColors.Green, 1600, 1200))
        {
            image.Orientation = Orientation.RightTop;
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.Orientation, (ushort)6);
            image.SetProfile(exif);
            image.Write(path, MagickFormat.Jpeg);
        }

        var result = await Run(path);

        Assert.True(result.Success);
        var page = Assert.Single(ReadPages(result.OutputPaths[0]));
        Assert.True(page.Height > page.Width);
        var embedded = page.Images.Single();
        Assert.Equal(1200, embedded.Width);
        Assert.Equal(1600, embedded.Height);
    }

    [Fact]
    public async Task LargePhoto_IsDownscaledToAtMost300Dpi()
    {
        var jpg = MakeImage("big.jpg", 6000, 4000, MagickColors.Orange);

        var result = await Run(jpg);

        Assert.True(result.Success);
        var page = Assert.Single(ReadPages(result.OutputPaths[0]));
        var embedded = page.Images.Single();
        Assert.True(page.Width > page.Height);
        var boxW = page.Width - 36;
        var boxH = page.Height - 36;
        var placedW = 6000 * Math.Min(boxW / 6000, boxH / 4000);
        var dpi = embedded.Width / placedW * 72;
        Assert.True(dpi <= 300.5, $"embedded at {dpi} DPI");
        Assert.True(dpi > 290, $"downscaled too far: {dpi} DPI");
        Assert.True(embedded.Width < 6000);
    }

    [Fact]
    public async Task EmbeddedImage_HasNoExifOrGpsMetadata()
    {
        const string marker = "SECRET-GPS-MARKER";
        var path = Path.Combine(_dir, "located.jpg");
        using (var image = new MagickImage(MagickColors.Gray, 800, 600))
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.ImageDescription, marker);
            exif.SetValue(ExifTag.Orientation, (ushort)1);
            image.SetProfile(exif);
            image.Write(path, MagickFormat.Jpeg);
        }

        Assert.Contains(marker, Encoding.ASCII.GetString(File.ReadAllBytes(path)), StringComparison.Ordinal);

        var result = await Run(path);

        Assert.True(result.Success);
        var pdfText = Encoding.Latin1.GetString(File.ReadAllBytes(result.OutputPaths[0]));
        Assert.DoesNotContain(marker, pdfText, StringComparison.Ordinal);
        Assert.DoesNotContain("Exif", pdfText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OneCorruptInputAmongThree_FailsNamingIt_NothingLeftBehind()
    {
        var a = MakeImage("a.jpg", 400, 300, MagickColors.Red);
        var bad = Path.Combine(_dir, "broken.jpg");
        File.WriteAllText(bad, "this is not a jpeg");
        var c = MakeImage("c.jpg", 400, 300, MagickColors.Blue);

        var result = await Run(a, bad, c);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.CorruptInput, result.Error);
        Assert.Contains("broken.jpg", result.Message, StringComparison.Ordinal);
        Assert.Equal(["a.jpg", "broken.jpg", "c.jpg"], Directory.GetFiles(_dir).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public async Task CancelDuring30Images_NoTempLeft()
    {
        var paths = new List<string>();
        for (var i = 1; i <= 30; i++)
        {
            paths.Add(MakeImage($"p{i:00}.png", 200, 200, MagickColors.Red));
        }

        using var cts = new CancellationTokenSource();
        var reports = 0;
        var progress = new SyncProgress(count =>
        {
            reports = count;
            if (count == 3)
            {
                cts.Cancel();
            }
        });
        var request = new ConversionRequest(paths[0], TargetCatalog.ImageToPdf, new Dictionary<string, string>(), paths.Skip(1).ToList());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _converter.ConvertAsync(request, _writer, progress, cts.Token));

        // Cancelled between images: at most one more image is finished after the cancel, not all 30.
        Assert.InRange(reports, 3, 4);
        Assert.Equal(30, Directory.GetFiles(_dir).Length);
        Assert.DoesNotContain(Directory.GetFiles(_dir), f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f).StartsWith(".~wc-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CorruptInput_LogLineHasFileNameButNoFolderPath()
    {
        var a = MakeImage("a.jpg", 400, 300, MagickColors.Red);
        var bad = Path.Combine(_dir, "broken.jpg");
        File.WriteAllText(bad, "this is not a jpeg");

        var result = await Run(a, bad);

        Assert.False(result.Success);
        var line = Assert.Single(_log.Lines);
        Assert.Contains("broken.jpg", line, StringComparison.Ordinal);
        Assert.DoesNotContain(_dir, line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_dir.Replace(Path.DirectorySeparatorChar, '/'), line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, '/'), line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CommitFails_TempFileIsDiscarded()
    {
        var jpg = MakeImage("photo.jpg", 400, 300, MagickColors.Red);
        var writer = new ThrowingCommitWriter(_writer);
        var request = ConversionRequest.Create(jpg, TargetCatalog.ImageToPdf);

        var result = await _converter.ConvertAsync(request, writer, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.OutputFailed, result.Error);
        // The temp PDF really existed when Commit ran, so Discard is what removed it.
        Assert.True(writer.TempExistedAtCommit);
        Assert.Equal([jpg], Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task EmbeddingFormat_JpegSourceIsDct_PngSourceIsFlate()
    {
        var jpg = MakeImage("a.jpg", 400, 300, MagickColors.Red);
        var png = MakeImage("b.png", 400, 300, MagickColors.Blue);

        var result = await Run(jpg, png);

        Assert.True(result.Success);
        var pages = ReadPages(result.OutputPaths[0]);
        Assert.Contains("DCTDecode", pages[0].Images.Single().Dict.Elements["/Filter"]?.ToString(), StringComparison.Ordinal);
        Assert.Contains("FlateDecode", pages[1].Images.Single().Dict.Elements["/Filter"]?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task JpegSource_IsEncodedAtQuality85()
    {
        // Noise so the quantisation tables, not the picture, decide the estimated quality.
        var path = Path.Combine(_dir, "noise.jpg");
        using (var image = new MagickImage(MagickColors.Gray, 600, 400))
        {
            image.AddNoise(NoiseType.Gaussian);
            image.Quality = 100;
            image.Write(path, MagickFormat.Jpeg);
        }

        var result = await Run(path);

        Assert.True(result.Success);
        var embedded = ReadPages(result.OutputPaths[0]).Single().Images.Single();
        using var decoded = new MagickImage(embedded.Dict.Stream.Value, MagickFormat.Jpeg);
        Assert.Equal(85u, decoded.Quality);
    }

    [Fact]
    public async Task CmykJpeg_IsEmbeddedAsRgb()
    {
        var path = Path.Combine(_dir, "cmyk.jpg");
        using (var image = new MagickImage(MagickColors.Red, 400, 300))
        {
            image.ColorSpace = ColorSpace.CMYK;
            image.Write(path, MagickFormat.Jpeg);
        }

        Assert.Equal(ColorSpace.CMYK, new MagickImageInfo(path).ColorSpace);

        var result = await Run(path);

        Assert.True(result.Success);
        var embedded = ReadPages(result.OutputPaths[0]).Single().Images.Single();
        Assert.Equal("/DeviceRGB", embedded.Dict.Elements.GetName("/ColorSpace"));
    }

    [Fact]
    public async Task AnimatedGif_OnlyFirstFrameBecomesOnePage()
    {
        var path = Path.Combine(_dir, "anim.gif");
        using (var frames = new MagickImageCollection())
        {
            frames.Add(new MagickImage(MagickColors.Red, 200, 100));
            frames.Add(new MagickImage(MagickColors.Blue, 200, 100));
            frames.Write(path, MagickFormat.Gif);
        }

        var result = await Run(path);

        Assert.True(result.Success);
        var page = Assert.Single(ReadPages(result.OutputPaths[0]));
        var embedded = page.Images.Single();
        Assert.True(embedded.Dict.Stream.TryUncompress());
        var raw = embedded.Dict.Stream.Value;
        Assert.Equal(255, raw[0]);
        Assert.Equal(0, raw[1]);
        Assert.Equal(0, raw[2]);
    }

    [Fact]
    public void PdfTarget_IsSupported_ForEveryImageInput()
    {
        Assert.Contains(TargetCatalog.ImageToPdf.Id, _converter.SupportedTargetIds);
        foreach (var ext in new[] { ".jpg", ".png", ".heic", ".webp", ".gif", ".tif", ".bmp", ".avif" })
        {
            Assert.True(_converter.CanConvert(ext, TargetCatalog.ImageToPdf.Id), ext);
        }
    }

    private Task<ConversionResult> Run(params string[] paths)
    {
        var request = new ConversionRequest(
            paths[0],
            TargetCatalog.ImageToPdf,
            new Dictionary<string, string>(),
            paths.Length > 1 ? paths.Skip(1).ToList() : null);
        return _converter.ConvertAsync(request, _writer, null, CancellationToken.None);
    }

    private string MakeImage(string name, uint width, uint height, IMagickColor<byte> color)
    {
        var path = Path.Combine(_dir, name);
        using var image = new MagickImage(color, width, height);
        image.Write(path);
        return path;
    }

    private static List<PageInfo> ReadPages(string pdfPath)
    {
        using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        var pages = new List<PageInfo>();
        foreach (var page in document.Pages)
        {
            var images = new List<EmbeddedImage>();
            var xObjects = page.Resources.Elements.GetDictionary("/XObject");
            if (xObjects is not null)
            {
                foreach (var item in xObjects.Elements.Values)
                {
                    if (item is PdfReference { Value: PdfDictionary dict } && dict.Elements.GetName("/Subtype") == "/Image")
                    {
                        images.Add(new EmbeddedImage(dict, dict.Elements.GetInteger("/Width"), dict.Elements.GetInteger("/Height")));
                    }
                }
            }

            pages.Add(new PageInfo(page.Width.Point, page.Height.Point, images));
        }

        return pages;
    }

    private sealed record EmbeddedImage(PdfDictionary Dict, int Width, int Height);

    private sealed record PageInfo(double Width, double Height, List<EmbeddedImage> Images);

    private sealed class ThrowingCommitWriter(IOutputWriter inner) : IOutputWriter
    {
        public bool TempExistedAtCommit { get; private set; }

        public string CreateTempPath(string sourcePath, string outputExtension) => inner.CreateTempPath(sourcePath, outputExtension);

        public string Commit(string tempPath, string sourcePath, string outputExtension, string label)
        {
            TempExistedAtCommit = File.Exists(tempPath);
            throw new IOException("disk full");
        }

        public bool Discard(string tempPath) => inner.Discard(tempPath);
    }

    private sealed class SyncProgress(Action<int> onReport) : IProgress<double>
    {
        private int _count;

        public void Report(double value) => onReport(++_count);
    }
}
