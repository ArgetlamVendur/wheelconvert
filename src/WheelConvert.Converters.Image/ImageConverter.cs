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
    // Shown to the user when the native library, the security policy or the limits could not be set up.
    // The exception detail is not kept: the frozen parameterless constructor gives this class no ILog.
    private const string LibraryFailedMessage = "Image library failed to load.";

    private static readonly bool IsSecurityInitialized;

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
        catch (Exception)
        {
            // Any failure here (native load, policy, limits) must leave the converter unusable rather than
            // running without the security policy. CheckHealth() reports it as Missing.
            IsSecurityInitialized = false;
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
            return ConverterHealth.Missing(LibraryFailedMessage);
        }

        try
        {
            // Forces native library load
            _ = MagickNET.Version;
            return ConverterHealth.Ready;
        }
        catch (Exception ex) when (ex is TypeInitializationException or DllNotFoundException or BadImageFormatException)
        {
            return ConverterHealth.Missing(LibraryFailedMessage);
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
                return ConversionResult.Fail(ConversionError.ToolMissing, LibraryFailedMessage);
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
        catch (SourceReadException)
        {
            // The source could not be opened (locked by another app, no permission, or removed after the check
            // above). This is a read problem, so it must never be shown as "couldn't save".
            return File.Exists(request.SourcePath)
                ? ConversionResult.Fail(ConversionError.CorruptInput, "Couldn't read the source file. It may be open in another app.")
                : ConversionResult.Fail(ConversionError.SourceMissing, "The source file was not found.");
        }
        catch (MagickCorruptImageErrorException)
        {
            return ConversionResult.Fail(ConversionError.CorruptInput, "The image file is damaged or unreadable.");
        }
        catch (MagickMissingDelegateErrorException)
        {
            return ConversionResult.Fail(ConversionError.Unsupported, "Image format or variant is not supported.");
        }
        catch (Exception ex) when (ex is ImageTooLargeException or MagickResourceLimitErrorException or MagickCacheErrorException)
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
            // CONTRACTS C1: these come from IOutputWriter.CreateTempPath/Commit. Read-side I/O errors are turned
            // into SourceReadException in ReadSource and handled above, so they cannot land here.
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

        // Read only the header (explicit format, no sniffing) and refuse oversized images before decoding.
        // This replaces matching ImageMagick's English "exceeds limit" text, which could change on any upgrade.
        // The native Width/Height limits set at start-up stay as the backstop.
        var info = ReadSource(() => new MagickImageInfo(request.SourcePath, new MagickReadSettings { Format = inputFormat }));
        if (info.Width > ResourceLimits.Width || info.Height > ResourceLimits.Height)
        {
            throw new ImageTooLargeException();
        }

        cancellationToken.ThrowIfCancellationRequested();

        var isGifToWebp = inputFormat == MagickFormat.Gif && request.Target.Id == TargetCatalog.ImageWebp.Id;
        if (isGifToWebp)
        {
            // Choice: For multi-frame inputs, only animated GIF -> WebP preserves all animation frames to produce
            // animated WebP. All other conversions (e.g. GIF -> JPG/PNG/ICO and multi-page TIFF) use only the first frame.
            var readSettings = new MagickReadSettings { Format = MagickFormat.Gif };
            using var collection = ReadSource(() => new MagickImageCollection(request.SourcePath, readSettings));

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

        using var image = ReadSource(() => new MagickImage(request.SourcePath, singleFrameSettings));
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

    private static T ReadSource<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is MagickBlobErrorException or IOException or UnauthorizedAccessException)
        {
            // Opening the source failed (a locked file raises MagickBlobErrorException "Permission denied").
            // Decoder errors (corrupt data, limits, policy) are other types and reach the ConvertAsync handlers.
            throw new SourceReadException(ex);
        }
    }

    private static void WriteImage(MagickImage image, string tempPath, MagickFormat format)
    {
        try
        {
            image.Write(tempPath, format);
        }
        catch (Exception ex) when (ex is MagickBlobErrorException or IOException or UnauthorizedAccessException)
        {
            // Only file-system failures mean "couldn't save here" (missing folder, read-only, file in use all
            // raise MagickBlobErrorException). Encoder errors (limits, unsupported variants) reach the
            // ConvertAsync handlers so the user sees the real reason.
            throw new OutputWriteException("Couldn't save the converted file here.", ex);
        }
    }

    private static void WriteCollection(MagickImageCollection collection, string tempPath, MagickFormat format)
    {
        try
        {
            collection.Write(tempPath, format);
        }
        catch (Exception ex) when (ex is MagickBlobErrorException or IOException or UnauthorizedAccessException)
        {
            // Only file-system failures mean "couldn't save here" (missing folder, read-only, file in use all
            // raise MagickBlobErrorException). Encoder errors (limits, unsupported variants) reach the
            // ConvertAsync handlers so the user sees the real reason.
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
        // Security policy: disable risky coders to prevent ImageTragick and unauthorized file access.
        // Deviation from the WP-06 spec, which names MagickNET.SetSecurityPolicy: we pass the policy XML through
        // ConfigurationFiles + MagickNET.Initialize instead. Both load a policy.xml; Initialize also replaces the
        // package's other default configuration files in one call, and must run before any other native call
        // (including ResourceLimits) so the policy is in force from the first image operation.
        //
        // Two rules are kept as defence in depth but cannot be exercised in Magick.NET 15.0.0 (probed 2026-11-10):
        // - URL: policy matches coder names, and the URL module registers its coders as HTTP, HTTPS and, in the
        //   Windows build only, FTP (denied separately and tested); there is no coder named "URL", so deleting
        //   this rule changes nothing. The Linux build registers no FTP coder, so the FTP rule is inert there.
        // - EPHEMERAL: this build registers no EPHEMERAL coder; "ephemeral:x.png" is read as a plain PNG and the
        //   file is not deleted, with or without the rule. The old test passed with the rule deleted because its
        //   input was MVG, which the MVG rule blocked.
        // Tests SecurityPolicy_UrlRule_IsInert / _EphemeralRule_IsInert fail if an upgrade makes them reachable.
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

    private sealed class SourceReadException(Exception inner) : Exception("The source file could not be read.", inner);

    private sealed class ImageTooLargeException : Exception;
}
