using SkiaSharp;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Output;
using Xunit;

namespace WheelConvert.Converters.Pdf.Tests;

/// <summary>Acceptance tests 1–8 and 10 from docs/modules/WP-08-pdf-operations.md, plus S5 and edge cases.</summary>
public sealed class PdfConverterTests : IDisposable
{
    private readonly TestFolder _folder = new();
    private readonly PdfConverter _converter = new();

    public void Dispose() => _folder.Dispose();

    // Acceptance 1
    [Fact]
    public async Task SplitPages_ThreePages_OneFilePerPage_SourceUnchanged()
    {
        var source = _folder.CreatePdf(3);
        var hash = TestFolder.Hash(source);

        var result = await Run(source, TargetCatalog.PdfSplitPages);

        Assert.True(result.Success, result.Message);
        Assert.Equal(
            ["doc (page 1).pdf", "doc (page 2).pdf", "doc (page 3).pdf"],
            result.OutputPaths.Select(Path.GetFileName));
        for (var i = 0; i < 3; i++)
        {
            using var piece = TestFolder.Read(result.OutputPaths[i]);
            Assert.Equal(1, piece.PageCount);
            Assert.Equal(i, TestFolder.IndexOf(piece.Pages[0]));
        }

        Assert.Equal(hash, TestFolder.Hash(source));
        Assert.Empty(_folder.TempFiles());
    }

    // Acceptance 2
    [Fact]
    public async Task SplitPages_FortyPages_NamesAreZeroPadded()
    {
        var source = _folder.CreatePdf(40);

        var result = await Run(source, TargetCatalog.PdfSplitPages);

        Assert.True(result.Success, result.Message);
        Assert.Equal(40, result.OutputPaths.Count);
        Assert.Equal("doc (page 01).pdf", Path.GetFileName(result.OutputPaths[0]));
        Assert.Equal("doc (page 09).pdf", Path.GetFileName(result.OutputPaths[8]));
        Assert.Equal("doc (page 40).pdf", Path.GetFileName(result.OutputPaths[39]));
    }

    // Acceptance 3
    [Fact]
    public async Task SplitHalf_FivePages_ThreePlusTwo()
    {
        var source = _folder.CreatePdf(5);

        var result = await Run(source, TargetCatalog.PdfSplitHalf);

        Assert.True(result.Success, result.Message);
        Assert.Equal(["doc (part 1).pdf", "doc (part 2).pdf"], result.OutputPaths.Select(Path.GetFileName));
        Assert.Equal([0, 1, 2], PageIndices(result.OutputPaths[0]));
        Assert.Equal([3, 4], PageIndices(result.OutputPaths[1]));
    }

    [Fact]
    public async Task SplitHalf_OnePage_Unsupported()
    {
        var source = _folder.CreatePdf(1);

        var result = await Run(source, TargetCatalog.PdfSplitHalf);

        AssertFailed(result, ConversionError.Unsupported, "Only one page.");
    }

    // Acceptance 4
    [Fact]
    public async Task ExtractOdd_FivePages_PagesOneThreeFive()
    {
        var source = _folder.CreatePdf(5);

        var result = await Run(source, TargetCatalog.PdfExtractOdd);

        Assert.True(result.Success, result.Message);
        Assert.Equal("doc (odd pages).pdf", Path.GetFileName(Assert.Single(result.OutputPaths)));
        Assert.Equal([0, 2, 4], PageIndices(result.OutputPaths[0]));
    }

    [Fact]
    public async Task ExtractEven_FivePages_PagesTwoFour()
    {
        var source = _folder.CreatePdf(5);

        var result = await Run(source, TargetCatalog.PdfExtractEven);

        Assert.True(result.Success, result.Message);
        Assert.Equal("doc (even pages).pdf", Path.GetFileName(Assert.Single(result.OutputPaths)));
        Assert.Equal([1, 3], PageIndices(result.OutputPaths[0]));
    }

