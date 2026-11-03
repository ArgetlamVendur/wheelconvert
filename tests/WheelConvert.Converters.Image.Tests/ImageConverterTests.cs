using System.Security.Cryptography;
using System.Text;
using ImageMagick;
using ImageMagick.Configuration;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Output;
using Xunit;

namespace WheelConvert.Converters.Image.Tests;

public sealed class ImageConverterTests : IDisposable
{
    private readonly string _testDir;
    private readonly SafeOutputWriter _writer;
    private readonly ImageConverter _converter;

    public ImageConverterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "wc-img-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _writer = new SafeOutputWriter();
        _converter = new ImageConverter();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Best effort clean-up of temp test directory.
        }
    }

    [Fact]
    public void CheckHealth_WhenLibraryLoads_ReturnsReady()
    {
        var health = _converter.CheckHealth();
        Assert.True(health.IsAvailable);
        Assert.Equal(ConverterHealth.Ready, health);
    }

    [Fact]
    public void SecurityPolicy_DeniesMvg_WhenFormatExplicitlySet()
    {
        var path = Path.Combine(_testDir, "test.mvg");
        File.WriteAllText(path, "push graphic-context\nviewbox 0 0 100 100\npop graphic-context");
        var settings = new MagickReadSettings { Format = MagickFormat.Mvg };
        var ex = Assert.Throws<MagickPolicyErrorException>(() => new MagickImage(path, settings));
        Assert.Contains("security policy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecurityPolicy_DeniesMsl_WhenFormatExplicitlySet()
    {
        var path = Path.Combine(_testDir, "test.msl");
        File.WriteAllText(path, "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<image></image>");
        var settings = new MagickReadSettings { Format = MagickFormat.Msl };
        var ex = Assert.Throws<MagickPolicyErrorException>(() => new MagickImage(path, settings));
        Assert.Contains("security policy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecurityPolicy_DeniesSvg_WhenFormatExplicitlySet()
    {
        var path = Path.Combine(_testDir, "test.svg");
        File.WriteAllText(path, "<svg width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"red\"/></svg>");
        var settings = new MagickReadSettings { Format = MagickFormat.Svg };
        var ex = Assert.Throws<MagickPolicyErrorException>(() => new MagickImage(path, settings));
        Assert.Contains("security policy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecurityPolicy_DeniesText_WhenFormatExplicitlySet()
    {
        var path = Path.Combine(_testDir, "test.txt");
        File.WriteAllText(path, "some secret text payload");
        var settings = new MagickReadSettings { Format = MagickFormat.Text };
        var ex = Assert.Throws<MagickPolicyErrorException>(() => new MagickImage(path, settings));
        Assert.Contains("security policy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecurityPolicy_DeniesUrl_WhenFormatExplicitlySet()
    {
        var settings = new MagickReadSettings { Format = MagickFormat.Http };
        var ex = Assert.Throws<MagickPolicyErrorException>(() => new MagickImage("http://127.0.0.1/test.png", settings));
        Assert.Contains("security policy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecurityPolicy_DeniesEphemeral_InPolicyConfiguration()
    {
        // Ephemeral pseudo-protocol is disabled by policy
        Assert.Contains(@"pattern=""EPHEMERAL""", ConfigurationFiles.Default.Policy.Data);
        var path = Path.Combine(_testDir, "test_eph.mvg");
        File.WriteAllText(path, "push graphic-context\nviewbox 0 0 10 10\npop graphic-context");
        var ex = Assert.Throws<MagickPolicyErrorException>(() => new MagickImage("ephemeral:" + path));
        Assert.Contains("security policy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecurityPolicy_DeniesPs_WhenFormatExplicitlySet()
    {
        var path = Path.Combine(_testDir, "test.ps");
        File.WriteAllText(path, "%!PS-Adobe-3.0\n%%BoundingBox: 0 0 10 10\nshowpage");
        var settings = new MagickReadSettings { Format = MagickFormat.Ps };
        var ex = Assert.Throws<MagickPolicyErrorException>(() => new MagickImage(path, settings));
        Assert.Contains("security policy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecurityPolicy_DeniesEps_WhenFormatExplicitlySet()
    {
        var path = Path.Combine(_testDir, "test.eps");
        File.WriteAllText(path, "%!PS-Adobe-3.0 EPSF-3.0\n%%BoundingBox: 0 0 10 10\nshowpage");
        var settings = new MagickReadSettings { Format = MagickFormat.Eps };
        var ex = Assert.Throws<MagickPolicyErrorException>(() => new MagickImage(path, settings));
        Assert.Contains("security policy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecurityPolicy_DeniesPdf_WhenFormatExplicitlySet()
    {
        var path = Path.Combine(_testDir, "test.pdf");
        File.WriteAllText(path, "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\nxref\n0 1\n0000000000 65535 f \ntrailer<</Size 1/Root 1 0 R>>\nstartxref\n50\n%%EOF");
        var settings = new MagickReadSettings { Format = MagickFormat.Pdf };
        var ex = Assert.Throws<MagickPolicyErrorException>(() => new MagickImage(path, settings));
        Assert.Contains("security policy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecurityPolicy_DeniesXps_WhenFormatExplicitlySet()
    {
        var path = Path.Combine(_testDir, "test.xps");
        File.WriteAllText(path, "<FixedDocumentSequence xmlns=\"http://schemas.microsoft.com/xps/2005/06\" />");
        var settings = new MagickReadSettings { Format = MagickFormat.Xps };
        var ex = Assert.Throws<MagickPolicyErrorException>(() => new MagickImage(path, settings));
        Assert.Contains("security policy", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecurityPolicy_DeniesPathAtPrefix()
    {
        var cur = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            var path = "test_at.png";
            using (var img = new MagickImage(MagickColors.Red, 10, 10))
            {
                img.Write(path, MagickFormat.Png);
            }

            var ex = Assert.Throws<MagickPolicyErrorException>(() => new MagickImage("@" + path));
            Assert.Contains("security policy", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.SetCurrentDirectory(cur);
        }
    }

    [Fact]
    public void SecurityPolicy_DeniesPathPipePrefix()
    {
        var cur = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_testDir);
            var path = "test_pipe.png";
            using (var img = new MagickImage(MagickColors.Red, 10, 10))
            {
                img.Write(path, MagickFormat.Png);
            }

            var ex = Assert.Throws<MagickPolicyErrorException>(() => new MagickImage("|type " + path));
            Assert.Contains("security policy", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.SetCurrentDirectory(cur);
        }
    }



















































    [Theory]
    [InlineData(".jpg", "image.jpg", true)]
    [InlineData(".jpg", "image.png", true)]
    [InlineData(".jpg", "image.webp", true)]
    [InlineData(".jpg", "image.ico", true)]
    [InlineData(".png", "image.jpg", true)]
    [InlineData(".webp", "image.png", true)]
    [InlineData(".heic", "image.jpg", true)]
    [InlineData(".gif", "image.webp", true)]
    [InlineData(".gif", "image.jpg", true)]
    [InlineData(".gif", "image.png", true)]
    [InlineData(".gif", "image.ico", false)] // Requirement 3: refuse .gif -> image.ico
    [InlineData(".ico", "image.ico", false)] // Requirement 3: refuse .ico -> image.ico
    [InlineData(".ico", "image.png", true)]
    [InlineData(".bmp", "image.jpg", true)]
    [InlineData(".tiff", "image.png", true)]
    [InlineData(".avif", "image.jpg", true)]
    [InlineData(".txt", "image.jpg", false)]
    [InlineData(".png", "media.mp4", false)]
    public void CanConvert_ValidatesInputAndTargetCombinations(string sourceExt, string targetId, bool expected)
    {
        var actual = _converter.CanConvert(sourceExt, targetId);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ConvertAsync_PngWithAlphaToJpg_FlattensWhiteAndKeepsSourceUntouched()
    {
        // Acceptance #1: PNG with alpha -> JPG: Output exists, white background, source byte-identical
        var sourcePath = Path.Combine(_testDir, "alpha_circle.png");
        using (var sourceImg = new MagickImage(MagickColors.Transparent, 40, 40))
        {
            using var circle = new MagickImage(MagickColors.Red, 20, 20);
            sourceImg.Composite(circle, 10, 10, CompositeOperator.Over);
            await sourceImg.WriteAsync(sourcePath, MagickFormat.Png);
        }

        var sourceBytesBefore = await File.ReadAllBytesAsync(sourcePath);
        var sourceHashBefore = SHA256.HashData(sourceBytesBefore);

        var request = ConversionRequest.Create(sourcePath, TargetCatalog.ImageJpg);
        var result = await _converter.ConvertAsync(request, _writer, null, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Single(result.OutputPaths);

        var outputPath = result.OutputPaths[0];
        Assert.True(File.Exists(outputPath));
        Assert.EndsWith(".jpg", outputPath, StringComparison.OrdinalIgnoreCase);

        // Verify source is byte-identical
        var sourceBytesAfter = await File.ReadAllBytesAsync(sourcePath);
        var sourceHashAfter = SHA256.HashData(sourceBytesAfter);
        Assert.Equal(sourceHashBefore, sourceHashAfter);

        // Verify white background where source was transparent (e.g. at pixel 0,0)
        using var outImg = new MagickImage(outputPath, MagickFormat.Jpeg);
        using var pixels = outImg.GetPixels();
        var corner = pixels.GetPixel(0, 0)!.ToColor()!;
        Assert.Equal(Quantum.Max, corner.R);
        Assert.Equal(Quantum.Max, corner.G);
        Assert.Equal(Quantum.Max, corner.B);
    }

    [Fact]
    public async Task ConvertAsync_JpgToPngWebpIco_ProducesValidFilesAndIcoContains6Sizes()
    {
        // Acceptance #2: JPG -> PNG/WebP/ICO: Valid files (re-open with Magick), ICO contains 6 sizes
        var sourcePath = Path.Combine(_testDir, "photo.jpg");
        using (var img = new MagickImage(MagickColors.RoyalBlue, 300, 150))
        {
            await img.WriteAsync(sourcePath, MagickFormat.Jpeg);
        }

        // 1. JPG -> PNG
        var pngRequest = ConversionRequest.Create(sourcePath, TargetCatalog.ImagePng);
        var pngResult = await _converter.ConvertAsync(pngRequest, _writer, null, CancellationToken.None);
        Assert.True(pngResult.Success, pngResult.Message);
        using (var pngImg = new MagickImage(pngResult.OutputPaths[0], MagickFormat.Png))
        {
            Assert.Equal(300u, pngImg.Width);
            Assert.Equal(150u, pngImg.Height);
        }

        // 2. JPG -> WebP
        var webpRequest = ConversionRequest.Create(sourcePath, TargetCatalog.ImageWebp);
        var webpResult = await _converter.ConvertAsync(webpRequest, _writer, null, CancellationToken.None);
        Assert.True(webpResult.Success, webpResult.Message);
        using (var webpImg = new MagickImage(webpResult.OutputPaths[0], MagickFormat.WebP))
        {
            Assert.Equal(300u, webpImg.Width);
            Assert.Equal(150u, webpImg.Height);
        }

        // 3. JPG -> ICO
        var icoRequest = ConversionRequest.Create(sourcePath, TargetCatalog.ImageIco);
        var icoResult = await _converter.ConvertAsync(icoRequest, _writer, null, CancellationToken.None);
        Assert.True(icoResult.Success, icoResult.Message);

        using (var icoCollection = new MagickImageCollection(icoResult.OutputPaths[0], MagickFormat.Ico))
        {
            Assert.Equal(6, icoCollection.Count);
            var expectedSizes = new uint[] { 256, 128, 64, 48, 32, 16 };
            var actualSizes = icoCollection.Select(f => f.Width).OrderByDescending(s => s).ToArray();
            Assert.Equal(expectedSizes, actualSizes);
            // All ICO layers must be square
            foreach (var frame in icoCollection)
            {
                Assert.Equal(frame.Width, frame.Height);
            }
        }
    }

    [Fact]
    public async Task ConvertAsync_ExifRotatedJpgToPng_ProducesUprightImage()
    {
        // Acceptance #3: EXIF-rotated JPG -> PNG: Output upright
        var sourcePath = Path.Combine(_testDir, "exif_rotated.jpg");
        using (var img = new MagickImage(MagickColors.ForestGreen, 120, 60))
        {
            img.Orientation = Orientation.RightTop;
            img.SetAttribute("exif:Orientation", "6");
            var profile = img.GetExifProfile() ?? new ExifProfile();
            profile.SetValue(ExifTag.Orientation, (ushort)6);
            img.SetProfile(profile);
            await img.WriteAsync(sourcePath, MagickFormat.Jpeg);
        }

        var request = ConversionRequest.Create(sourcePath, TargetCatalog.ImagePng);
        var result = await _converter.ConvertAsync(request, _writer, null, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        var outputPath = result.OutputPaths[0];

        using var outImg = new MagickImage(outputPath, MagickFormat.Png);
        // After AutoOrient, dimensions 120x60 rotate upright to 60x120
        Assert.Equal(60u, outImg.Width);
        Assert.Equal(120u, outImg.Height);
        Assert.True(outImg.Orientation is Orientation.TopLeft or Orientation.Undefined);
    }

    [Fact]
    public async Task ConvertAsync_TruncatedCorruptJpg_FailsCorruptInputAndLeavesNoTempFiles()
    {
        // Acceptance #4: Truncated/corrupt JPG -> Fail(CorruptInput), no .~wc- file left
        var sourcePath = Path.Combine(_testDir, "damaged.jpg");
        // Write incomplete JPEG header followed by garbage
        await File.WriteAllBytesAsync(sourcePath, [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x00, 0x99]);

        var request = ConversionRequest.Create(sourcePath, TargetCatalog.ImagePng);
        var result = await _converter.ConvertAsync(request, _writer, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.CorruptInput, result.Error);

        // Verify no temporary files left in test directory
        var tempFiles = Directory.GetFiles(_testDir, SafeOutputWriter.TempPrefix + "*");
        Assert.Empty(tempFiles);
    }

    [Fact]
    public async Task ConvertAsync_OutputNameAlreadyExists_IncrementsLabelWithoutOverwriting()
    {
        // Acceptance #5: Output name already exists -> photo (converted 2).jpg, original untouched
        var sourcePath = Path.Combine(_testDir, "picture.png");
        using (var img = new MagickImage(MagickColors.Purple, 50, 50))
        {
            await img.WriteAsync(sourcePath, MagickFormat.Png);
        }

        var request = ConversionRequest.Create(sourcePath, TargetCatalog.ImageJpg);

        // First run creates picture (converted).jpg
        var result1 = await _converter.ConvertAsync(request, _writer, null, CancellationToken.None);
        Assert.True(result1.Success);
        var path1 = result1.OutputPaths[0];
        Assert.EndsWith("picture (converted).jpg", path1, StringComparison.OrdinalIgnoreCase);

        var bytes1 = await File.ReadAllBytesAsync(path1);

        // Second run creates picture (converted 2).jpg
        var result2 = await _converter.ConvertAsync(request, _writer, null, CancellationToken.None);
        Assert.True(result2.Success);
        var path2 = result2.OutputPaths[0];
        Assert.EndsWith("picture (converted 2).jpg", path2, StringComparison.OrdinalIgnoreCase);

        // Verify original file is unchanged and exists
        Assert.True(File.Exists(path1));
        var bytes1After = await File.ReadAllBytesAsync(path1);
        Assert.Equal(bytes1, bytes1After);
    }

    [Fact]
    public async Task ConvertAsync_CancellationBeforeStart_ThrowsAndLeavesNoTempFiles()
    {
        // Acceptance #6: Cancellation token cancelled before start -> OperationCanceledException, no temp file left
        var sourcePath = Path.Combine(_testDir, "cancel_test.png");
        using (var img = new MagickImage(MagickColors.Red, 50, 50))
        {
            await img.WriteAsync(sourcePath, MagickFormat.Png);
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = ConversionRequest.Create(sourcePath, TargetCatalog.ImageJpg);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _converter.ConvertAsync(request, _writer, null, cts.Token));

        var tempFiles = Directory.GetFiles(_testDir, SafeOutputWriter.TempPrefix + "*");
        Assert.Empty(tempFiles);
    }

    [Fact]
    public async Task ConvertAsync_EvilMvgPayloadInPng_FailsCorruptInputAndSentinelNeverReadOrExfiltrated()
    {
        // Acceptance #7a: MVG payload saved as evil.png -> Fail(CorruptInput); no new files; file.txt not read
        var sentinelPath = Path.Combine(_testDir, "sentinel.txt");
        var sentinelContent = "SECRET_USER_DATA_" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(sentinelPath, sentinelContent);

        var exfiltratedPath = Path.Combine(_testDir, "exfiltrated.txt");

        // The MVG payload references the sentinel file by ABSOLUTE path and attempts to copy/render it
        var normalizedSentinelPath = sentinelPath.Replace('\\', '/');
        var normalizedExfilPath = exfiltratedPath.Replace('\\', '/');
        var evilPngPath = Path.Combine(_testDir, "evil.png");
        var mvgPayload = $"""
            push graphic-context
            viewbox 0 0 640 480
            image over 0,0 0,0 '{normalizedSentinelPath}'
            image over 0,0 0,0 '|copy "{normalizedSentinelPath}" "{normalizedExfilPath}"'
            pop graphic-context
            """;
        await File.WriteAllTextAsync(evilPngPath, mvgPayload, Encoding.UTF8);

        var filesBefore = Directory.GetFiles(_testDir).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var request = ConversionRequest.Create(evilPngPath, TargetCatalog.ImageJpg);
        var result = await _converter.ConvertAsync(request, _writer, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.CorruptInput, result.Error);

        // Sentinel was never read into exfiltrated output, and sentinel is untouched
        Assert.False(File.Exists(exfiltratedPath));
        var sentinelAfter = await File.ReadAllTextAsync(sentinelPath);
        Assert.Equal(sentinelContent, sentinelAfter);

        // No new output or temporary files in the directory
        var filesAfter = Directory.GetFiles(_testDir).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(filesBefore, filesAfter);
    }

    [Fact]
    public async Task ConvertAsync_CancellationMidConversion_ReachesDiscardPathAndLeavesNoTempFiles()
    {
        var sourcePath = Path.Combine(_testDir, "cancel_mid_test.png");
        using (var img = new MagickImage(MagickColors.DodgerBlue, 50, 50))
        {
            await img.WriteAsync(sourcePath, MagickFormat.Png);
        }

        using var cts = new CancellationTokenSource();
        // Cancel as soon as CreateTempPath has been called, ensuring ConvertAsync has issued a temp path
        // and must execute the finally discard logic.
        var trackingWriter = new TrackingOutputWriter(_writer, onTempCreated: () => cts.Cancel());

        var request = ConversionRequest.Create(sourcePath, TargetCatalog.ImageJpg);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _converter.ConvertAsync(request, trackingWriter, null, cts.Token));

        Assert.True(trackingWriter.DiscardCalled, "Discard must be called when cancelled after temp creation.");
        Assert.False(trackingWriter.CommitCalled, "Commit must not be called when cancelled.");

        var tempFiles = Directory.GetFiles(_testDir, SafeOutputWriter.TempPrefix + "*");
        Assert.Empty(tempFiles);
    }

    [Fact]
    public async Task ConvertAsync_WhenOutputPathUnwritable_ReturnsOutputFailedNotCorruptInput()
    {
        var sourcePath = Path.Combine(_testDir, "write_fail_test.png");
        using (var img = new MagickImage(MagickColors.Green, 20, 20))
        {
            await img.WriteAsync(sourcePath, MagickFormat.Png);
        }

        // Return a temp path inside a non-existent sub-folder to trigger write error
        var unwritableWriter = new FakeUnwritableOutputWriter(_testDir);
        var request = ConversionRequest.Create(sourcePath, TargetCatalog.ImageJpg);
        var result = await _converter.ConvertAsync(request, unwritableWriter, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.OutputFailed, result.Error);
        Assert.Equal("Couldn't save the converted file here.", result.Message);
        Assert.True(unwritableWriter.DiscardCalled, "Discard must be called when write fails.");
    }

    [Fact]
    public async Task ConvertAsync_TargetWithoutOutputExtension_ReturnsUnsupported()
    {
        var sourcePath = Path.Combine(_testDir, "no_ext_test.png");
        using (var img = new MagickImage(MagickColors.Red, 10, 10))
        {
            await img.WriteAsync(sourcePath, MagickFormat.Png);
        }

        var noExtTarget = new ConversionTarget("image.noext", "NoExt", FileKind.Image, null, 1);
        var request = ConversionRequest.Create(sourcePath, noExtTarget);
        var result = await _converter.ConvertAsync(request, _writer, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.Unsupported, result.Error);
    }


    [Fact]
    public async Task ConvertAsync_ImageExceeding30000Px_ReturnsUnsupportedWithImageTooLargeMessage()
    {
        var sourcePath = Path.Combine(_testDir, "huge_30001.bmp");
        var origWidth = ResourceLimits.Width;
        try
        {
            // Temporarily raise limit to create the test file
            ResourceLimits.Width = 40000;
            using (var img = new MagickImage(MagickColors.Red, 30001, 1))
            {
                await img.WriteAsync(sourcePath, MagickFormat.Bmp);
            }
        }
        finally
        {
            ResourceLimits.Width = origWidth;
        }

        var request = ConversionRequest.Create(sourcePath, TargetCatalog.ImageJpg);
        var result = await _converter.ConvertAsync(request, _writer, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.Unsupported, result.Error);
        Assert.Equal("Image is too large.", result.Message);
    }


    [Fact]
    public async Task ConvertAsync_AnimatedGifToWebp_PreservesAllFrames()
    {
        // Multi-frame requirement: keep all frames only for animated GIF -> WebP (animated WebP)
        var sourcePath = Path.Combine(_testDir, "animated.gif");
        using (var collection = new MagickImageCollection())
        {
            var frame1 = new MagickImage(MagickColors.Red, 30, 30);
            frame1.AnimationDelay = 10;
            var frame2 = new MagickImage(MagickColors.Blue, 30, 30);
            frame2.AnimationDelay = 10;
            collection.Add(frame1);
            collection.Add(frame2);
            await collection.WriteAsync(sourcePath, MagickFormat.Gif);
        }

        var request = ConversionRequest.Create(sourcePath, TargetCatalog.ImageWebp);
        var result = await _converter.ConvertAsync(request, _writer, null, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        var outputPath = result.OutputPaths[0];

        using var outCollection = new MagickImageCollection(outputPath, MagickFormat.WebP);
        Assert.Equal(2, outCollection.Count);
    }

    [Fact]
    public async Task ConvertAsync_AnimatedGifToJpg_TakesFirstFrameOnly()
    {
        // Multi-frame requirement: GIF -> JPG uses only first frame
        var sourcePath = Path.Combine(_testDir, "animated_to_jpg.gif");
        using (var collection = new MagickImageCollection())
        {
            var frame1 = new MagickImage(MagickColors.Red, 40, 40);
            var frame2 = new MagickImage(MagickColors.Green, 40, 40);
            collection.Add(frame1);
            collection.Add(frame2);
            await collection.WriteAsync(sourcePath, MagickFormat.Gif);
        }

        var request = ConversionRequest.Create(sourcePath, TargetCatalog.ImageJpg);
        var result = await _converter.ConvertAsync(request, _writer, null, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        var outputPath = result.OutputPaths[0];

        using var outImg = new MagickImage(outputPath, MagickFormat.Jpeg);
        Assert.Equal(40u, outImg.Width);
        Assert.Equal(40u, outImg.Height);
    }

    [Fact]
    public async Task ConvertAsync_MissingSourceFile_ReturnsSourceMissing()
    {
        var missingPath = Path.Combine(_testDir, "missing.png");
        var request = ConversionRequest.Create(missingPath, TargetCatalog.ImageJpg);
        var result = await _converter.ConvertAsync(request, _writer, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.SourceMissing, result.Error);
    }

    [Fact]
    public async Task ConvertAsync_UnsupportedSourceFormat_ReturnsUnsupported()
    {
        var textPath = Path.Combine(_testDir, "notes.txt");
        await File.WriteAllTextAsync(textPath, "hello world");

        var request = ConversionRequest.Create(textPath, TargetCatalog.ImageJpg);
        var result = await _converter.ConvertAsync(request, _writer, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.Unsupported, result.Error);
    }

    [Fact]
    public async Task ConvertAsync_CustomQualityOption_AppliesToJpg()
    {
        var sourcePath = Path.Combine(_testDir, "quality_test.png");
        using (var img = new MagickImage(MagickColors.Coral, 60, 60))
        {
            await img.WriteAsync(sourcePath, MagickFormat.Png);
        }

        var options = new Dictionary<string, string> { [OptionKeys.Quality] = "42" };
        var request = new ConversionRequest(sourcePath, TargetCatalog.ImageJpg, options);
        var result = await _converter.ConvertAsync(request, _writer, null, CancellationToken.None);

        Assert.True(result.Success);
        using var outImg = new MagickImage(result.OutputPaths[0], MagickFormat.Jpeg);
        Assert.Equal(42u, outImg.Quality);
    }

    [Fact]
    public async Task ConvertAsync_ResourceLimitExceeded_ReturnsUnsupportedWithImageTooLargeMessage()
    {
        var sourcePath = Path.Combine(_testDir, "large_dim.png");
        using (var img = new MagickImage(MagickColors.Yellow, 100, 100))
        {
            await img.WriteAsync(sourcePath, MagickFormat.Png);
        }

        var origArea = ResourceLimits.Area;
        var origDisk = ResourceLimits.Disk;
        try
        {
            ResourceLimits.Area = 1;
            ResourceLimits.Disk = 1;

            var request = ConversionRequest.Create(sourcePath, TargetCatalog.ImageJpg);
            var result = await _converter.ConvertAsync(request, _writer, null, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ConversionError.Unsupported, result.Error);
            Assert.Equal("Image is too large.", result.Message);
        }
        finally
        {
            ResourceLimits.Area = origArea;
            ResourceLimits.Disk = origDisk;
        }
    }

    private sealed class TrackingOutputWriter(IOutputWriter inner, Action onTempCreated) : IOutputWriter
    {
        public bool DiscardCalled { get; private set; }
        public bool CommitCalled { get; private set; }

        public string CreateTempPath(string sourcePath, string outputExtension)
        {
            var path = inner.CreateTempPath(sourcePath, outputExtension);
            onTempCreated();
            return path;
        }

        public string Commit(string tempPath, string sourcePath, string outputExtension, string label)
        {
            CommitCalled = true;
            return inner.Commit(tempPath, sourcePath, outputExtension, label);
        }

        public bool Discard(string tempPath)
        {
            DiscardCalled = true;
            return inner.Discard(tempPath);
        }
    }

    private sealed class FakeUnwritableOutputWriter(string baseDir) : IOutputWriter
    {
        public bool DiscardCalled { get; private set; }

        public string CreateTempPath(string sourcePath, string outputExtension)
        {
            return Path.Combine(baseDir, "non_existent_folder", ".~wc-test-unwritable" + outputExtension);
        }

        public string Commit(string tempPath, string sourcePath, string outputExtension, string label)
        {
            throw new InvalidOperationException("Commit should not be reached when write fails.");
        }

        public bool Discard(string tempPath)
        {
            DiscardCalled = true;
            return true;
        }
    }
}
