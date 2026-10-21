using WheelConvert.Core.Output;

namespace WheelConvert.Core.Tests;

public static class OutputWriterTests
{
    [Test]
    public static void Commit_NamesOutputNextToSource_AndKeepsSource()
    {
        using var dir = new TempDir();
        var source = dir.File("photo.png", "SOURCE");
        var writer = new SafeOutputWriter();

        var temp = writer.CreateTempPath(source, ".jpg");
        Assert.True(SafeOutputWriter.IsTempFile(temp));
        Assert.Equal(dir.Path, Path.GetDirectoryName(temp));
        File.WriteAllText(temp, "OUT");

        var final = writer.Commit(temp, source, ".jpg", "converted");
        Assert.Equal(Path.Combine(dir.Path, "photo (converted).jpg"), final);
        Assert.Equal("SOURCE", File.ReadAllText(source));
        Assert.False(File.Exists(temp));
    }

    [Test]
    public static void Commit_NeverOverwrites_AddsNumber()
    {
        using var dir = new TempDir();
        var source = dir.File("photo.png");
        var existing = dir.File("photo (converted).jpg", "KEEP ME");
        var writer = new SafeOutputWriter();

        var temp = writer.CreateTempPath(source, ".jpg");
        File.WriteAllText(temp, "NEW");
        var final = writer.Commit(temp, source, ".jpg", "converted");

        Assert.Equal(Path.Combine(dir.Path, "photo (converted 2).jpg"), final);
        Assert.Equal("KEEP ME", File.ReadAllText(existing));
    }

    [Test]
    public static void Commit_RefusesNonTempFiles()
    {
        using var dir = new TempDir();
        var source = dir.File("photo.png");
        var notTemp = dir.File("important.jpg", "DATA");
        Assert.Throws<InvalidOperationException>(() => new SafeOutputWriter().Commit(notTemp, source, ".jpg", "x"));
        Assert.Equal("DATA", File.ReadAllText(notTemp));
    }

    [Test]
    public static void Commit_ThrowsWhenConverterProducedNothing()
    {
        using var dir = new TempDir();
        var source = dir.File("photo.png");
        var writer = new SafeOutputWriter();
        var temp = writer.CreateTempPath(source, ".jpg");
        Assert.Throws<FileNotFoundException>(() => writer.Commit(temp, source, ".jpg", "converted"));
    }

    [Test]
    public static void Discard_DeletesOnlyTempFiles()
    {
        using var dir = new TempDir();
        var source = dir.File("photo.png", "SOURCE");
        var writer = new SafeOutputWriter();
        var temp = writer.CreateTempPath(source, ".jpg");
        File.WriteAllText(temp, "partial");

        Assert.True(writer.Discard(temp));
        Assert.False(File.Exists(temp));
        Assert.False(writer.Discard(source), "must refuse to delete a normal file");
        Assert.True(File.Exists(source));
        Assert.False(writer.Discard(Path.Combine(dir.Path, "missing.jpg")));
    }

    [Test]
    public static void Labels_AreSanitised()
    {
        Assert.Equal("page 3", SafeOutputWriter.SanitizeLabel("page 3"));
        Assert.Equal("ab", SafeOutputWriter.SanitizeLabel("a/b"));
        Assert.Equal("converted", SafeOutputWriter.SanitizeLabel("..."));
        Assert.Equal("converted", SafeOutputWriter.SanitizeLabel("   "));
        Assert.Equal("x", SafeOutputWriter.SanitizeLabel("x:*?\"<>|"));
    }

    [Test]
    public static void Commit_HandlesUnicodeAndSpacesInNames()
    {
        using var dir = new TempDir();
        var source = dir.File("ଓଡ଼ିଆ फ़ाइल photo.png");
        var writer = new SafeOutputWriter();
        var temp = writer.CreateTempPath(source, ".webp");
        File.WriteAllText(temp, "x");
        var final = writer.Commit(temp, source, ".webp", "converted");
        Assert.Equal("ଓଡ଼ିଆ फ़ाइल photo (converted).webp", Path.GetFileName(final));
    }

    [Test]
    public static void OutputLabels_PadToTotalWidth()
    {
        Assert.Equal("page 3", OutputLabels.Page(3, 9));
        Assert.Equal("page 03", OutputLabels.Page(3, 40));
        Assert.Equal("page 100", OutputLabels.Page(100, 100));
        Assert.Equal("part 2", OutputLabels.Part(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => OutputLabels.Page(5, 4));
    }

    [Test]
    public static void CreateTempPath_RequiresExtension()
    {
        using var dir = new TempDir();
        var source = dir.File("photo.png");
        Assert.Throws<ArgumentException>(() => new SafeOutputWriter().CreateTempPath(source, ""));
    }
}
