using System.Security.Cryptography;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Output;
using Xunit;

namespace WheelConvert.Converters.Docx.Tests;

/// <summary>Acceptance tests 1-6 from docs/modules/WP-18-pdf-to-word.md. Every PDF is generated here, in a temp folder.</summary>
public sealed class DocxConverterTests : IDisposable
{
    private const string Para1 = "The quick brown fox jumps over the lazy dog near the river bank today.";
    private const string Para2 = "Pack my box with five dozen liquor jugs and carry it to the old stone house.";
    private const string Para3 = "How vexingly quick daft zebras jump while the sleepy farmers watch from afar.";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "wc-docx-tests", Guid.NewGuid().ToString("N"));
    private readonly DocxConverter _converter = new();

    public DocxConverterTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp folder must not fail a test run.
        }
    }

    // Acceptance 1
    [Fact]
    public async Task TwoPagePdf_TitleAndThreeParagraphs_HeadingParagraphsAndPageBreak_ValidatesClean()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page1 = builder.AddPage(595, 842);
        page1.AddText("Annual Report", 28, new PdfPoint(50, 780), font);
        page1.AddText(Para1, 11, new PdfPoint(50, 700), font);
        page1.AddText(Para2, 11, new PdfPoint(50, 620), font);
        var page2 = builder.AddPage(595, 842);
        page2.AddText(Para3, 11, new PdfPoint(50, 780), font);
        var source = Save(builder);
        var hash = Hash(source);

        var result = await Run(source);

        Assert.True(result.Success, result.Message);
        var output = Assert.Single(result.OutputPaths);
        Assert.Equal("doc (converted).docx", Path.GetFileName(output));
        Assert.Empty(Validate(output));

        var paragraphs = ReadParagraphs(output);
        var heading = Assert.Single(paragraphs, p => p.Style == "Heading1");
        Assert.Equal("Annual Report", heading.Text);
        Assert.Equal([Para1, Para2, Para3], paragraphs.Where(p => p.Style is null && p.Text.Length > 0).Select(p => p.Text));
        Assert.Equal(1, CountPageBreaks(output));
        Assert.Equal(hash, Hash(source));
        Assert.Empty(TempFiles());
    }

    // Acceptance 2
    [Fact]
    public async Task HyphenatedLineEnd_IsJoined_OnlyWhenNextLineIsLowerCase()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);
        page.AddText("This is a long and very detailed exam-", 12, new PdfPoint(50, 700), font);
        page.AddText("ple of text that wraps over two lines.", 12, new PdfPoint(50, 686), font);
        page.AddText("Another sentence that ends with a well-", 12, new PdfPoint(50, 560), font);
        page.AddText("Known name on the next line of text.", 12, new PdfPoint(50, 546), font);
        var source = Save(builder);

        var result = await Run(source);

        Assert.True(result.Success, result.Message);
        var text = string.Join('\n', ReadParagraphs(result.OutputPaths[0]).Select(p => p.Text));
        Assert.Contains("example of text", text);
        Assert.DoesNotContain("exam-", text);
        Assert.Contains("well- Known", text);
    }

    [Theory]
    [InlineData("exam-", "ple", "example")]
    [InlineData("exam-", "Ple", "exam- Ple")]
    [InlineData("a line", "next", "a line next")]
    public void JoinLines_Rules(string first, string second, string expected) =>
        Assert.Equal(expected, TextRules.JoinLines([first, second]));

    [Theory]
    [InlineData(28, 11, "Heading1")]
    [InlineData(16, 11, "Heading2")]
    [InlineData(11, 11, "Normal")]
    [InlineData(14.3, 11, "Heading2")]
    [InlineData(14.2, 11, "Normal")]
    [InlineData(17.6, 11, "Heading1")]
    [InlineData(10, 0, "Normal")]
    public void StyleFor_Thresholds(double block, double body, string expected) =>
        Assert.Equal(expected, TextRules.StyleFor(block, body).ToString());

    // Acceptance 3
    [Fact]
    public async Task ImageOnlyPdf_LooksLikeAScan_NoTempLeft()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(595, 842);
        builder.AddPage(595, 842);
        var source = Save(builder);
        var hash = Hash(source);

        var result = await Run(source);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.NoResult, result.Error);
        Assert.Equal("This PDF looks like a scan: there's no text to copy.", result.Message);
        Assert.Empty(TempFiles());
        Assert.Empty(Directory.GetFiles(_folder, "*.docx"));
        Assert.Equal(hash, Hash(source));
    }

    [Theory]
    [InlineData(1000, 1000, 301, 2, "Couldn't read the text in this PDF.")]
    [InlineData(1000, 1000, 300, 2, null)]
    [InlineData(39, 39, 0, 2, "This PDF looks like a scan: there's no text to copy.")]
    [InlineData(40, 40, 0, 2, null)]
    [InlineData(0, 0, 0, 3, "This PDF looks like a scan: there's no text to copy.")]
    public void RejectReason_Thresholds(long letters, long chars, long junk, int pages, string? expected) =>
        Assert.Equal(expected, TextRules.RejectReason(letters, chars, junk, pages));

    [Fact]
    public void Junk_IsCountedForPrivateUseAndReplacementCharacters()
    {
        Assert.True(TextRules.IsJunk("�", 0));
        Assert.True(TextRules.IsJunk("", 0));
        Assert.False(TextRules.IsJunk("a", 0));
    }

    // Acceptance 4
    [Fact]
    public async Task EncryptedPdf_PasswordProtected_NoTempLeft()
    {
        var path = Path.Combine(_folder, "locked.pdf");
        using (var document = new PdfSharp.Pdf.PdfDocument())
        {
            document.AddPage();
            document.SecuritySettings.UserPassword = "secret";
            document.SecuritySettings.OwnerPassword = "owner";
            document.Save(path);
        }

        var result = await Run(path);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.PasswordProtected, result.Error);
        Assert.Equal("This PDF is password-protected.", result.Message);
        Assert.Empty(TempFiles());
    }

    [Fact]
    public async Task GarbageFile_CorruptInput_NoTempLeft()
    {
        var path = Path.Combine(_folder, "garbage.pdf");
        File.WriteAllBytes(path, [.. "this is definitely not a pdf file at all, just some bytes"u8]);

        var result = await Run(path);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.CorruptInput, result.Error);
        Assert.Empty(TempFiles());
    }

    [Fact]
    public async Task MissingSource_SourceMissing()
    {
        var result = await Run(Path.Combine(_folder, "nowhere.pdf"));

        Assert.Equal(ConversionError.SourceMissing, result.Error);
    }

    // Acceptance 5
    [Fact]
    public async Task Cancel_On200PagePdf_ThrowsAndLeavesNoTemp()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (var i = 0; i < 200; i++)
        {
            builder.AddPage(595, 842).AddText(Para1, 11, new PdfPoint(50, 780), font);
        }

        var source = Save(builder);
        using var cts = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _converter.ConvertAsync(Request(source), new SafeOutputWriter(), new CancelAtProgress(cts, 0.05), cts.Token));

        Assert.Empty(TempFiles());
        Assert.Empty(Directory.GetFiles(_folder, "*.docx"));
    }

    [Fact]
    public async Task Cancel_DuringWrite_DiscardsTheTemp()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(595, 842).AddText(Para1, 11, new PdfPoint(50, 780), font);
        var source = Save(builder);
        using var cts = new CancellationTokenSource();

        // 0.9 is the last extraction report: the token is cancelled after the last page, before the writer starts.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _converter.ConvertAsync(Request(source), new SafeOutputWriter(), new CancelAtProgress(cts, 0.9), cts.Token));

        Assert.Empty(TempFiles());
    }

    [Fact]
    public async Task CommitFailure_IsOutputFailed_AndTempDiscarded()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(595, 842).AddText(Para1, 11, new PdfPoint(50, 780), font);
        var source = Save(builder);

        var result = await _converter.ConvertAsync(Request(source), new FailingCommitWriter(), null, CancellationToken.None);

        Assert.Equal(ConversionError.OutputFailed, result.Error);
        Assert.Empty(TempFiles());
    }

    // Acceptance 6 is asserted by the hash checks above; this one also covers a second run next to the first output.
    [Fact]
    public async Task SecondRun_NeverOverwrites_SourceUnchanged()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(595, 842).AddText(Para1, 11, new PdfPoint(50, 780), font);
        var source = Save(builder);
        var hash = Hash(source);

        var first = await Run(source);
        var second = await Run(source);

        Assert.Equal("doc (converted).docx", Path.GetFileName(first.OutputPaths[0]));
        Assert.Equal("doc (converted 2).docx", Path.GetFileName(second.OutputPaths[0]));
        Assert.Equal(hash, Hash(source));
    }

    [Fact]
    public void Routing_And_Health()
    {
        Assert.Equal("docx", _converter.Id);
        Assert.True(_converter.CanConvert(".pdf", TargetCatalog.PdfToDocx.Id));
        Assert.False(_converter.CanConvert(".png", TargetCatalog.PdfToDocx.Id));
        Assert.False(_converter.CanConvert(".pdf", TargetCatalog.PdfSplitPages.Id));
        Assert.Equal([TargetCatalog.PdfToDocx.Id], _converter.SupportedTargetIds);
        Assert.True(_converter.CheckHealth().IsAvailable);
    }

    [Fact]
    public void XmlSafe_RemovesControlCharacters_KeepsSurrogatePairs()
    {
        Assert.Equal("ab\t", TextRules.XmlSafe("a\0b\u0001\t"));
        Assert.Equal("\U0001F600", TextRules.XmlSafe("\U0001F600"));
        Assert.Equal("x", TextRules.XmlSafe("x\uD800"));
    }

    private ConversionRequest Request(string source) => ConversionRequest.Create(source, TargetCatalog.PdfToDocx);

    private Task<ConversionResult> Run(string source) =>
        _converter.ConvertAsync(Request(source), new SafeOutputWriter(), null, CancellationToken.None);

    private string Save(PdfDocumentBuilder builder)
    {
        var path = Path.Combine(_folder, "doc.pdf");
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    private string[] TempFiles() => Directory.GetFiles(_folder).Where(SafeOutputWriter.IsTempFile).ToArray();

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static List<ValidationErrorInfo> Validate(string docx)
    {
        using var doc = WordprocessingDocument.Open(docx, false);
        return new OpenXmlValidator().Validate(doc).ToList();
    }

    private static List<(string? Style, string Text)> ReadParagraphs(string docx)
    {
        using var doc = WordprocessingDocument.Open(docx, false);
        return doc.MainDocumentPart!.Document!.Body!.Elements<Paragraph>()
            .Select(p => (p.ParagraphProperties?.ParagraphStyleId?.Val?.Value, p.InnerText))
            .ToList();
    }

    private static int CountPageBreaks(string docx)
    {
        using var doc = WordprocessingDocument.Open(docx, false);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<Break>().Count(b => b.Type?.Value == BreakValues.Page);
    }

    /// <summary>The real writer, except that every commit throws like a full disk would.</summary>
    private sealed class FailingCommitWriter : IOutputWriter
    {
        private readonly SafeOutputWriter _inner = new();

        public string CreateTempPath(string sourcePath, string outputExtension) => _inner.CreateTempPath(sourcePath, outputExtension);

        public string Commit(string tempPath, string sourcePath, string outputExtension, string label) =>
            throw new IOException("There is not enough space on the disk.");

        public bool Discard(string tempPath) => _inner.Discard(tempPath);
    }

    /// <summary>Cancels synchronously once progress reaches the threshold (Progress&lt;T&gt; would post asynchronously).</summary>
    private sealed class CancelAtProgress(CancellationTokenSource cts, double threshold) : IProgress<double>
    {
        public void Report(double value)
        {
            if (value >= threshold)
            {
                cts.Cancel();
            }
        }
    }
}
