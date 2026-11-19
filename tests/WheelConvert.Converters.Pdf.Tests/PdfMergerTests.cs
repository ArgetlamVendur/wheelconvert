using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Jobs;
using WheelConvert.Core.Output;
using Xunit;

namespace WheelConvert.Converters.Pdf.Tests;

/// <summary>Acceptance tests 1-6 from docs/modules/WP-14-pdf-merge.md, plus the edge cases from its requirements.</summary>
public sealed class PdfMergerTests : IDisposable
{
    private readonly TestFolder _folder = new();
    private readonly PdfConverter _converter = new();

    public void Dispose() => _folder.Dispose();

    // Acceptance 1
    [Fact]
    public async Task Merge_TwoPlusThreePages_FivePagesInOrder_SourcesUnchanged()
    {
        var a = _folder.CreatePdf(2, "a.pdf");
        var b = _folder.CreatePdf(3, "b.pdf");
        var hashA = TestFolder.Hash(a);
        var hashB = TestFolder.Hash(b);

        var result = await Run([a, b]);

        Assert.True(result.Success, result.Message);
        Assert.Equal("a (merged).pdf", Path.GetFileName(Assert.Single(result.OutputPaths)));
        using var merged = TestFolder.Read(result.OutputPaths[0]);
        Assert.Equal([0, 1, 0, 1, 2], Enumerable.Range(0, merged.PageCount).Select(i => TestFolder.IndexOf(merged.Pages[i])));
        Assert.Equal(hashA, TestFolder.Hash(a));
        Assert.Equal(hashB, TestFolder.Hash(b));
        Assert.Empty(_folder.TempFiles());
    }

    // Acceptance 2
    [Fact]
    public async Task Merge_ViaDropPlanner_NaturalOrder_Doc2BeforeDoc10()
    {
        var ten = _folder.CreatePdf(3, "doc 10.pdf");
        var two = _folder.CreatePdf(2, "doc 2.pdf");
        var registry = new FormatRegistry([_converter], new NullLog());

        var plan = DropPlanner.Plan([ten, two], TargetCatalog.PdfMerge, registry);

        var request = Assert.Single(plan.Requests);
        Assert.Empty(plan.Skipped);
        var result = await _converter.ConvertAsync(request, new SafeOutputWriter(), null, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal("doc 2 (merged).pdf", Path.GetFileName(Assert.Single(result.OutputPaths)));
        using var merged = TestFolder.Read(result.OutputPaths[0]);
        // doc 2 has 2 pages (indices 0, 1), doc 10 has 3: doc 2's pages come first.
        Assert.Equal([0, 1, 0, 1, 2], Enumerable.Range(0, merged.PageCount).Select(i => TestFolder.IndexOf(merged.Pages[i])));
    }

    // Acceptance 3
    [Fact]
    public async Task Merge_OneInputEncrypted_PasswordProtectedNamingIt_NoTempLeft()
    {
        var a = _folder.CreatePdf(1, "a.pdf");
        var locked = _folder.CreatePdf(2, "locked.pdf", userPassword: "secret", ownerPassword: "owner");

        var result = await Run([a, locked]);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.PasswordProtected, result.Error);
        Assert.Equal("locked.pdf is password-protected.", result.Message);
        Assert.Empty(result.OutputPaths);
        Assert.Empty(_folder.TempFiles());
        Assert.Equal(2, Directory.GetFiles(_folder.Path).Length);
    }

