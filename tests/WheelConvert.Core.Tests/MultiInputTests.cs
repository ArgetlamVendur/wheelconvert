using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Jobs;
using WheelConvert.Core.Output;

namespace WheelConvert.Core.Tests;

/// <summary>WP-13 / ADR-011: multi-input targets, natural sort, MinInputs/MaxInputs, new catalog entries.</summary>
public static class MultiInputTests
{
    private static readonly string[] ScanNames = ["scan 10", "scan 2", "Scan 1", "scan 02"];
    private static readonly string[] FolderNames = ["/z/a 2.jpg", "/a/a 10.jpg", "/m/a 1.jpg"];
    private static readonly string[] Names3 = ["a.png", "b.png", "c.png"];

    private static readonly string[] AllPdfIds =
    [
        "pdf.split-pages", "pdf.to-images", "pdf.rotate-90", "pdf.reorder", "pdf.split-half",
        "pdf.extract-odd", "pdf.extract-even", "pdf.reverse", "pdf.merge", "pdf.to-docx", "pdf.smaller",
    ];

    private static FormatRegistry Registry(string id, params string[] targets) =>
        new([new FakeConverter(id, targets)], new NullLog());

    [Test]
    public static void Natural_Sort_ScanNames()
    {
        var sorted = ScanNames.Order(NaturalFileNameComparer.Instance).ToList();
        Assert.SequenceEqual(["Scan 1", "scan 02", "scan 2", "scan 10"], sorted);
    }

    [Test]
    public static void Natural_Sort_UsesFileNameNotFolder()
    {
        var sorted = FolderNames.Order(NaturalFileNameComparer.Instance).ToList();
        Assert.SequenceEqual(["/m/a 1.jpg", "/z/a 2.jpg", "/a/a 10.jpg"], sorted);
    }

    [Test]
    public static void Plan_Merge_ThreePdfsRandomOrder_OneSortedRequest()
    {
        using var dir = new TempDir();
        var p10 = dir.File("doc 10.pdf");
        var p2 = dir.File("doc 2.pdf");
        var p1 = dir.File("doc 1.pdf");
        var plan = DropPlanner.Plan([p10, p1, p2], TargetCatalog.PdfMerge, Registry("pdf", "pdf.merge"));

        Assert.Equal(1, plan.Requests.Count, "request count");
        Assert.Equal(p1, plan.Requests[0].SourcePath);
        Assert.SequenceEqual([p1, p2, p10], plan.Requests[0].AllSourcePaths);
        Assert.SequenceEqual([p2, p10], plan.Requests[0].AdditionalSourcePaths!);
        Assert.Equal(0, plan.Skipped.Count, "skipped");
    }

    [Test]
    public static void Plan_Merge_OnePdfAndOneJpg_NoRequest()
    {
        using var dir = new TempDir();
        var pdf = dir.File("a.pdf");
        var jpg = dir.File("b.jpg");
        var plan = DropPlanner.Plan([pdf, jpg], TargetCatalog.PdfMerge, Registry("pdf", "pdf.merge"));

        Assert.Equal(0, plan.Requests.Count, "requests");
        Assert.Equal("Drop at least 2 files for Merge.", plan.Skipped.Single(s => s.Path == pdf).Reason);
        Assert.Equal("Not a Pdf file.", plan.Skipped.Single(s => s.Path == jpg).Reason);
    }

    [Test]
    public static void Plan_ImageToPdf_OneImage_OneRequestNoAdditional()
    {
        using var dir = new TempDir();
        var png = dir.File("a.png");
        var plan = DropPlanner.Plan([png], TargetCatalog.ImageToPdf, Registry("image", "image.to-pdf"));

        Assert.Equal(1, plan.Requests.Count, "requests");
        Assert.True(plan.Requests[0].AdditionalSourcePaths is null);
        Assert.SequenceEqual([png], plan.Requests[0].AllSourcePaths);
    }

    [Test]
    public static void Plan_ImageToPdf_DuplicatePathsDedupedBeforeSort()
    {
        using var dir = new TempDir();
        var a = dir.File("a.png");
        var plan = DropPlanner.Plan([a, a], TargetCatalog.ImageToPdf, Registry("image", "image.to-pdf"));

        Assert.True(plan.Requests[0].AdditionalSourcePaths is null);
    }

    [Test]
    public static void Plan_Reverse_TwoPdfs_NoRequestBothSkipped()
    {
        using var dir = new TempDir();
        var a = dir.File("a.pdf");
        var b = dir.File("b.pdf");
        var plan = DropPlanner.Plan([a, b], TargetCatalog.PdfReverse, Registry("pdf", "pdf.reverse"));

        Assert.Equal(0, plan.Requests.Count, "requests");
        Assert.Equal(2, plan.Skipped.Count, "skipped");
        Assert.True(plan.Skipped.All(s => s.Reason == "Drop one file at a time for Reverse."));
    }

