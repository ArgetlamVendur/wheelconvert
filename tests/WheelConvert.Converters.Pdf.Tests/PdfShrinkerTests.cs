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

        public void Error(string message, Exception? exception = null) => Errors.Add(message);
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