    [Fact]
    public async Task ExtractEven_OnePage_Unsupported()
    {
        var source = _folder.CreatePdf(1);

        var result = await Run(source, TargetCatalog.PdfExtractEven);

        AssertFailed(result, ConversionError.Unsupported, "No even pages.");
    }

    [Fact]
    public async Task Reverse_FourPages_ReversedOrder()
    {
        var source = _folder.CreatePdf(4);

        var result = await Run(source, TargetCatalog.PdfReverse);

        Assert.True(result.Success, result.Message);
        Assert.Equal("doc (reversed).pdf", Path.GetFileName(Assert.Single(result.OutputPaths)));
        Assert.Equal([3, 2, 1, 0], PageIndices(result.OutputPaths[0]));
    }

    [Theory]
    [InlineData(0, 90)]
    [InlineData(90, 180)]
    [InlineData(270, 0)]
    public async Task Rotate_AddsNinetyDegreesClockwise(int before, int after)
    {
        var source = _folder.CreatePdf(3, rotate: before);
        var hash = TestFolder.Hash(source);

        var result = await Run(source, TargetCatalog.PdfRotate90);

        Assert.True(result.Success, result.Message);
        Assert.Equal("doc (rotated).pdf", Path.GetFileName(Assert.Single(result.OutputPaths)));
        using var rotated = TestFolder.Read(result.OutputPaths[0]);
        Assert.Equal(3, rotated.PageCount);
        Assert.All(rotated.Pages.Cast<PdfSharp.Pdf.PdfPage>(), page => Assert.Equal(after, page.Rotate));
        Assert.Equal([0, 1, 2], PageIndices(result.OutputPaths[0]));
        Assert.Equal(hash, TestFolder.Hash(source));
    }

    // Acceptance 5
    [Fact]
    public async Task Reorder_ValidOrder_PagesInThatOrder()
    {
        var source = _folder.CreatePdf(3);

        var result = await Run(source, TargetCatalog.PdfReorder, "3,1,2");

        Assert.True(result.Success, result.Message);
        Assert.Equal("doc (reordered).pdf", Path.GetFileName(Assert.Single(result.OutputPaths)));
        Assert.Equal([2, 0, 1], PageIndices(result.OutputPaths[0]));
    }

    [Fact]
    public async Task Reorder_Subset_LeavesOtherPagesOut()
    {
        var source = _folder.CreatePdf(3);

        var result = await Run(source, TargetCatalog.PdfReorder, "2");

        Assert.True(result.Success, result.Message);
        Assert.Equal([1], PageIndices(result.OutputPaths[0]));
    }

    [Theory]
    [InlineData("1,1")]
    [InlineData("0")]
    [InlineData("4")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Reorder_InvalidOrder_FailsWithoutOutput(string? order)
    {
        var source = _folder.CreatePdf(3);

        var result = await Run(source, TargetCatalog.PdfReorder, order);

        AssertFailed(result, ConversionError.Unsupported, "Invalid page order.");
        Assert.Single(Directory.GetFiles(_folder.Path));
    }

    // Acceptance 6
    [Theory]
    [InlineData("pdf.split-pages")]
    [InlineData("pdf.rotate-90")]
    [InlineData("pdf.to-images")]
    public async Task Encrypted_PasswordProtected_NoTempLeft(string targetId)
    {
        var source = _folder.CreatePdf(2, userPassword: "secret", ownerPassword: "owner");

        var result = await Run(source, Target(targetId));

        AssertFailed(result, ConversionError.PasswordProtected, "This PDF is password-protected.");
        Assert.Empty(_folder.TempFiles());
        Assert.Single(Directory.GetFiles(_folder.Path));
    }

    [Theory]
    [InlineData("pdf.split-pages")]
    [InlineData("pdf.to-images")]
    public async Task OwnerPasswordOnly_OpensWithoutPassword(string targetId)
    {
        var source = _folder.CreatePdf(2, ownerPassword: "owner");

        var result = await Run(source, Target(targetId));

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, result.OutputPaths.Count);
    }

