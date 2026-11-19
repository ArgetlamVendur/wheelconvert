using System.Security.Cryptography;
using ImageMagick;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Output;
using Xunit;

namespace WheelConvert.Converters.Image.Tests;

/// <summary>WP-16 acceptance tests 1-5 for image.smaller. All images are generated here in a temp folder.</summary>
[Collection("MagickLimits")]
public sealed class ImageShrinkerTests : IDisposable
{
    private readonly string _dir;
    private readonly ListLog _log = new();
    private readonly ImageConverter _converter;

    public ImageShrinkerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "wc-shrink-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _converter = new ImageConverter(_log);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover folder in %TEMP% is harmless.
        }
    }

    private string P(string name) => Path.Combine(_dir, name);

    private static MagickImage Photo(uint width, uint height)
    {
        // Plasma is photo-like: it has smooth gradients plus noise, so JPEG q95 is much larger than q75.
        var image = new MagickImage("plasma:fractal", width, height);
        image.AddNoise(NoiseType.Gaussian);
        return image;
    }

    private async Task<ConversionResult> ShrinkAsync(string source) =>
        await _converter.ConvertAsync(ConversionRequest.Create(source, TargetCatalog.ImageSmaller), new SafeOutputWriter(), null, CancellationToken.None);

    private string[] TempFiles() => Directory.GetFiles(_dir).Where(SafeOutputWriter.IsTempFile).ToArray();

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static void AssertGpsGone(string path)
    {
        using var image = new MagickImage(path);
        var exif = image.GetExifProfile();
        Assert.True(exif is null || !exif.Values.Any(v => v.Tag.ToString()!.StartsWith("GPS", StringComparison.Ordinal)), "GPS data is still in the file");
        Assert.Null(image.GetXmpProfile());
    }

    // ---- Acceptance 1 ----

    [Fact]
    public async Task Test1_large_jpeg_q95_is_1920_wide_smaller_upright_and_without_gps()
    {
        var source = P("big.jpg");
        using (var image = Photo(4000, 3000))
        {
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.GPSLatitudeRef, "N");
            exif.SetValue(ExifTag.GPSLongitudeRef, "E");
            exif.SetValue(ExifTag.Make, "TestCam");
            image.SetProfile(exif);
            image.Quality = 95;
            image.Write(source, MagickFormat.Jpeg);
        }

        using (var check = new MagickImage(source))
        {
            Assert.Contains(check.GetExifProfile()!.Values, v => v.Tag.ToString()!.StartsWith("GPS", StringComparison.Ordinal));
        }

        var before = Hash(source);

        var result = await ShrinkAsync(source);

        Assert.True(result.Success, result.Message);
        var output = result.OutputPaths.Single();
        Assert.Equal(P("big (smaller).jpg"), output);
        using var info = new MagickImage(output);
        Assert.Equal(1920u, info.Width);
        Assert.Equal(1440u, info.Height);
        Assert.True(new FileInfo(output).Length < new FileInfo(source).Length * 0.95);
        AssertGpsGone(output);
        Assert.Equal(before, Hash(source));
        Assert.Empty(TempFiles());
    }

    [Fact]
    public async Task Exif_rotated_photo_comes_out_upright()
    {
        var source = P("rot.jpg");
        using (var image = Photo(600, 300))
        {
            image.Orientation = Orientation.RightTop;
            var exif = image.GetExifProfile() ?? new ExifProfile();
            exif.SetValue(ExifTag.Orientation, (ushort)6);
            image.SetProfile(exif);
            image.Quality = 95;
            image.Write(source, MagickFormat.Jpeg);
        }

        var result = await ShrinkAsync(source);

        Assert.True(result.Success, result.Message);
        using var output = new MagickImage(result.OutputPaths.Single());
        Assert.Equal(300u, output.Width);
        Assert.Equal(600u, output.Height);
    }

    [Fact]
    public async Task Colour_profile_is_kept_while_other_metadata_is_stripped()
    {
        var source = P("profile.jpg");
        using (var image = Photo(1000, 800))
        {
            image.SetProfile(ColorProfiles.SRGB);
            var exif = new ExifProfile();
            exif.SetValue(ExifTag.GPSLatitudeRef, "S");
            exif.SetValue(ExifTag.Make, "TestCam");
            image.SetProfile(exif);
            image.Quality = 95;
            image.Write(source, MagickFormat.Jpeg);
        }

        using (var check = new MagickImage(source))
        {
            Assert.NotNull(check.GetColorProfile());
        }

        var result = await ShrinkAsync(source);

        Assert.True(result.Success, result.Message);
        using var output = new MagickImage(result.OutputPaths.Single());
        Assert.NotNull(output.GetColorProfile());
        AssertGpsGone(result.OutputPaths.Single());
        Assert.Null(output.GetExifProfile()?.GetValue(ExifTag.Make));
    }

    [Fact]
    public async Task Jpeg_output_is_progressive_420_at_quality_75()
    {
        var source = P("settings.jpg");
        using (var image = Photo(1200, 900))
        {
            image.Quality = 98;
            image.Write(source, MagickFormat.Jpeg);
        }

        var result = await ShrinkAsync(source);

        Assert.True(result.Success, result.Message);
        using var output = new MagickImage(result.OutputPaths.Single());
        Assert.Equal(Interlace.Jpeg, output.Interlace);
        Assert.Equal("2x2,1x1,1x1", output.GetAttribute("jpeg:sampling-factor"));
        Assert.InRange(output.Quality, 74u, 76u);
    }

    // ---- Acceptance 2 and never-enlarge ----

    [Fact]
    public async Task Test2_small_low_quality_jpeg_is_not_enlarged_and_is_refused_without_a_temp_file()
    {
        var source = P("small.jpg");
        using (var image = Photo(800, 600))
        {
            image.Quality = 60;
            image.Write(source, MagickFormat.Jpeg);
        }

        var before = Hash(source);

        var result = await ShrinkAsync(source);

        Assert.False(result.Success);
        Assert.Equal(ConversionError.NoResult, result.Error);
        Assert.Equal("This image is already about as small as it gets.", result.Message);
        Assert.Empty(result.OutputPaths);
        Assert.Empty(TempFiles());
        Assert.Equal([source], Directory.GetFiles(_dir));
        Assert.Equal(before, Hash(source));
    }

    [Theory]
    [InlineData(800u, 600u, 800u, 600u)]
    [InlineData(1920u, 1080u, 1920u, 1080u)] // exactly on the limit: untouched
    [InlineData(2000u, 1000u, 1920u, 960u)]
    [InlineData(3000u, 4000u, 1440u, 1920u)] // portrait: the long edge is the height
    public async Task Long_edge_is_limited_to_1920_and_never_enlarged(uint width, uint height, uint expectedWidth, uint expectedHeight)
    {
        var source = P("size.jpg");
        using (var image = Photo(width, height))
        {
            image.Quality = 98;
            image.Write(source, MagickFormat.Jpeg);
        }

        var result = await ShrinkAsync(source);

        Assert.True(result.Success, result.Message);
        using var output = new MagickImage(result.OutputPaths.Single());
        Assert.Equal(expectedWidth, output.Width);
        Assert.Equal(expectedHeight, output.Height);
    }

    [Theory]
    [InlineData(100L, 94L, true)]
    [InlineData(100L, 95L, true)] // exactly 5 % smaller counts
    [InlineData(100L, 96L, false)]
    [InlineData(100L, 100L, false)]
    [InlineData(100L, 150L, false)]
    public void Five_percent_rule(long sourceLength, long outputLength, bool expected) =>
        Assert.Equal(expected, ImageShrinker.IsSmallEnough(outputLength, sourceLength));

    // ---- Output format per source type ----

    [Theory]
    [InlineData(".jpg", ".jpg")]
    [InlineData(".jpeg", ".jpg")]
    [InlineData(".webp", ".webp")]
    [InlineData(".png", ".png")]
    [InlineData(".bmp", ".png")]
    [InlineData(".tif", ".png")]
    [InlineData(".tiff", ".png")]
    [InlineData(".gif", ".png")]
    [InlineData(".heic", ".jpg")]
    [InlineData(".heif", ".jpg")]
    [InlineData(".avif", ".jpg")]
    [InlineData(".ico", null)]
    [InlineData(".txt", null)]
    public void Output_extension_per_source_type(string source, string? expected) =>
        Assert.Equal(expected, ImageShrinker.OutputExtensionFor(source));

    [Theory]
    [InlineData(".png", ".png")]
    [InlineData(".bmp", ".png")]
    [InlineData(".tif", ".png")]
    [InlineData(".gif", ".png")]
    [InlineData(".webp", ".webp")]
    [InlineData(".jpeg", ".jpg")]
    public async Task Real_conversion_gives_the_chosen_extension_and_format(string sourceExt, string expectedExt)
    {
        var source = P("photo" + sourceExt);
        using (var image = Photo(1000, 700))
        {
            switch (sourceExt)
            {
                case ".png":
                    image.Quality = 1; // fast, weak compression: a big source
                    image.Write(source, MagickFormat.Png);
                    break;
                case ".bmp":
                    image.Write(source, MagickFormat.Bmp);
                    break;
                case ".tif":
                    image.Write(source, MagickFormat.Tiff);
                    break;
                case ".gif":
                    image.Write(source, MagickFormat.Gif);
                    break;
                case ".webp":
                    image.Quality = 100;
                    image.Write(source, MagickFormat.WebP);
                    break;
                default:
                    image.Quality = 98;
                    image.Write(source, MagickFormat.Jpeg);
                    break;
            }
        }

        var result = await ShrinkAsync(source);

        Assert.True(result.Success, result.Message);
        var output = result.OutputPaths.Single();
        Assert.Equal(expectedExt, Path.GetExtension(output));
        Assert.EndsWith(" (smaller)" + expectedExt, output, StringComparison.Ordinal);
        var expectedFormat = expectedExt switch { ".jpg" => MagickFormat.Jpeg, ".webp" => MagickFormat.WebP, _ => MagickFormat.Png };
        Assert.Equal(expectedFormat, new MagickImageInfo(output).Format);
        Assert.Empty(TempFiles());
    }

    // ---- Acceptance 3 ----

    [Theory]
    [InlineData(".heic")]
    [InlineData(".avif")]
    public async Task Test3_heic_and_avif_become_a_jpg(string ext)
    {
        // This Magick build has no HEIC encoder (probed), so HEIC is only exercised when a sample can be written; AVIF
        // takes the same code path (OutputExtensionFor maps both to .jpg) and is the sample that always runs.
        var source = P("phone" + ext);
        try
        {
            using var image = Photo(1500, 1000);
            image.Quality = 100;
            image.Write(source, ext == ".heic" ? MagickFormat.Heic : MagickFormat.Avif);
        }
        catch (MagickException ex) when (ext == ".heic")
        {
            Assert.True(ex.Message.Contains("no encode delegate", StringComparison.OrdinalIgnoreCase), ex.Message);
            return;
        }

        var result = await ShrinkAsync(source);

        Assert.True(result.Success, result.Message);
        var output = result.OutputPaths.Single();
        Assert.Equal(P("phone (smaller).jpg"), output);
        Assert.Equal(MagickFormat.Jpeg, new MagickImageInfo(output).Format);
    }

    // ---- Acceptance 4 ----

    [Fact]
    public async Task Test4_png_with_alpha_stays_png_and_keeps_its_alpha()
    {
        var source = P("alpha.png");
        using (var image = Photo(800, 600))
        {
            image.HasAlpha = true;
            using var hole = new MagickImage(MagickColors.Transparent, 200, 200);
            image.Composite(hole, 50, 50, CompositeOperator.Copy);
            image.Quality = 1;
            image.Write(source, MagickFormat.Png);
        }

        var result = await ShrinkAsync(source);

        Assert.True(result.Success, result.Message);
        var output = result.OutputPaths.Single();
        Assert.Equal(".png", Path.GetExtension(output));
        using var shrunk = new MagickImage(output);
        Assert.True(shrunk.HasAlpha);
        using var pixels = shrunk.GetPixels();
        Assert.Equal(0, pixels.GetPixel(100, 100).ToColor()!.A);
        Assert.Equal(Quantum.Max, pixels.GetPixel(600, 500).ToColor()!.A);
    }

    // ---- Acceptance 5 ----

    [Fact]
    public async Task Test5_animated_gif_is_unsupported_and_leaves_nothing_behind()
    {
        var source = P("anim.gif");
        using (var frames = new MagickImageCollection())
        {
            frames.Add(new MagickImage(MagickColors.Red, 100, 100));
            frames.Add(new MagickImage(MagickColors.Blue, 100, 100));
            foreach (var frame in frames)
            {
                frame.AnimationDelay = 10;
            }

            frames.Write(source, MagickFormat.Gif);
        }

        var before = Hash(source);

        var result = await ShrinkAsync(source);

        Assert.Equal(ConversionError.Unsupported, result.Error);
        Assert.Equal("Animated GIFs can't be made smaller yet.", result.Message);
        Assert.Empty(TempFiles());
        Assert.Equal([source], Directory.GetFiles(_dir));
        Assert.Equal(before, Hash(source));
    }

    // ---- Registration ----

    [Theory]
    [InlineData(".jpg", true)]
    [InlineData(".heic", true)]
    [InlineData(".avif", true)]
    [InlineData(".gif", true)]
    [InlineData(".ico", false)]
    [InlineData(".txt", false)]
    public void CanConvert_smaller(string ext, bool expected) =>
        Assert.Equal(expected, _converter.CanConvert(ext, TargetCatalog.ImageSmaller.Id));

    [Fact]
    public async Task Corrupt_input_logs_the_file_name_but_not_the_folder()
    {
        var source = P("broken.jpg");
        File.WriteAllBytes(source, new byte[2000]);

        var result = await ShrinkAsync(source);

        Assert.False(result.Success);
        Assert.NotEmpty(_log.Lines);
        Assert.All(_log.Lines, line => Assert.DoesNotContain(_dir, line, StringComparison.OrdinalIgnoreCase));
        Assert.Empty(TempFiles());
    }
}
