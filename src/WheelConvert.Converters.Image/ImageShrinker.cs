using ImageMagick;

namespace WheelConvert.Converters.Image;

/// <summary>
/// WP-16: the fixed "Smaller" preset for images. Pure image work; ImageConverter owns reading, temp files, the 5 % check and errors.
/// </summary>
internal static class ImageShrinker
{
    internal const uint LongEdgeLimit = 1920;
    internal const uint JpegWebpQuality = 75;

    /// <summary>A result must be at least this much smaller than the source (WP-16 req. 4): 5 %.</summary>
    internal const double MinimumSavingFraction = 0.05;

    internal const string AnimatedGifMessage = "Animated GIFs can't be made smaller yet.";
    internal const string AlreadySmallMessage = "This image is already about as small as it gets.";

    // Colour-profile names ImageMagick uses; everything else (EXIF with GPS, XMP, IPTC, 8BIM, comments) is dropped.
    private static readonly string[] KeptProfiles = ["icc", "icm"];

    /// <summary>The extension the output gets for a given (lower-case) source extension, or null when the source can't be shrunk.</summary>
    internal static string? OutputExtensionFor(string sourceExtension) => sourceExtension switch
    {
        ".jpg" or ".jpeg" => ".jpg",
        ".webp" => ".webp",
        ".png" => ".png",
        // Most apps and portals can't open HEIC/AVIF, so they become JPEG.
        ".heic" or ".heif" or ".avif" => ".jpg",
        ".gif" or ".bmp" or ".tif" or ".tiff" => ".png",
        _ => null,
    };

    /// <summary>True when the file has more than one frame. Reads headers only.</summary>
    internal static bool IsAnimatedGif(string path)
    {
        var infos = MagickImageInfo.ReadCollection(path, new MagickReadSettings { Format = MagickFormat.Gif });
        return infos.Count() > 1;
    }

    /// <summary>Upright, at most 1920 px on the long edge (never enlarged), metadata stripped except the colour profile.</summary>
    internal static void Prepare(MagickImage image)
    {
        image.AutoOrient();

        if (Math.Max(image.Width, image.Height) > LongEdgeLimit)
        {
            // Fit inside a 1920 x 1920 box: the long edge becomes 1920 and the ratio is kept.
            image.Resize(new MagickGeometry(LongEdgeLimit, LongEdgeLimit));
        }

        var kept = new List<IImageProfile>();
        foreach (var name in KeptProfiles)
        {
            if (image.GetProfile(name) is { } profile)
            {
                kept.Add(profile);
            }
        }

        image.Strip();
        foreach (var profile in kept)
        {
            image.SetProfile(profile);
        }
    }

    /// <summary>Applies the output settings for <paramref name="extension"/> (from <see cref="OutputExtensionFor"/>) and returns the Magick format.</summary>
    internal static MagickFormat Configure(MagickImage image, string extension)
    {
        switch (extension)
        {
            case ".jpg":
                if (image.HasAlpha)
                {
                    image.ColorAlpha(MagickColors.White);
                }

                image.Quality = JpegWebpQuality;
                image.Settings.SetDefine(MagickFormat.Jpeg, "sampling-factor", "4:2:0");
                image.Settings.Interlace = Interlace.Plane; // progressive JPEG
                return MagickFormat.Jpeg;
            case ".webp":
                image.Quality = JpegWebpQuality;
                return MagickFormat.WebP;
            default:
                // PNG quality 95 = zlib level 9 with adaptive filtering: the smallest lossless output.
                image.Quality = 95;
                image.Settings.SetDefine(MagickFormat.Png, "compression-level", "9");
                return MagickFormat.Png;
        }
    }

    /// <summary>True when <paramref name="outputLength"/> is at least 5 % below <paramref name="sourceLength"/>.</summary>
    internal static bool IsSmallEnough(long outputLength, long sourceLength) =>
        outputLength <= sourceLength * (1.0 - MinimumSavingFraction);
}