    // Acceptance 7
    [Theory]
    [InlineData("pdf.split-pages")]
    [InlineData("pdf.reverse")]
    [InlineData("pdf.to-images")]
    public async Task GarbageBytes_CorruptInput(string targetId)
    {
        var source = _folder.CreateFile("junk.pdf", [.. Enumerable.Range(0, 4096).Select(i => (byte)(i * 31 % 251))]);

        var result = await Run(source, Target(targetId));

        Assert.False(result.Success);
        Assert.Equal(ConversionError.CorruptInput, result.Error);
        Assert.Empty(result.OutputPaths);
        Assert.Single(Directory.GetFiles(_folder.Path));
    }

    [Fact]
    public async Task EmptyFile_CorruptInput()
    {
        var source = _folder.CreateFile("empty.pdf", []);

        var result = await Run(source, TargetCatalog.PdfSplitPages);

        Assert.Equal(ConversionError.CorruptInput, result.Error);
    }

    [Fact]
    public async Task MissingSource_SourceMissing()
    {
        var result = await Run(Path.Combine(_folder.Path, "gone.pdf"), TargetCatalog.PdfSplitPages);

        Assert.Equal(ConversionError.SourceMissing, result.Error);
    }

    // Acceptance 8
    [Fact]
    public async Task ToImages_TwoPages_TwoReadablePngsAt150Dpi()
    {
        var source = _folder.CreatePdf(2);
        var hash = TestFolder.Hash(source);

        var result = await Run(source, TargetCatalog.PdfToImages);

        Assert.True(result.Success, result.Message);
        Assert.Equal(["doc (page 1).png", "doc (page 2).png"], result.OutputPaths.Select(Path.GetFileName));
        for (var i = 0; i < 2; i++)
        {
            using var bitmap = SKBitmap.Decode(result.OutputPaths[i]);
            Assert.NotNull(bitmap);
            Assert.InRange(bitmap.Width, Expected150Dpi(TestFolder.WidthOf(i)) - 1, Expected150Dpi(TestFolder.WidthOf(i)) + 1);
            Assert.InRange(bitmap.Height, Expected150Dpi(300) - 1, Expected150Dpi(300) + 1);
        }

        Assert.Equal(hash, TestFolder.Hash(source));
        Assert.Empty(_folder.TempFiles());
    }

    [Fact]
    public async Task ToImages_ReportsProgressPerPage()
    {
        var source = _folder.CreatePdf(4);
        var reports = new List<double>();

        var result = await _converter.ConvertAsync(
            ConversionRequest.Create(source, TargetCatalog.PdfToImages),
            new SafeOutputWriter(),
            new SyncProgress(reports.Add),
            CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal([0.25, 0.5, 0.75, 1.0], reports);
    }

    // Acceptance 10
    [Fact]
    public async Task Cancel_DuringSplitOfFiftyPages_CommittedPiecesStay_NoTemps()
    {
        var source = _folder.CreatePdf(50);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _converter.ConvertAsync(
            ConversionRequest.Create(source, TargetCatalog.PdfSplitPages),
            new SafeOutputWriter(),
            new CancelAtProgress(cts, 3 / 50.0),
            cts.Token));

        var pieces = Directory.GetFiles(_folder.Path, "doc (page *).pdf");
        Assert.Equal(3, pieces.Length);
        Assert.All(pieces, piece => Assert.Single(PageIndices(piece)));
        Assert.Empty(_folder.TempFiles());
    }

    [Fact]
    public async Task Cancel_DuringToImages_CommittedPiecesStay_NoTemps()
    {
        var source = _folder.CreatePdf(10);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _converter.ConvertAsync(
            ConversionRequest.Create(source, TargetCatalog.PdfToImages),
            new SafeOutputWriter(),
            new CancelAtProgress(cts, 2 / 10.0),
            cts.Token));

