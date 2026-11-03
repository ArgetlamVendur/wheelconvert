using System.Globalization;
using ImageMagick;
using ImageMagick.Configuration;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;

namespace WheelConvert.Converters.Image;

/// <summary>
/// Converts between image formats with Magick.NET (WP-06).
/// Targets: image.jpg, image.png, image.webp, image.ico.
/// </summary>
public sealed class ImageConverter : IConverter
{
    private static readonly bool IsSecurityInitialized;
    private static readonly string? InitializationFailureReason;

    private static readonly HashSet<string> Inputs = new(StringComparer.Ordinal)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif", ".bmp", ".tif", ".tiff", ".gif", ".ico", ".avif",
    };

    private static readonly Dictionary<string, MagickFormat> ExtensionFormats = new(StringComparer.Ordinal)
    {
        [".jpg"] = MagickFormat.Jpeg,
        [".jpeg"] = MagickFormat.Jpeg,
        [".png"] = MagickFormat.Png,
        [".webp"] = MagickFormat.WebP,
        [".heic"] = MagickFormat.Heic,
        [".heif"] = MagickFormat.Heif,
        [".bmp"] = MagickFormat.Bmp,
        [".tif"] = MagickFormat.Tiff,
        [".tiff"] = MagickFormat.Tiff,
        [".gif"] = MagickFormat.Gif,
        [".ico"] = MagickFormat.Ico,
        [".avif"] = MagickFormat.Avif,
    };

    static ImageConverter()
    {
        try
        {
            InitializeSecurityAndLimits();
            IsSecurityInitialized = true;
        }
        catch (Exception ex)
        {
            IsSecurityInitialized = false;
            InitializationFailureReason = ex.Message;
        }
    }

    public string Id => "image";

    public IReadOnlyCollection<string> SupportedTargetIds { get; } =
    [
        TargetCatalog.ImageJpg.Id,
        TargetCatalog.ImagePng.Id,
        TargetCatalog.ImageWebp.Id,
        TargetCatalog.ImageIco.Id,
    ];

    public bool CanConvert(string sourceExtension, string targetId)
    {
        if (!IsSecurityInitialized)
        {
            return false;
        }

        // Refuse .ico -> image.ico (same format is filtered anyway) and .gif -> image.ico (animated GIF to icon is meaningless)
        if (targetId == TargetCatalog.ImageIco.Id && (sourceExtension == ".ico" || sourceExtension == ".gif"))
        {
            return false;
        }

        return Inputs.Contains(sourceExtension) && SupportedTargetIds.Contains(targetId);
    }

    public ConverterHealth CheckHealth()
    {
        if (!IsSecurityInitialized)
        {
            return ConverterHealth.Missing("Image converter security policy or limits could not be applied: " + (InitializationFailureReason ?? "Initialization failed."));
        }

        try
        {
            // Forces native library load
            _ = MagickNET.Version;
            return ConverterHealth.Ready;
        }
        catch (Exception ex) when (ex is TypeInitializationException or DllNotFoundException or BadImageFormatException)
        {
            return ConverterHealth.Missing("Image library failed to load.");
        }
    }

    public async Task<ConversionResult> ConvertAsync(
        ConversionRequest request,
        IOutputWriter output,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(output);

        cancellationToken.ThrowIfCancellationRequested();

        string? temp = null;
        var committed = false;

        try
        {
            if (!IsSecurityInitialized)
            {
                return ConversionResult.Fail(ConversionError.ToolMissing, InitializationFailureReason ?? "Security policy or limits could not be applied.");
            }

            if (!File.Exists(request.SourcePath))
            {
                return ConversionResult.Fail(ConversionError.SourceMissing, "The source file was not found.");
            }

            var sourceExt = Path.GetExtension(request.SourcePath).ToLowerInvariant();
            if (!CanConvert(sourceExt, request.Target.Id))
            {
                return ConversionResult.Fail(ConversionError.Unsupported, "Cannot convert this file format.");
            }

            if (!ExtensionFormats.TryGetValue(sourceExt, out var inputFormat))
            {
                return ConversionResult.Fail(ConversionError.Unsupported, "Unsupported source format.");
            }

            if (request.Target.OutputExtension is not { } ext)
            {
                return ConversionResult.Fail(ConversionError.Unsupported, "Target has no output extension.");
            }

            temp = output.CreateTempPath(request.SourcePath, ext);

            await Task.Run(
                () => ConvertCore(request, inputFormat, temp, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(1.0);

            var final = output.Commit(temp, request.SourcePath, ext, request.OutputLabel);
            committed = true;
            return ConversionResult.Ok(final);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OutputWriteException)
        {
            return ConversionResult.Fail(ConversionError.OutputFailed, "Couldn't save the converted file here.");
        }
        catch (MagickCorruptImageErrorException)
        {
            return ConversionResult.Fail(ConversionError.CorruptInput, "The image file is damaged or unreadable.");
        }
        catch (MagickMissingDelegateErrorException)
        {
            return ConversionResult.Fail(ConversionError.Unsupported, "Image format or variant is not supported.");
        }
        catch (MagickException ex) when (ex is MagickResourceLimitErrorException or MagickCacheErrorException ||
                                        ex.Message.Contains("exceeds limit", StringComparison.OrdinalIgnoreCase))
        {
            return ConversionResult.Fail(ConversionError.Unsupported, "Image is too large.");
        }
        catch (MagickPolicyErrorException)
        {
            return ConversionResult.Fail(ConversionError.CorruptInput, "Image format is restricted or unsupported.");
        }
        catch (MagickException)
        {
            return ConversionResult.Fail(ConversionError.CorruptInput, "Failed to decode or process image.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or FileNotFoundException or DirectoryNotFoundException)
        {
            return ConversionResult.Fail(ConversionError.OutputFailed, "Couldn't save the converted file here.");
        }
        catch (Exception)
        {
            return ConversionResult.Fail(ConversionError.Unknown, "Something went wrong while converting the image.");
        }
        finally
        {
            if (temp != null && !committed)
            {
                output.Discard(temp);
            }
        }
    }

    private static void ConvertCore(
        ConversionRequest request,
        MagickFormat inputFormat,
        string tempPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isGifToWebp = inputFormat == MagickFormat.Gif && request.Target.Id == TargetCatalog.ImageWebp.Id;
        if (isGifToWebp)
        {
            // Choice: For multi-frame inputs, only animated GIF -> WebP preserves all animation frames to produce
            // animated WebP. All other conversions (e.g. GIF -> JPG/PNG/ICO and multi-page TIFF) use only the first frame.
            var readSettings = new MagickReadSettings { Format = MagickFormat.Gif };
            using var collection = new MagickImageCollection(request.SourcePath, readSettings);

            cancellationToken.ThrowIfCancellationRequested();
            var quality = ResolveQuality(request, 85);

            foreach (var frame in collection)
            {
                cancellationToken.ThrowIfCancellationRequested();
                frame.AutoOrient();
                frame.Quality = quality;
            }

            cancellationToken.ThrowIfCancellationRequested();
            WriteCollection(collection, tempPath, MagickFormat.WebP);
            return;
        }

        // For all single-frame conversions and multi-frame inputs going to JPG, PNG, or ICO:
        // Read only the first frame (FrameIndex = 0, FrameCount = 1) to conserve memory and avoid loading unused frames.
        var singleFrameSettings = new MagickReadSettings
        {
            Format = inputFormat,
            FrameIndex = 0,
            FrameCount = 1,
        };

        using var image = new MagickImage(request.SourcePath, singleFrameSettings);
        cancellationToken.ThrowIfCancellationRequested();

        // Respect EXIF rotation; keep metadata unchanged
        image.AutoOrient();
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Target.Id == TargetCatalog.ImageJpg.Id)
        {
            var quality = ResolveQuality(request, 90);
            image.Quality = quality;

            if (image.HasAlpha)
            {
                image.ColorAlpha(MagickColors.White);
            }

            WriteImage(image, tempPath, MagickFormat.Jpeg);
        }
        else if (request.Target.Id == TargetCatalog.ImagePng.Id)
        {
            WriteImage(image, tempPath, MagickFormat.Png);
        }
        else if (request.Target.Id == TargetCatalog.ImageWebp.Id)
        {
            var quality = ResolveQuality(request, 85);
            image.Quality = quality;
            WriteImage(image, tempPath, MagickFormat.WebP);
        }
        else if (request.Target.Id == TargetCatalog.ImageIco.Id)
        {
            var geometry = new MagickGeometry(256, 256) { IgnoreAspectRatio = false };
            image.Resize(geometry);
            image.Extent(256, 256, Gravity.Center, MagickColors.Transparent);
            image.Settings.SetDefine(MagickFormat.Ico, "auto-resize", "256,128,64,48,32,16");
            WriteImage(image, tempPath, MagickFormat.Ico);
        }
        else
        {
            throw new NotSupportedException($"Target '{request.Target.Id}' is not supported by ImageConverter.");
        }
    }

    private static void WriteImage(MagickImage image, string tempPath, MagickFormat format)
    {
        try
        {
            image.Write(tempPath, format);
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException)
        {
            throw new OutputWriteException("Couldn't save the converted file here.", ex);
        }
    }

    private static void WriteCollection(MagickImageCollection collection, string tempPath, MagickFormat format)
    {
        try
        {
            collection.Write(tempPath, format);
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException)
        {
            throw new OutputWriteException("Couldn't save the converted file here.", ex);
        }
    }

    private static uint ResolveQuality(ConversionRequest request, uint defaultQuality)
    {
        if (request.GetOption(OptionKeys.Quality) is { } qStr &&
            uint.TryParse(qStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var q))
        {
            return Math.Clamp(q, 1, 100);
        }

        return defaultQuality;
    }

    private static void InitializeSecurityAndLimits()
    {
        // Security policy: disable risky coders to prevent ImageTragick and unauthorized file access
        // Must be initialized before any native Magick calls (including ResourceLimits) so that
        // ImageMagick loads the policy configuration when the native runtime initializes.
        var configFiles = ConfigurationFiles.Default;
        configFiles.Policy.Data = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<policymap>
  <policy domain=""coder"" rights=""none"" pattern=""MVG"" />
  <policy domain=""coder"" rights=""none"" pattern=""MSL"" />
  <policy domain=""coder"" rights=""none"" pattern=""SVG"" />
  <policy domain=""coder"" rights=""none"" pattern=""TEXT"" />
  <policy domain=""coder"" rights=""none"" pattern=""TXT"" />
  <policy domain=""coder"" rights=""none"" pattern=""URL"" />
  <policy domain=""coder"" rights=""none"" pattern=""HTTP"" />
  <policy domain=""coder"" rights=""none"" pattern=""HTTPS"" />
  <policy domain=""coder"" rights=""none"" pattern=""FTP"" />
  <policy domain=""coder"" rights=""none"" pattern=""EPHEMERAL"" />
  <policy domain=""coder"" rights=""none"" pattern=""PS"" />
  <policy domain=""coder"" rights=""none"" pattern=""EPS"" />
  <policy domain=""coder"" rights=""none"" pattern=""PDF"" />
  <policy domain=""coder"" rights=""none"" pattern=""XPS"" />
  <policy domain=""path"" rights=""none"" pattern=""@*"" />
  <policy domain=""path"" rights=""none"" pattern=""|*"" />
</policymap>";
        MagickNET.Initialize(configFiles);

        // Safety limits: avoid decompression bombs (WP-06 requirement 6)
        ResourceLimits.Memory = 1024UL * 1024UL * 1024UL; // ~1 GB
        ResourceLimits.Width = 30000;
        ResourceLimits.Height = 30000;
    }

    private sealed class OutputWriteException(string message, Exception inner) : Exception(message, inner);
}
