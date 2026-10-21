using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;

namespace WheelConvert.Core.Tests;

public static class FormatsTests
{
    [Test]
    public static void Classify_IsCaseInsensitive_AndHandlesPaths()
    {
        Assert.Equal(FileKind.Image, FileClassifier.Classify("C:\\x\\Photo.JPG"));
        Assert.Equal(FileKind.Image, FileClassifier.Classify("/tmp/a.heic"));
        Assert.Equal(FileKind.Audio, FileClassifier.Classify("song.FLAC"));
        Assert.Equal(FileKind.Video, FileClassifier.Classify("clip.mkv"));
        Assert.Equal(FileKind.Pdf, FileClassifier.Classify("doc.pdf"));
        Assert.Equal(FileKind.Unsupported, FileClassifier.Classify("notes.txt"));
        Assert.Equal(FileKind.Unsupported, FileClassifier.Classify("noextension"));
        Assert.Equal(FileKind.Unsupported, FileClassifier.Classify("trailingdot."));
    }

    [Test]
    public static void NormalizeExtension_AcceptsBareExtensionsAndPaths()
    {
        Assert.Equal(".png", FileClassifier.NormalizeExtension(".PNG"));
        Assert.Equal(".png", FileClassifier.NormalizeExtension("a.b.PNG"));
        Assert.Equal(".gz", FileClassifier.NormalizeExtension("archive.tar.gz"));
        Assert.Equal(string.Empty, FileClassifier.NormalizeExtension("README"));
    }

    [Test]
    public static void AreEquivalent_GroupsSpellings()
    {
        Assert.True(FileClassifier.AreEquivalent(".jpg", "photo.JPEG"));
        Assert.True(FileClassifier.AreEquivalent(".tif", ".tiff"));
        Assert.False(FileClassifier.AreEquivalent(".jpg", ".png"));
        Assert.False(FileClassifier.AreEquivalent("", ""));
    }

    [Test]
    public static void Catalog_IdsAreUnique_LabelsShort_ExtensionsNormalised()
    {
        var ids = TargetCatalog.All.Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count(), "unique ids");
        foreach (var t in TargetCatalog.All)
        {
            Assert.True(t.Label.Length <= 8, $"label too long: {t.Label}");
            Assert.True(t.InputKind != FileKind.Unsupported, $"{t.Id} has no input kind");
            Assert.True(t.ProducesText == (t.OutputExtension is null), $"{t.Id}: text targets have no extension and vice versa");
            if (t.OutputExtension is not null)
            {
                Assert.Equal(FileClassifier.NormalizeExtension(t.OutputExtension), t.OutputExtension, t.Id);
            }
        }
    }

    [Test]
    public static void Registry_ExcludesSameFormat_Disabled_AndUnavailable()
    {
        var image = new FakeConverter("image", "image.jpg", "image.png", "image.webp", "image.ico");
        var qr = new FakeConverter("qr", "image.read-qr") { Health = ConverterHealth.NotImplemented };
        var registry = new FormatRegistry([image, qr], new NullLog());

        var targets = registry.GetTargetsFor("photo.jpeg", new HashSet<string> { "image.ico" });
        Assert.SequenceEqual(["image.png", "image.webp"], targets.Select(t => t.Id));
        Assert.Equal(1, registry.Unavailable.Count);
        Assert.Equal("qr", registry.Unavailable[0].ConverterId);
    }

    [Test]
    public static void Registry_RespectsMaxCount_PriorityOrder_AndCanConvert()
    {
        var image = new FakeConverter("image", "image.jpg", "image.png", "image.webp", "image.ico")
        {
            CanConvertFunc = (ext, target) => !(ext == ".gif" && target == "image.ico"),
        };
        var registry = new FormatRegistry([image], new NullLog());

        Assert.SequenceEqual(["image.jpg", "image.png"], registry.GetTargetsFor("a.gif", maxCount: 2).Select(t => t.Id));
        Assert.SequenceEqual(["image.jpg", "image.png", "image.webp"], registry.GetTargetsFor("a.gif").Select(t => t.Id));
        Assert.Equal(0, registry.GetTargetsFor("notes.txt").Count);
        Assert.True(registry.ResolveConverter("a.gif", "image.ico") is null);
    }

    [Test]
    public static void Registry_Refresh_PicksUpNewlyAvailableConverter()
    {
        var media = new FakeConverter("media", "audio.mp3") { Health = ConverterHealth.Missing("FFmpeg not found") };
        var registry = new FormatRegistry([media], new NullLog());
        Assert.Equal(0, registry.GetTargetsFor("a.wav").Count);

        media.Health = ConverterHealth.Ready;
        registry.Refresh();
        Assert.Equal(1, registry.GetTargetsFor("a.wav").Count);
    }

    [Test]
    public static void Registry_HealthCheckThatThrows_IsTreatedAsUnavailable()
    {
        var bad = new ThrowingHealthConverter();
        var log = new NullLog();
        var registry = new FormatRegistry([bad], log);
        Assert.Equal(1, registry.Unavailable.Count);
        Assert.True(log.Lines.Any(l => l.StartsWith("ERROR", StringComparison.Ordinal)));
    }

    [Test]
    public static void Registry_RejectsDuplicateConverterIds()
    {
        Assert.Throws<ArgumentException>(() => _ = new FormatRegistry([new FakeConverter("x"), new FakeConverter("x")], new NullLog()));
    }

    [Test]
    public static void Registry_IgnoresUnknownTargetIds()
    {
        var log = new NullLog();
        var registry = new FormatRegistry([new FakeConverter("x", "image.does-not-exist")], log);
        Assert.Equal(0, registry.GetTargetsFor("a.png").Count);
        Assert.True(log.Lines.Any(l => l.Contains("unknown target", StringComparison.Ordinal)));
    }

    private sealed class ThrowingHealthConverter : IConverter
    {
        public string Id => "throws";

        public IReadOnlyCollection<string> SupportedTargetIds => ["image.jpg"];

        public bool CanConvert(string sourceExtension, string targetId) => true;

        public ConverterHealth CheckHealth() => throw new InvalidOperationException("boom");

        public Task<ConversionResult> ConvertAsync(ConversionRequest request, IOutputWriter output, IProgress<double>? progress, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
