using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Output;
using Xunit;

namespace WheelConvert.Converters.Pdf.Tests;

/// <summary>Acceptance tests 1-7 from docs/modules/WP-20-pdf-smaller.md.</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
[System.Runtime.Versioning.SupportedOSPlatform("macos")]
public sealed class PdfShrinkerTests : IDisposable
{
    private readonly TestFolder _folder = new();
    private readonly RecordingLog _log = new();

    public void Dispose() => _folder.Dispose();

    // Acceptance 1
    [Fact]
    public async Task Shrink_ThreeBigJpegPages_AtLeastHalfSmaller_RendersAndKeepsPageCount()
    {
        var jpeg = ImagePdf.MakeJpeg(4000, 3000);
        var source = Path.Combine(_folder.Path, "scan.pdf");
        using (var document = new PdfDocument())
        {
            for (var i = 0; i < 3; i++)
            {
                ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, jpeg, 4000, 3000));
            }

            document.Save(source);
        }

        var hash = TestFolder.Hash(source);

        var result = await Run(source);

        Assert.True(result.Success, result.Message);
        var output = Assert.Single(result.OutputPaths);
        Assert.Equal("scan (smaller).pdf", Path.GetFileName(output));
        Assert.True(new FileInfo(output).Length <= new FileInfo(source).Length / 2, "expected at least 50 % smaller");
        using (var shrunk = TestFolder.Read(output))
        {
            Assert.Equal(3, shrunk.PageCount);
            foreach (var image in Images(shrunk))
            {
                Assert.Equal(2000, image.Elements.GetInteger("/Width"));
                Assert.Equal(1500, image.Elements.GetInteger("/Height"));
                Assert.Equal(image.Stream.Length, image.Elements.GetInteger("/Length"));
            }
        }

        await AssertRenders(output, 3);
        Assert.Equal(hash, TestFolder.Hash(source));
        Assert.Empty(_folder.TempFiles());
    }

    [Fact]
    public async Task Shrink_GrayJpegAndSmallJpeg_StayGrayAndKeepTheirSize()
    {
        var gray = ImagePdf.MakeJpeg(2400, 1600, gray: true);
        var small = ImagePdf.MakeJpeg(800, 600);
        var source = Path.Combine(_folder.Path, "mixed.pdf");
        using (var document = new PdfDocument())
        {
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, gray, 2400, 1600, "/DeviceGray"));
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, small, 800, 600));
            document.Save(source);
        }

        var result = await Run(source);

        Assert.True(result.Success, result.Message);
        using var shrunk = TestFolder.Read(result.OutputPaths[0]);
        var images = Images(shrunk).OrderByDescending(i => i.Elements.GetInteger("/Width")).ToList();
        Assert.Equal(2, images.Count);
        Assert.Equal(2000, images[0].Elements.GetInteger("/Width")); // downscaled, never enlarged
        Assert.Equal("/DeviceGray", images[0].Elements.GetName("/ColorSpace"));
        Assert.Equal(1, JpegInfo.TryRead(images[0].Stream.Value)!.Value.Components);
        Assert.Equal(800, images[1].Elements.GetInteger("/Width")); // under 2000 px: re-encoded only
        await AssertRenders(result.OutputPaths[0], 2);
    }

    // Acceptance 2
    [Fact]
    public async Task Shrink_TextOnlyPdf_NoResultWithGenericMessage_NoTempLeft()
    {
        var source = _folder.CreatePdf(3, "text.pdf");
        var hash = TestFolder.Hash(source);

        var result = await Run(source);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.NoResult, result.Error);
        Assert.Equal("Couldn't make this PDF smaller.", result.Message);
        AssertUntouched(source, hash);
    }

    // Acceptance 3
    [Fact]
    public async Task Shrink_OnlyImageIsCmyk_NoResultWithFormatMessage_NoTempLeft()
    {
        var source = Path.Combine(_folder.Path, "cmyk.pdf");
        using (var document = new PdfDocument())
        {
            // The dictionary says DeviceCMYK; the app must skip it on that alone, whatever the bytes are.
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, ImagePdf.MakeJpeg(3000, 2000), 3000, 2000, "/DeviceCMYK"));
            document.Save(source);
        }

        var hash = TestFolder.Hash(source);

        var result = await Run(source);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.NoResult, result.Error);
        Assert.Equal("Couldn't make this PDF smaller: its images use a format this app can't shrink yet.", result.Message);
        AssertUntouched(source, hash);
    }

    [Fact]
    public async Task Shrink_ImageWithSoftMask_IsSkipped()
    {
        var source = Path.Combine(_folder.Path, "mask.pdf");
        using (var document = new PdfDocument())
        {
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, ImagePdf.MakeJpeg(3000, 2000), 3000, 2000, addSMask: true));
            document.Save(source);
        }

        var result = await Run(source);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.NoResult, result.Error);
        Assert.Contains("format", result.Message);
        Assert.Empty(_folder.TempFiles());
    }

    // Acceptance 4
    [Fact]
    public async Task Shrink_OneImageSharedByTenPages_ReplacedOnce_AllPagesRender()
    {
        var source = Path.Combine(_folder.Path, "shared.pdf");
        using (var document = new PdfDocument())
        {
            var image = ImagePdf.AddImage(document, ImagePdf.MakeJpeg(3000, 2000), 3000, 2000);
            for (var i = 0; i < 10; i++)
            {
                ImagePdf.AddPageWith(document, image);
            }

            document.Save(source);
        }

        var shrinker = new PdfShrinker(_log);
        var result = await shrinker.ShrinkAsync(Request(source), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, shrinker.LastStats!.Replaced);
        Assert.Equal(0, shrinker.LastStats.Skipped);
        using (var shrunk = TestFolder.Read(result.OutputPaths[0]))
        {
            Assert.Single(Images(shrunk));
            Assert.Equal(10, shrunk.PageCount);
        }

        await AssertRenders(result.OutputPaths[0], 10);
        Assert.Empty(_folder.TempFiles());
    }

    // Acceptance 5 (the success path is covered by the tests above: hash unchanged, no temps)
    [Fact]
    public async Task Shrink_CommitFails_NoTempLeft_SourceUnchanged()
    {
        var source = BigScan("commitfail.pdf");
        var hash = TestFolder.Hash(source);

        var result = await new PdfShrinker(_log).ShrinkAsync(Request(source), new FailingCommitWriter(1), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.OutputFailed, result.Error);
        AssertUntouched(source, hash);
    }

    [Fact]
    public async Task Shrink_UsesTwoWriterTemps_AndDiscardsBoth()
    {
        var source = _folder.CreatePdf(1, "text.pdf");
        var writer = new CountingWriter();

        await new PdfShrinker(_log).ShrinkAsync(Request(source), writer, null, CancellationToken.None);

        Assert.Equal(2, writer.Created.Count);
        Assert.Equal(writer.Created.Order(), writer.Discarded.Order());
        Assert.Empty(_folder.TempFiles());
    }

    // Acceptance 6
    [Fact]
    public async Task Shrink_KeepsLinkAndBookmark()
    {
        var source = Path.Combine(_folder.Path, "linked.pdf");
        using (var document = new PdfDocument())
        {
            var page = ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, ImagePdf.MakeJpeg(3000, 2000), 3000, 2000));
            page.AddWebLink(new PdfRectangle(new PdfSharp.Drawing.XRect(10, 10, 100, 20)), "https://example.com/");
            document.Outlines.Add("Chapter one", page, true);
            document.Save(source);
        }

        var result = await Run(source);

        Assert.True(result.Success, result.Message);
        using var shrunk = TestFolder.Read(result.OutputPaths[0]);
        Assert.Single(shrunk.Pages[0].Annotations);
        Assert.Single(shrunk.Outlines);
        Assert.Equal("Chapter one", shrunk.Outlines[0].Title);
    }

    // Acceptance 7
    [Fact]
    public async Task Shrink_Encrypted_PasswordProtected_NoTempLeft()
    {
        var source = _folder.CreatePdf(2, "locked.pdf", userPassword: "secret", ownerPassword: "owner");
        var hash = TestFolder.Hash(source);

        var result = await Run(source);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.PasswordProtected, result.Error);
        AssertUntouched(source, hash);
    }

    [Fact]
    public async Task Shrink_OwnerPasswordOnly_PasswordProtected_NeverSavedWithoutRestrictions()
    {
        var source = Path.Combine(_folder.Path, "owner.pdf");
        using (var document = new PdfDocument())
        {
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, ImagePdf.MakeJpeg(3000, 2000), 3000, 2000));
            document.SecuritySettings.OwnerPassword = "owner";
            document.SecuritySettings.PermitPrint = false;
            document.Save(source);
        }

        var hash = TestFolder.Hash(source);

        var result = await Run(source);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.PasswordProtected, result.Error);
        AssertUntouched(source, hash);
    }

    [Fact]
    public async Task Shrink_Garbage_CorruptInput_NoTempLeft()
    {
        var source = _folder.CreateFile("junk.pdf", [.. Enumerable.Range(0, 4096).Select(i => (byte)(i * 31 % 251))]);
        var hash = TestFolder.Hash(source);

        var result = await Run(source);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.CorruptInput, result.Error);
        AssertUntouched(source, hash);
    }

    [Fact]
    public async Task Shrink_CancelBetweenImages_Throws_NoTempLeft_SourceUnchanged()
    {
        var jpeg = ImagePdf.MakeJpeg(2500, 2000);
        var source = Path.Combine(_folder.Path, "cancel.pdf");
        using (var document = new PdfDocument())
        {
            for (var i = 0; i < 3; i++)
            {
                ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, jpeg, 2500, 2000));
            }

            document.Save(source);
        }

        var hash = TestFolder.Hash(source);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Run(source, new CancelAtProgress(cts, 0.3), cts.Token));

        AssertUntouched(source, hash);
    }

    [Fact]
    public async Task Shrink_SourceMissing_SourceMissing()
    {
        var result = await Run(Path.Combine(_folder.Path, "gone.pdf"));

        Assert.Equal(ConversionError.SourceMissing, result.Error);
    }

    [Fact]
    public async Task Shrink_RenderCheckFails_Discarded_LoggedAndUnknown()
    {
        var source = BigScan("render.pdf");
        var writer = new CountingWriter();
        var shrinker = new PdfShrinker(_log, (_, _) => throw new InvalidDataException("cannot render"));

        var result = await shrinker.ShrinkAsync(Request(source), writer, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.Unknown, result.Error);
        Assert.Equal("Couldn't make this PDF smaller.", result.Message);
        Assert.Single(_log.Errors);
        Assert.Equal(2, writer.Created.Count);
        Assert.Equal(writer.Created.Order(), writer.Discarded.Order());
        Assert.Empty(_folder.TempFiles());
    }

    // Finding 1 (ADR-013): the shared log never gets a folder path or a stack trace.
    [Fact]
    public async Task Shrink_RenderCheckFailsWithPaths_LogHasFileNamesOnly()
    {
        var source = BigScan("render.pdf");
        var shrinker = new PdfShrinker(_log, (tempB, _) => throw new IOException(
            $"The process cannot access the file '{tempB}' or '{tempB.Replace('\\', '/')}' next to {source}."));

        var result = await shrinker.ShrinkAsync(Request(source), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.Equal(ConversionError.Unknown, result.Error);
        var line = Assert.Single(_log.Errors);
        Assert.DoesNotContain(_folder.Path, line, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_folder.Path.Replace('\\', '/'), line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("render.pdf", line);
        Assert.Contains("IOException", line);
        Assert.DoesNotContain("   at ", line);
        Assert.All(_log.Exceptions, Assert.Null);
        Assert.True(line.Length <= 500);
    }

    [Fact]
    public async Task Shrink_LongRenderErrorMessage_LogLineIsCutAt500Characters()
    {
        var source = BigScan("long.pdf");
        var shrinker = new PdfShrinker(_log, (_, _) => throw new InvalidDataException(new string('x', 2000)));

        await shrinker.ShrinkAsync(Request(source), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.Equal(500, Assert.Single(_log.Errors).Length);
    }

    // Round 2: PDFsharp 6.2.4 can't decode every filter; a page with such content is left untouched and the rest still shrinks.
    [Fact]
    public async Task Shrink_PageWithRunLengthContent_IsLeftByteIdentical_AndTheRestShrinks()
    {
        byte[] plain = "q 0.5 g 10 10 50 50 re f Q"u8.ToArray();
        byte[] runLength = [(byte)(plain.Length - 1), .. plain, 128];
        var source = Path.Combine(_folder.Path, "runlength.pdf");
        using (var document = new PdfDocument())
        {
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, ImagePdf.MakeJpeg(3000, 2000), 3000, 2000));
            var page = document.AddPage();
            var content = new PdfDictionary(document);
            content.Elements["/Filter"] = new PdfName("/RunLengthDecode");
            content.CreateStream(runLength);
            document.Internals.AddObject(content);
            page.Elements["/Contents"] = content.Reference;
            document.Save(source);
        }

        var result = await Run(source);

        Assert.True(result.Success, result.Message);
        using var shrunk = TestFolder.Read(result.OutputPaths[0]);
        Assert.Equal(2, shrunk.PageCount);
        var stream = Assert.Single(ContentStreams(shrunk), d => d.Elements.GetName("/Filter") == "/RunLengthDecode");
        Assert.Equal(runLength, stream.Stream.Value);
        await AssertRenders(result.OutputPaths[0], 2);
    }

    // Finding 2: quality 70
    [Fact]
    public async Task Shrink_UnderLimit_ReencodesAtQuality70()
    {
        var original = ImagePdf.MakeJpeg(800, 600, quality: 100);
        var source = Path.Combine(_folder.Path, "q.pdf");
        using (var document = new PdfDocument())
        {
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, original, 800, 600));
            document.Save(source);
        }

        var result = await Run(source);

        Assert.True(result.Success, result.Message);
        using var shrunk = TestFolder.Read(result.OutputPaths[0]);
        Assert.Equal(ReencodeAt70(original), Assert.Single(Images(shrunk)).Stream.Value);
    }

    // Finding 2: replace only if smaller; never enlarge
    [Fact]
    public async Task Shrink_ImageThatWouldGrow_StaysByteIdentical_AndIsNeverEnlarged()
    {
        var lowQuality = ImagePdf.MakeJpeg(800, 600, quality: 20);
        var big = ImagePdf.MakeJpeg(3000, 2000);
        var source = Path.Combine(_folder.Path, "grow.pdf");
        using (var document = new PdfDocument())
        {
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, lowQuality, 800, 600));
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, big, 3000, 2000));
            document.Save(source);
        }

        var shrinker = new PdfShrinker(_log);
        var result = await shrinker.ShrinkAsync(Request(source), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, shrinker.LastStats!.Replaced);
        Assert.Equal(2, shrinker.LastStats.Eligible);
        using var shrunk = TestFolder.Read(result.OutputPaths[0]);
        var small = Images(shrunk).Single(i => i.Elements.GetInteger("/Width") == 800);
        Assert.Equal(lowQuality, small.Stream.Value);
        Assert.Equal(600, small.Elements.GetInteger("/Height"));
    }

    [Fact]
    public async Task Shrink_SmallImageUnderLimit_NeverEnlarged()
    {
        var original = ImagePdf.MakeJpeg(800, 600, quality: 100);
        var source = Path.Combine(_folder.Path, "small.pdf");
        using (var document = new PdfDocument())
        {
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, original, 800, 600));
            document.Save(source);
        }

        var result = await Run(source);

        Assert.True(result.Success, result.Message);
        using var shrunk = TestFolder.Read(result.OutputPaths[0]);
        var image = Assert.Single(Images(shrunk));
        Assert.Equal(800, image.Elements.GetInteger("/Width"));
        Assert.Equal(600, image.Elements.GetInteger("/Height"));
    }

    // Finding 2: content-stream compression (req. 4)
    [Fact]
    public async Task Shrink_ContentStreams_AreFlateCompressed()
    {
        var source = Path.Combine(_folder.Path, "content.pdf");
        using (var document = new PdfDocument())
        {
            document.Options.CompressContentStreams = false;
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, ImagePdf.MakeJpeg(3000, 2000), 3000, 2000));
            document.Save(source);
        }

        using (var before = TestFolder.Read(source))
        {
            Assert.DoesNotContain(ContentStreams(before), d => d.Elements.GetName("/Filter") == "/FlateDecode");
        }

        var result = await Run(source);

        Assert.True(result.Success, result.Message);
        using var shrunk = TestFolder.Read(result.OutputPaths[0]);
        var streams = ContentStreams(shrunk);
        Assert.NotEmpty(streams);
        Assert.Contains(streams, d => d.Elements.GetName("/Filter") == "/FlateDecode");
    }

    // Finding 2: cancellation between images (req. 6)
    [Fact]
    public async Task Shrink_CancelAfterFirstImage_StopsTheImagePass_BeforeSaveAndRenderCheck()
    {
        var jpeg = ImagePdf.MakeJpeg(2500, 2000);
        var source = Path.Combine(_folder.Path, "cancel2.pdf");
        using (var document = new PdfDocument())
        {
            for (var i = 0; i < 3; i++)
            {
                ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, jpeg, 2500, 2000));
            }

            document.Save(source);
        }

        var renderCalls = 0;
        var shrinker = new PdfShrinker(_log, (_, _) =>
        {
            renderCalls++;
            return Task.CompletedTask;
        });
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            shrinker.ShrinkAsync(Request(source), new SafeOutputWriter(), new CancelAtProgress(cts, 0.3), cts.Token));

        Assert.Null(shrinker.LastStats); // the pass never finished: it stopped between images
        Assert.Equal(0, renderCalls);
        Assert.Empty(_folder.TempFiles());
    }

    // Finding 2: DCTDecode only / 8 bits only. The bytes are real JPEG data, so only the dictionary can tell them apart.
    [Fact]
    public async Task Shrink_NonDctFilter_AndNon8BitImages_AreSkippedUnchanged()
    {
        var jpeg = ImagePdf.MakeJpeg(1200, 900);
        var big = ImagePdf.MakeJpeg(3000, 2000);
        var source = Path.Combine(_folder.Path, "other.pdf");
        using (var document = new PdfDocument())
        {
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, jpeg, 1200, 900, filter: "/FlateDecode"));
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, jpeg, 1200, 900, bits: 16));
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, big, 3000, 2000));
            document.Save(source);
        }

        var shrinker = new PdfShrinker(_log);
        var result = await shrinker.ShrinkAsync(Request(source), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new PdfShrinker.Stats(1, 2, 1), shrinker.LastStats);
        using var shrunk = TestFolder.Read(result.OutputPaths[0]);
        Assert.Equal(2, Images(shrunk).Count(i => i.Stream.Value.AsSpan().SequenceEqual(jpeg)));
    }

    // Finding 6: an image that is another image's mask is left alone, even when it would qualify on its own
    [Fact]
    public async Task Shrink_ImageUsedAsMaskOfAnotherImage_IsSkipped()
    {
        var maskJpeg = ImagePdf.MakeJpeg(3000, 2000, gray: true);
        var source = Path.Combine(_folder.Path, "maskof.pdf");
        using (var document = new PdfDocument())
        {
            var mask = ImagePdf.AddImage(document, maskJpeg, 3000, 2000, "/DeviceGray");
            ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, ImagePdf.MakeJpeg(3000, 2000), 3000, 2000, smaskImage: mask));
            document.Save(source);
        }

        var shrinker = new PdfShrinker(_log);
        var result = await shrinker.ShrinkAsync(Request(source), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(new PdfShrinker.Stats(0, 2, 0), shrinker.LastStats);
        Assert.Empty(_folder.TempFiles());
    }

    // Finding 3
    [Fact]
    public async Task Shrink_SourceVanishedMidRun_SourceMissing_NoTempLeft()
    {
        var missing = Path.Combine(_folder.Path, "gone.pdf");

        var result = await new PdfShrinker(_log).ShrinkAsync(Request(missing), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.Equal(ConversionError.SourceMissing, result.Error);
        Assert.Empty(_folder.TempFiles());
    }

    [Fact]
    public async Task Shrink_SourceLockedByAnotherApp_ReportsInUse_NotSaveError()
    {
        var source = BigScan("locked.pdf");
        using var hold = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None);

        var result = await new PdfShrinker(_log).ShrinkAsync(Request(source), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Couldn't open this PDF. Another app may be using it.", result.Message);
        Assert.Empty(_folder.TempFiles());
    }

    private static byte[] ReencodeAt70(byte[] jpeg)
    {
        using var codec = SkiaSharp.SKCodec.Create(new SkiaSharp.SKMemoryStream(jpeg));
        using var bitmap = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(codec.Info.Width, codec.Info.Height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Opaque));
        codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 70);
        return data.ToArray();
    }

    private static List<PdfDictionary> ContentStreams(PdfDocument document) =>
        document.Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Where(d => d.Stream is not null && d.Elements.GetName("/Subtype") != "/Image" && d.Elements.GetName("/Type") != "/XRef")
            .ToList();

    [Fact]
    public void JpegInfo_ReadsFrameHeader_RejectsNonJpeg()
    {
        var info = JpegInfo.TryRead(ImagePdf.MakeJpeg(64, 48))!.Value;
        Assert.Equal(new JpegInfo(64, 48, 8, 3), info);
        Assert.Null(JpegInfo.TryRead(new byte[] { 1, 2, 3, 4, 5 }));
        Assert.Null(JpegInfo.TryRead(ReadOnlySpan<byte>.Empty));
    }

    private string BigScan(string name)
    {
        var path = Path.Combine(_folder.Path, name);
        using var document = new PdfDocument();
        ImagePdf.AddPageWith(document, ImagePdf.AddImage(document, ImagePdf.MakeJpeg(3000, 2000), 3000, 2000));
        document.Save(path);
        return path;
    }

    private void AssertUntouched(string source, string hash)
    {
        Assert.Equal(hash, TestFolder.Hash(source));
        Assert.Empty(_folder.TempFiles());
        Assert.Single(Directory.GetFiles(_folder.Path));
    }

    private static async Task AssertRenders(string path, int pages)
    {
        Assert.Equal(pages, await Pdfium.GetPageCountAsync(path, CancellationToken.None));
        for (var i = 0; i < pages; i++)
        {
            Assert.NotEmpty(await Pdfium.RenderThumbnailPngAsync(path, i, 32, CancellationToken.None));
        }
    }

    private static List<PdfDictionary> Images(PdfDocument document) =>
        document.Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Where(d => d.Stream is not null && d.Elements.GetName("/Subtype") == "/Image")
            .ToList();

    private Task<ConversionResult> Run(string source, IProgress<double>? progress = null, CancellationToken token = default) =>
        new PdfConverter(_log).ConvertAsync(Request(source), new SafeOutputWriter(), progress, token);

    private static ConversionRequest Request(string source) =>
        new(source, TargetCatalog.PdfSmaller, new Dictionary<string, string>());

    private sealed class RecordingLog : ILog
    {
        public List<string> Errors { get; } = [];

        public void Info(string message)
        {
        }

        public void Warn(string message, Exception? exception = null)
        {
        }

        public List<Exception?> Exceptions { get; } = [];

        public void Error(string message, Exception? exception = null)
        {
            Errors.Add(message);
            Exceptions.Add(exception);
        }
    }

    private sealed class CountingWriter : IOutputWriter
    {
        private readonly SafeOutputWriter _inner = new();

        public List<string> Created { get; } = [];

        public List<string> Discarded { get; } = [];

        public string CreateTempPath(string sourcePath, string outputExtension)
        {
            var path = _inner.CreateTempPath(sourcePath, outputExtension);
            Created.Add(path);
            return path;
        }

        public string Commit(string tempPath, string sourcePath, string outputExtension, string label) =>
            _inner.Commit(tempPath, sourcePath, outputExtension, label);

        public bool Discard(string tempPath)
        {
            Discarded.Add(tempPath);
            return _inner.Discard(tempPath);
        }
    }
}