        Assert.Equal(2, Directory.GetFiles(_folder.Path, "doc (page *).png").Length);
        Assert.Empty(_folder.TempFiles());
    }

    // CONTRACTS C2 S5
    [Fact]
    public async Task CommitFailsMidway_KeepsEarlierPieces_ReportsHowMany()
    {
        var source = _folder.CreatePdf(5);

        var result = await _converter.ConvertAsync(
            ConversionRequest.Create(source, TargetCatalog.PdfSplitPages),
            new FailingCommitWriter(failOnCommit: 3),
            null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.OutputFailed, result.Error);
        Assert.Equal("Stopped after 2 of 5 pages: Couldn't save the file here.", result.Message);
        Assert.Equal(["doc (page 1).pdf", "doc (page 2).pdf"], result.OutputPaths.Select(Path.GetFileName));
        Assert.All(result.OutputPaths, path => Assert.True(File.Exists(path)));
        Assert.Empty(_folder.TempFiles());
    }

    [Fact]
    public async Task CommitFailsOnSingleOutput_OutputFailed_NoTemps()
    {
        var source = _folder.CreatePdf(2);

        var result = await _converter.ConvertAsync(
            ConversionRequest.Create(source, TargetCatalog.PdfReverse),
            new FailingCommitWriter(failOnCommit: 1),
            null,
            CancellationToken.None);

        AssertFailed(result, ConversionError.OutputFailed, "Couldn't save the file here.");
        Assert.Empty(_folder.TempFiles());
    }

    [Fact]
    public async Task SourceOpenInAnotherApp_StillReadable()
    {
        var source = _folder.CreatePdf(2);
        using var otherApp = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        var result = await Run(source, TargetCatalog.PdfSplitPages);
        var images = await Run(source, TargetCatalog.PdfToImages);

        Assert.True(result.Success, result.Message);
        Assert.True(images.Success, images.Message);
    }

    [Fact]
    public async Task ExistingOutput_NeverOverwritten()
    {
        var source = _folder.CreatePdf(2);
        var existing = _folder.CreateFile("doc (reversed).pdf", [1, 2, 3]);

        var result = await Run(source, TargetCatalog.PdfReverse);

        Assert.True(result.Success, result.Message);
        Assert.Equal("doc (reversed 2).pdf", Path.GetFileName(Assert.Single(result.OutputPaths)));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(existing));
    }

    [Fact]
    public void CheckHealth_Ready()
    {
        Assert.Equal(ConverterHealth.Ready, _converter.CheckHealth());
        Assert.Equal(ConverterHealth.Ready, _converter.CheckHealth());
    }

    [Fact]
    public void Supports_AllPdfTargets_OnlyForPdf()
    {
        var pdfTargets = TargetCatalog.All.Where(t => t.InputKind == FileKind.Pdf).Select(t => t.Id).Order();
        Assert.Equal(pdfTargets, _converter.SupportedTargetIds.Order());
        Assert.True(_converter.CanConvert(".pdf", TargetCatalog.PdfReverse.Id));
        Assert.False(_converter.CanConvert(".png", TargetCatalog.PdfReverse.Id));
    }

    private static int Expected150Dpi(double points) => (int)Math.Round(points / 72 * 150);

    private static ConversionTarget Target(string id) => TargetCatalog.All.Single(t => t.Id == id);

    private static void AssertFailed(ConversionResult result, ConversionError error, string message)
    {
        Assert.False(result.Success);
        Assert.Equal(error, result.Error);
        Assert.Equal(message, result.Message);
        Assert.Empty(result.OutputPaths);
    }

    private static int[] PageIndices(string pdfPath)
    {
        using var document = TestFolder.Read(pdfPath);
        return document.Pages.Cast<PdfSharp.Pdf.PdfPage>().Select(TestFolder.IndexOf).ToArray();
    }

    private Task<ConversionResult> Run(string source, ConversionTarget target, string? pageOrder = null)
    {
        var options = new Dictionary<string, string>();
        if (pageOrder is not null)
        {
            options[OptionKeys.PageOrder] = pageOrder;
        }

        return _converter.ConvertAsync(new ConversionRequest(source, target, options), new SafeOutputWriter(), null, CancellationToken.None);
    }

    private sealed class SyncProgress(Action<double> onReport) : IProgress<double>
    {
        public void Report(double value) => onReport(value);
    }
}
