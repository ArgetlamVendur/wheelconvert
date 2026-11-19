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
        Assert.True(TextRules.IsJunk("\U000F0000", 0));
        Assert.False(TextRules.IsJunk("a", 0));
    }

    // Finding 5: junk counting on a real PDF. The font's ToUnicode map sends every code to U+E000 (private use).
    [Fact]
    public async Task PdfWhoseTextMapsToPrivateUse_IsReportedAsUnreadable_NoTempLeft()
    {
        var source = Path.Combine(_folder, "doc.pdf");
        File.WriteAllBytes(source, RawPdfWithToUnicode(new string('A', 80), "<E000>"));

        var result = await Run(source);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.NoResult, result.Error);
        Assert.Equal("Couldn't read the text in this PDF.", result.Message);
        Assert.Empty(TempFiles());
    }

    [Fact]
    public async Task RawPdfWithPlainToUnicode_Converts_SoThePrivateUseTestIsMeaningful()
    {
        var source = Path.Combine(_folder, "doc.pdf");
        File.WriteAllBytes(source, RawPdfWithToUnicode(new string('A', 80), "<0041>"));

        var result = await Run(source);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new string('A', 80), string.Concat(ReadParagraphs(result.OutputPaths[0]).Select(p => p.Text)));
    }

    [Fact]
    public async Task RawPdf_ToUnicodeMapIsHonoured()
    {
        var source = Path.Combine(_folder, "doc.pdf");
        File.WriteAllBytes(source, RawPdfWithToUnicode(new string('A', 80), "<0042>"));

        var result = await Run(source);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new string('B', 80), string.Concat(ReadParagraphs(result.OutputPaths[0]).Select(p => p.Text)));
    }

    // Finding 2: line spacing. Each case is one block of four lines that must stay one paragraph.
    [Theory]
    [InlineData(13.2)] // single
    [InlineData(16.5)] // 1.5
    [InlineData(22)] // double
    public async Task SpacedLines_StayOneParagraph(double leading)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);
        for (var i = 0; i < 4; i++)
        {
            page.AddText(Para1, 11, new PdfPoint(50, 700 - (i * leading)), font);
        }

        var result = await Run(Save(builder));

        Assert.True(result.Success, result.Message);
        var paragraph = Assert.Single(ReadParagraphs(result.OutputPaths[0]), p => p.Text.Length > 0);
        Assert.Equal(string.Join(' ', Enumerable.Repeat(Para1, 4)), paragraph.Text);
    }

    [Fact]
    public async Task TwoParagraphsSeparatedByABlankLine_AreTwoParagraphs()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);
        var y = 700.0;
        foreach (var line in new[] { Para1, Para1, Para1, null, Para2, Para2, Para2 })
        {
            if (line is null)
            {
                y -= 2 * 13.2;
                continue;
            }

            page.AddText(line, 11, new PdfPoint(50, y), font);
            y -= 13.2;
        }

        var result = await Run(Save(builder));

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, ReadParagraphs(result.OutputPaths[0]).Count(p => p.Text.Length > 0));
    }

    [Fact]
    public async Task TwoLineHeading_IsOneHeading_AboveTheBody()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);
        page.AddText("A Very Long Report Title That", 28, new PdfPoint(50, 780), font);
        page.AddText("Wraps Onto Two Lines", 28, new PdfPoint(50, 746), font);
        page.AddText(Para1, 11, new PdfPoint(50, 690), font);
        page.AddText(Para1, 11, new PdfPoint(50, 676.8), font);

        var result = await Run(Save(builder));

        Assert.True(result.Success, result.Message);
        var paragraphs = ReadParagraphs(result.OutputPaths[0]).Where(p => p.Text.Length > 0).ToList();
        Assert.True(paragraphs.Count == 2, string.Join(" | ", paragraphs.Select(p => p.Style + ":" + p.Text)));
        Assert.Equal("Heading1", paragraphs[0].Style);
        Assert.Equal("A Very Long Report Title That Wraps Onto Two Lines", paragraphs[0].Text);
        Assert.Null(paragraphs[1].Style);
    }

    [Theory]
    [InlineData(13.2, 13.2, 3, 11, false)]
    [InlineData(16.5, 16.5, 3, 11, false)]
    [InlineData(80, 80, 1, 11, true)]
    [InlineData(26.4, 13.2, 3, 11, true)]
    public void StartsNewParagraph_Rules(double pitch, double median, int count, double body, bool expected) =>
        Assert.Equal(expected, TextRules.StartsNewParagraph(pitch, median, count, body));

    // Finding 3 (S1): the source is only ever read, with sharing that lets other apps keep it open.
    [Fact]
    public async Task SourceHeldOpenByAnotherStream_StillConverts()
    {
        var source = TwoLineSource();
        using var otherApp = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        var result = await Run(source);

        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public async Task ReadOnlySource_StillConverts_AndStaysReadOnly()
    {
        var source = TwoLineSource();
        File.SetAttributes(source, FileAttributes.ReadOnly);
        try
        {
            var result = await Run(source);

            Assert.True(result.Success, result.Message);
            Assert.True(File.GetAttributes(source).HasFlag(FileAttributes.ReadOnly));
        }
        finally
        {
            File.SetAttributes(source, FileAttributes.Normal);
        }
    }

    // Finding 7: the heading styles are defined, and every style a paragraph uses resolves.
    [Fact]
    public async Task StylesPart_DefinesHeading1AndHeading2_WithOutlineLevels()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);
        page.AddText("Big Title", 28, new PdfPoint(50, 780), font);
        page.AddText("Section Heading", 16, new PdfPoint(50, 740), font);
        for (var i = 0; i < 3; i++)
        {
            page.AddText(Para1, 11, new PdfPoint(50, 700 - (i * 13.2)), font);
        }

        var result = await Run(Save(builder));

        Assert.True(result.Success, result.Message);
        using var doc = WordprocessingDocument.Open(result.OutputPaths[0], false);
        var styles = doc.MainDocumentPart!.StyleDefinitionsPart!.Styles!.Elements<Style>().ToDictionary(s => s.StyleId!.Value!);
        Assert.Equal(0, styles["Heading1"].StyleParagraphProperties!.OutlineLevel!.Val!.Value);
        Assert.Equal(1, styles["Heading2"].StyleParagraphProperties!.OutlineLevel!.Val!.Value);
        var used = doc.MainDocumentPart.Document!.Body!.Descendants<ParagraphStyleId>().Select(s => s.Val!.Value).Distinct().ToList();
        Assert.Contains("Heading1", used);
        Assert.Contains("Heading2", used);
        Assert.All(used, id => Assert.True(styles.ContainsKey(id!), id));
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
        var progress = new CancelAtProgress(cts, 0.05);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _converter.ConvertAsync(Request(source), new SafeOutputWriter(), progress, cts.Token));

        // Cancelled between pages: extraction stopped long before the last page (reports go up to 0.9).
        Assert.True(progress.Max < 0.5, $"progress reached {progress.Max}");
        Assert.Empty(TempFiles());
        Assert.Empty(Directory.GetFiles(_folder, "*.docx"));
    }

    [Fact]
    public async Task Cancel_AfterTheTempExists_DiscardsTheTemp()
    {
        var source = TwoLineSource();
        using var cts = new CancellationTokenSource();
        var writer = new CancelOnCreateWriter(cts);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _converter.ConvertAsync(Request(source), writer, null, cts.Token));

        Assert.Equal(1, writer.Created);
        Assert.Empty(TempFiles());
        Assert.Empty(Directory.GetFiles(_folder, "*.docx"));
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

    private string TwoLineSource()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);
        page.AddText(Para1, 11, new PdfPoint(50, 780), font);
        page.AddText(Para2, 11, new PdfPoint(50, 766.8), font);
        return Save(builder);
    }

    /// <summary>A one-page PDF (Helvetica) whose font has a ToUnicode CMap mapping the code for 'A' to <paramref name="unicodeHex"/>.</summary>
    private static byte[] RawPdfWithToUnicode(string text, string unicodeHex)
    {
        var cmap = "/CIDInit /ProcSet findresource begin 12 dict begin begincmap\n"
            + "/CMapName /Adobe-Identity-UCS def /CMapType 2 def\n"
            + "1 begincodespacerange <00> <FF> endcodespacerange\n"
            + $"1 beginbfchar <41> {unicodeHex} endbfchar\n"
            + "endcmap CMapName currentdict /CMap defineresource pop end end";
        var content = $"BT /F1 12 Tf 50 700 Td ({text}) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            // A Type3 font needs no font file; PdfPig ignores ToUnicode on the standard-14 fonts, so Helvetica can't be used here.
            "<< /Type /Font /Subtype /Type3 /FontBBox [0 0 1000 1000] /FontMatrix [0.001 0 0 0.001 0 0] /CharProcs << /a 7 0 R >> "
                + "/Encoding << /Type /Encoding /Differences [65 /a] >> /FirstChar 65 /LastChar 65 /Widths [600] /ToUnicode 6 0 R >>",
            $"<< /Length {cmap.Length} >>\nstream\n{cmap}\nendstream",
            "<< /Length 28 >>\nstream\n600 0 d0 0 0 500 500 re f\nendstream",
        ];

        var sb = new System.Text.StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(sb.Length);
            sb.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }

        var xref = sb.Length;
        sb.Append("xref\n0 ").Append(objects.Length + 1).Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append(offset.ToString("D10")).Append(" 00000 n \n");
        }

        sb.Append("trailer\n<< /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        return System.Text.Encoding.ASCII.GetBytes(sb.ToString());
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

    /// <summary>The real writer, except that the job is cancelled as soon as the temp file has been issued.</summary>
    private sealed class CancelOnCreateWriter(CancellationTokenSource cts) : IOutputWriter
    {
        private readonly SafeOutputWriter _inner = new();

        public int Created { get; private set; }

        public string CreateTempPath(string sourcePath, string outputExtension)
        {
            Created++;
            var temp = _inner.CreateTempPath(sourcePath, outputExtension);
            cts.Cancel();
            return temp;
        }

        public string Commit(string tempPath, string sourcePath, string outputExtension, string label) =>
            _inner.Commit(tempPath, sourcePath, outputExtension, label);

        public bool Discard(string tempPath) => _inner.Discard(tempPath);
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
        public double Max { get; private set; }

        public void Report(double value)
        {
            Max = Math.Max(Max, value);
            if (value >= threshold)
            {
                cts.Cancel();
            }
        }
    }
}
