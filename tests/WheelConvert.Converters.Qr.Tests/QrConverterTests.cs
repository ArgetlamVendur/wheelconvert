using ImageMagick;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Output;
using Xunit;
using ZXing;
using ZXing.QrCode;

namespace WheelConvert.Converters.Qr.Tests;

public sealed class QrConverterTests : IDisposable
{
    private readonly string _dir;
    private readonly QrConverter _converter = new();
    private readonly SafeOutputWriter _writer = new();

    public QrConverterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "wc-qr-test-" + Guid.NewGuid().ToString("N"));
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

    // ---- helpers ----

    /// <summary>Gray image of the code at the given pixel size (module size scales to fit).</summary>
    private static MagickImage MakeQr(string text, int size, bool invert = false)
    {
        var hints = new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 4 };
        var matrix = new QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, size, size, hints);
        var bytes = new byte[matrix.Width * matrix.Height];
        for (var y = 0; y < matrix.Height; y++)
        {
            for (var x = 0; x < matrix.Width; x++)
            {
                var black = matrix[x, y];
                bytes[(y * matrix.Width) + x] = (byte)(black ^ invert ? 0 : 255);
            }
        }

        var settings = new MagickReadSettings
        {
            Format = MagickFormat.Gray,
            Width = (uint)matrix.Width,
            Height = (uint)matrix.Height,
            Depth = 8,
        };
        return new MagickImage(bytes, settings);
    }

    private string Save(MagickImage image, string name)
    {
        var path = Path.Combine(_dir, name);
        image.Write(path);
        return path;
    }

    private string SaveQr(string text, string name = "code.png", int size = 300, bool invert = false)
    {
        using var qr = MakeQr(text, size, invert);
        return Save(qr, name);
    }

    private string SaveCanvas(uint w, uint h, string name, params (string Text, int Size, int X, int Y)[] codes)
    {
        using var canvas = new MagickImage(MagickColors.White, w, h);
        foreach (var (text, size, x, y) in codes)
        {
            using var qr = MakeQr(text, size);
            canvas.Composite(qr, x, y);
        }

        return Save(canvas, name);
    }

    private Task<ConversionResult> Read(string path, CancellationToken ct = default) =>
        _converter.ConvertAsync(ConversionRequest.Create(path, TargetCatalog.ImageReadQr), _writer, null, ct);

    private string[] Files() => Directory.GetFiles(_dir).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;

    // ---- acceptance tests ----

    [Fact]
    public void CheckHealth_ReturnsReady()
    {
        Assert.Equal(ConverterHealth.Ready, _converter.CheckHealth());
    }

    [Fact]
    public void CanConvert_OnlyImagesToReadQr()
    {
        Assert.True(_converter.CanConvert(".png", TargetCatalog.ImageReadQr.Id));
        Assert.True(_converter.CanConvert(".jpg", TargetCatalog.ImageReadQr.Id));
        Assert.False(_converter.CanConvert(".pdf", TargetCatalog.ImageReadQr.Id));
        Assert.False(_converter.CanConvert(".png", TargetCatalog.ImagePng.Id));
    }

    [Fact]
    public async Task T1_SingleQr_ReturnsText()
    {
        var path = SaveQr("hello");
        var r = await Read(path);
        Assert.True(r.Success);
        Assert.Equal("hello", r.Text);
        Assert.Empty(r.OutputPaths);
    }

    [Fact]
    public async Task T1b_Url_IsReturnedAsPlainText()
    {
        const string url = "https://example.com/path?a=1&b=2";
        var r = await Read(SaveQr(url));
        Assert.True(r.Success);
        Assert.Equal(url, r.Text);
        Assert.Empty(r.OutputPaths);
    }

    [Fact]
    public void T1c_QrAssemblyDoesNotReferenceProcessLaunching()
    {
        // ADR-009: decoded text is never opened or run. The assembly has no way to start a process.
        var refs = typeof(QrConverter).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.DoesNotContain("System.Diagnostics.Process", refs);
    }

    [Fact]
    public async Task T1d_Jpeg_IsRead()
    {
        using var qr = MakeQr("jpeg works", 300);
        var path = Save(qr, "code.jpg");
        var r = await Read(path);
        Assert.Equal("jpeg works", r.Text);
    }

    [Fact]
    public async Task T2_SmallQrInLargeScreenshot_IsFoundByUpscaleRetry()
    {
        // 29 px code (exactly 1 pixel per module) in a 1600x900 canvas.
        var path = SaveCanvas(1600, 900, "shot.png", ("tiny", 52, 1200, 600));
        var r = await Read(path);
        Assert.True(r.Success, r.Message);
        Assert.Equal("tiny", r.Text);
    }

    [Fact]
    public async Task T3_TwoQrs_AreNewlineSeparatedInReadingOrder()
    {
        // Listed bottom-right first on purpose: order must come from position, not drawing order.
        var path = SaveCanvas(
            900, 600, "two.png",
            ("second", 250, 550, 300),
            ("first", 250, 50, 20));
        var r = await Read(path);
        Assert.True(r.Success, r.Message);
        Assert.Equal("first\nsecond", r.Text);
    }

    [Fact]
    public async Task T3b_TwoQrsSideBySide_AreLeftToRight()
    {
        var path = SaveCanvas(
            900, 400, "row.png",
            ("right", 250, 600, 60),
            ("left", 250, 40, 70));
        var r = await Read(path);
        Assert.Equal("left\nright", r.Text);
    }

    [Fact]
    public async Task T4_NoQr_ReturnsNoResult()
    {
        using var blank = new MagickImage(MagickColors.White, 400, 400);
        var path = Save(blank, "blank.png");
        var r = await Read(path);
        Assert.False(r.Success);
        Assert.Equal(ConversionError.NoResult, r.Error);
        Assert.Equal("No QR code found.", r.Message);
    }

    [Fact]
    public async Task T5_InvertedQr_IsFound()
    {
        var path = SaveQr("inverted", invert: true);
        var r = await Read(path);
        Assert.True(r.Success, r.Message);
        Assert.Equal("inverted", r.Text);
    }

    [Fact]
    public async Task T6_SourceUnchanged_NoFilesCreated()
    {
        var path = SaveQr("keep me");
        var before = File.ReadAllBytes(path);
        var stamp = File.GetLastWriteTimeUtc(path);
        var listing = Files();

        var r = await Read(path);

        Assert.True(r.Success);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
        Assert.Equal(listing, Files());
    }

    [Fact]
    public async Task T6a_MvgPayloadNamedPng_IsCorruptInput_NoFiles()
    {
        // MVG can draw and read files; with the format forced to PNG by the extension it must just fail to decode.
        var path = Path.Combine(_dir, "evil.png");
        File.WriteAllText(path, "push graphic-context\nviewbox 0 0 100 100\nimage over 0,0 0,0 'label:pwned'\npop graphic-context");
        var listing = Files();

        var r = await Read(path);

        Assert.False(r.Success);
        Assert.Equal(ConversionError.CorruptInput, r.Error);
        Assert.Equal(listing, Files());
    }

    [Fact]
    public async Task CorruptFile_IsCorruptInput()
    {
        var path = Path.Combine(_dir, "junk.png");
        File.WriteAllBytes(path, [1, 2, 3, 4, 5, 6, 7, 8, 9]);
        var r = await Read(path);
        Assert.False(r.Success);
        Assert.Equal(ConversionError.CorruptInput, r.Error);
    }

    [Fact]
    public async Task MissingFile_IsSourceMissing()
    {
        var r = await Read(Path.Combine(_dir, "nope.png"));
        Assert.Equal(ConversionError.SourceMissing, r.Error);
    }

    [Fact]
    public async Task UnsupportedExtension_IsUnsupported()
    {
        var path = Path.Combine(_dir, "doc.txt");
        File.WriteAllText(path, "x");
        var r = await Read(path);
        Assert.Equal(ConversionError.Unsupported, r.Error);
    }

    [Fact]
    public async Task Cancellation_Throws()
    {
        var path = SaveQr("cancel");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Read(path, cts.Token));
    }

    [Fact]
    public async Task LongText_IsTruncatedTo4000WithEllipsis()
    {
        var text = string.Concat(Enumerable.Repeat("1234567890", 450));
        using var qr = MakeQr(text, 1500);
        var path = Save(qr, "long.png");
        var r = await Read(path);
        Assert.True(r.Success, r.Message);
        Assert.Equal(4000, r.Text!.Length);
        Assert.EndsWith("…", r.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransparentPng_IsFound()
    {
        using var qr = MakeQr("alpha", 300);
        qr.Transparent(MagickColors.White);
        var path = Save(qr, "alpha.png");
        var r = await Read(path);
        Assert.Equal("alpha", r.Text);
    }
}