    [Test]
    public static void Plan_MaxInputsAboveOne_UsesAtMostMessage()
    {
        using var dir = new TempDir();
        // Reuses a real catalog ID so the registry can resolve a converter; only the limits differ.
        var target = new ConversionTarget(TargetCatalog.ImageJpg.Id, "Cap", FileKind.Image, ".jpg", 1, MaxInputs: 2);
        var files = Names3.Select(n => dir.File(n)).ToList();
        var plan = DropPlanner.Plan(files, target, Registry("image", "image.jpg"));

        Assert.Equal(0, plan.Requests.Count, "requests");
        Assert.True(plan.Skipped.All(s => s.Reason == "Drop at most 2 files for Cap."));
    }

    [Test]
    public static void Plan_NonCombining_StillOneRequestPerFile()
    {
        using var dir = new TempDir();
        var a = dir.File("a.png");
        var b = dir.File("b.png");
        var plan = DropPlanner.Plan([b, a], TargetCatalog.ImageJpg, Registry("image", "image.jpg"));

        Assert.SequenceEqual([b, a], plan.Requests.Select(r => r.SourcePath));
        Assert.True(plan.Requests.All(r => r.AdditionalSourcePaths is null));
    }

    [Test]
    public static void GetTargetsFor_Merge_NeedsTwoPdfs()
    {
        var registry = Registry("pdf", "pdf.merge", "pdf.split-pages");
        Assert.SequenceEqual(["pdf.split-pages"], registry.GetTargetsFor(["a.pdf"]).Select(t => t.Id));
        Assert.SequenceEqual(["pdf.split-pages", "pdf.merge"], registry.GetTargetsFor(["a.pdf", "b.pdf"]).Select(t => t.Id));
        Assert.SequenceEqual(["pdf.split-pages"], registry.GetTargetsFor(["a.pdf", "b.jpg"]).Select(t => t.Id));
    }

    [Test]
    public static void GetTargetsFor_TwoPdfs_HidesSingleFileTargets()
    {
        var registry = Registry("pdf", AllPdfIds);
        var ids = registry.GetTargetsFor(["a.pdf", "b.pdf"]).Select(t => t.Id).ToList();

        foreach (var hidden in new[] { "pdf.reorder", "pdf.split-half", "pdf.extract-odd", "pdf.extract-even", "pdf.reverse" })
        {
            Assert.False(ids.Contains(hidden), hidden + " should be hidden");
        }

        foreach (var shown in new[] { "pdf.split-pages", "pdf.to-images", "pdf.rotate-90", "pdf.merge" })
        {
            Assert.True(ids.Contains(shown), shown + " should be shown");
        }

        Assert.Equal(6, ids.Count, "2+ PDFs show 6 segments");
        Assert.Equal(10, registry.GetTargetsFor(["a.pdf"]).Count, "1 PDF shows 10 segments");
    }

    [Test]
    public static void IsRedundantFor_SmallerAndResize_NeverRedundant()
    {
        Assert.False(TargetCatalog.ImageSmaller.IsRedundantFor(".jpg"));
        Assert.False(TargetCatalog.ImageResize.IsRedundantFor(".jpg"));
        Assert.False(TargetCatalog.ImageResize.IsRedundantFor(".png"));
    }

    [Test]
    public static void Catalog_LabelsFitAndIdsUnique()
    {
        Assert.True(TargetCatalog.All.All(t => t.Label.Length <= 8), "labels <= 8 chars");
        Assert.Equal(TargetCatalog.All.Count, TargetCatalog.All.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count(), "unique IDs");
        foreach (var id in new[] { "image.to-pdf", "image.smaller", "image.resize", "video.smaller", "pdf.merge", "pdf.to-docx", "pdf.smaller" })
        {
            Assert.True(TargetCatalog.Find(id) is not null, id);
        }
    }

    [Test]
    public static void Target_ValidatesInputLimits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ConversionTarget("a", "A", FileKind.Pdf, ".pdf", 1, MinInputs: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ConversionTarget("a", "A", FileKind.Pdf, ".pdf", 1, MinInputs: 3, MaxInputs: 2));
        var ok = new ConversionTarget("a", "A", FileKind.Pdf, ".pdf", 1, MinInputs: 2, MaxInputs: 2);
        Assert.Equal(2, ok.MinInputs);
        Assert.Equal(2, ok.MaxInputs);
    }

    [Test]
    public static void OutputLabels_NewConstants()
    {
        Assert.Equal("combined", OutputLabels.Combined);
        Assert.Equal("merged", OutputLabels.Merged);
        Assert.Equal("smaller", OutputLabels.Smaller);
        Assert.Equal("resized", OutputLabels.Resized);
    }
}