    // Acceptance 4
    [Fact]
    public async Task Merge_OneInputGarbage_CorruptInputNamingIt_NoTempLeft()
    {
        var a = _folder.CreatePdf(1, "a.pdf");
        var junk = _folder.CreateFile("junk.pdf", [.. Enumerable.Range(0, 4096).Select(i => (byte)(i * 31 % 251))]);

        var result = await Run([a, junk]);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.CorruptInput, result.Error);
        Assert.Equal("Couldn't read junk.pdf.", result.Message);
        Assert.Empty(result.OutputPaths);
        Assert.Empty(_folder.TempFiles());
        Assert.Equal(2, Directory.GetFiles(_folder.Path).Length);
    }

    // Acceptance 5
    [Fact]
    public async Task Merge_CancelDuringThreeHundredPages_Throws_NoTempLeft()
    {
        var files = new[]
        {
            _folder.CreatePdf(100, "a.pdf"),
            _folder.CreatePdf(100, "b.pdf"),
            _folder.CreatePdf(100, "c.pdf"),
        };
        using var cts = new CancellationTokenSource();
        var progress = new CancelAtProgress(cts, 0.3);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(files, progress, cts.Token));

        Assert.Empty(_folder.TempFiles());
        Assert.Equal(3, Directory.GetFiles(_folder.Path).Length);
    }

    // Acceptance 6
    [Fact]
    public async Task Merge_InputsInTwoFolders_OutputOnlyInFirstFolder()
    {
        var other = Path.Combine(_folder.Path, "other");
        Directory.CreateDirectory(other);
        var first = _folder.CreatePdf(1, "first.pdf");
        var second = Path.Combine(other, "second.pdf");
        File.Copy(_folder.CreatePdf(2, "tmp-second.pdf"), second);

        var result = await Run([first, second]);

        Assert.True(result.Success, result.Message);
        var output = Assert.Single(result.OutputPaths);
        Assert.Equal(_folder.Path, Path.GetDirectoryName(output));
        Assert.Equal("first (merged).pdf", Path.GetFileName(output));
        Assert.Equal(["second.pdf"], Directory.GetFiles(other).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Merge_SingleInput_Unsupported()
    {
        var a = _folder.CreatePdf(1, "a.pdf");

        var result = await Run([a]);

        Assert.Equal(ConversionError.Unsupported, result.Error);
        Assert.Equal("Drop two or more PDFs to merge.", result.Message);
        Assert.Empty(_folder.TempFiles());
    }

    [Fact]
    public async Task Merge_SourceVanished_SourceMissingNamingIt()
    {
        var a = _folder.CreatePdf(1, "a.pdf");

        var result = await Run([a, Path.Combine(_folder.Path, "gone.pdf")]);

        Assert.Equal(ConversionError.SourceMissing, result.Error);
        Assert.Equal("gone.pdf is no longer there.", result.Message);
        Assert.Empty(_folder.TempFiles());
    }

    [Fact]
    public async Task Merge_FirstSourceVanished_SourceMissingNamingIt()
    {
        var b = _folder.CreatePdf(1, "b.pdf");

        var result = await Run([Path.Combine(_folder.Path, "gone.pdf"), b]);

        Assert.Equal(ConversionError.SourceMissing, result.Error);
        Assert.Equal("gone.pdf is no longer there.", result.Message);
    }

    [Fact]
    public async Task Merge_ReportsProgressAfterEachInput()
    {
        var files = new[] { _folder.CreatePdf(1, "a.pdf"), _folder.CreatePdf(1, "b.pdf"), _folder.CreatePdf(1, "c.pdf") };
        var reported = new List<double>();

        var result = await Run(files, new SyncProgress(reported.Add), CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.Equal([1 / 3.0, 2 / 3.0, 1.0], reported);
    }

    [Fact]
    public async Task Merge_CommitFails_OutputFailed_NoTempLeft()
    {
        var files = new[] { _folder.CreatePdf(1, "a.pdf"), _folder.CreatePdf(1, "b.pdf") };
        var request = Request(files);

        var result = await _converter.ConvertAsync(request, new FailingCommitWriter(1), null, CancellationToken.None);

        Assert.Equal(ConversionError.OutputFailed, result.Error);
        Assert.Empty(_folder.TempFiles());
    }

    private Task<ConversionResult> Run(string[] files, IProgress<double>? progress = null, CancellationToken token = default) =>
        _converter.ConvertAsync(Request(files), new SafeOutputWriter(), progress, token);

    private static ConversionRequest Request(string[] files) =>
        new(files[0], TargetCatalog.PdfMerge, new Dictionary<string, string>(), files.Skip(1).ToArray());

    private sealed class SyncProgress(Action<double> onReport) : IProgress<double>
    {
        public void Report(double value) => onReport(value);
    }

    private sealed class NullLog : ILog
    {
        public void Info(string message)
        {
        }

        public void Warn(string message, Exception? exception = null)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }
}
