using ImageMagick;
using ImageMagick.Configuration;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using ZXing;
using ZXing.Common;

namespace WheelConvert.Converters.Qr;

/// <summary>
/// Reads QR codes from image files with Magick.NET (loading) and ZXing.Net (decoding) (WP-10, ADR-009).
/// The decoded text is untrusted: it is only returned, never opened, launched or passed to a shell.
/// No file is ever written; <c>output</c> is unused.
/// </summary>
public sealed class QrConverter : IConverter
{
    internal const int MaxTextLength = 4000;

    private const string LibraryFailedMessage = "Image library failed to load.";

    private static readonly bool IsSecurityInitialized;

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

    static QrConverter()
    {
        try
        {
            InitializeSecurityAndLimits();
            IsSecurityInitialized = true;
        }
        catch (Exception)
        {
            // Never decode without the security policy; CheckHealth() reports this as Missing.
            IsSecurityInitialized = false;
        }
    }

    public string Id => "qr";

    public IReadOnlyCollection<string> SupportedTargetIds { get; } = [TargetCatalog.ImageReadQr.Id];

    public bool CanConvert(string sourceExtension, string targetId) =>
        IsSecurityInitialized &&
        targetId == TargetCatalog.ImageReadQr.Id &&
        FileClassifier.Classify(sourceExtension) == FileKind.Image &&
        ExtensionFormats.ContainsKey(sourceExtension);

    public ConverterHealth CheckHealth()
    {
        if (!IsSecurityInitialized)
        {
            return ConverterHealth.Missing(LibraryFailedMessage);
        }

        try
        {
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

            // Explicit format from the extension; the content is never sniffed (WP-10 req. 2).
            var sourceExt = Path.GetExtension(request.SourcePath).ToLowerInvariant();
            if (!CanConvert(sourceExt, request.Target.Id) || !ExtensionFormats.TryGetValue(sourceExt, out var format))
            {
                return ConversionResult.Fail(ConversionError.Unsupported, "Cannot read QR codes in this file format.");
            }

            var text = await Task.Run(() => ReadCodes(request.SourcePath, format, cancellationToken), cancellationToken)
                .ConfigureAwait(false);

            progress?.Report(1.0);

            return text is null
                ? ConversionResult.Fail(ConversionError.NoResult, "No QR code found.")
                : ConversionResult.OkText(text);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SourceReadException)
        {
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
        catch (Exception)
        {
            return ConversionResult.Fail(ConversionError.Unknown, "Something went wrong while reading the QR code.");
        }
    }

    private static string? ReadCodes(string path, MagickFormat format, CancellationToken cancellationToken)
    {
        // Header first: refuse oversized images before decoding (same approach as WP-06).
        var info = ReadSource(() => new MagickImageInfo(path, new MagickReadSettings { Format = format }));
        if (info.Width > ResourceLimits.Width || info.Height > ResourceLimits.Height)
        {
            throw new ImageTooLargeException();
        }

        cancellationToken.ThrowIfCancellationRequested();

        // First frame only; the source file is opened read-only by Magick and never written.
        var settings = new MagickReadSettings { Format = format, FrameIndex = 0, FrameCount = 1 };
        using var image = ReadSource(() => new MagickImage(path, settings));
        cancellationToken.ThrowIfCancellationRequested();

        image.AutoOrient();

        // 1-bit PNGs would be resized without smoothing, which breaks the 2x retry on small codes.
        image.Depth = 8;
        image.ColorSpace = ColorSpace.sRGB;
        if (image.HasAlpha)
        {
            // Transparent pixels would otherwise decode as black and hide the code.
            image.ColorAlpha(MagickColors.White);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var results = Decode(image);
        if (results.Count == 0)
        {
            // Small codes in big screenshots: retry once on a 2x copy.
            cancellationToken.ThrowIfCancellationRequested();
            var w = image.Width * 2;
            var h = image.Height * 2;
            if (w <= ResourceLimits.Width && h <= ResourceLimits.Height)
            {
                image.FilterType = FilterType.Point; // keeps module edges crisp; smoothing blurs 1 px modules
                image.Resize(w, h);
                cancellationToken.ThrowIfCancellationRequested();
                results = Decode(image);
            }
        }

        if (results.Count == 0)
        {
            return null;
        }

        return Truncate(string.Join("\n", Order(results).Select(r => r.Text)));
    }

    private static List<Result> Decode(MagickImage image)
    {
        var width = (int)image.Width;
        var height = (int)image.Height;
        using var pixels = image.GetPixels();
        var bytes = pixels.ToByteArray("I")
            ?? throw new MagickCorruptImageErrorException("No pixel data.");

        var source = new PlanarYUVLuminanceSource(bytes, width, height, 0, 0, width, height, false);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                PossibleFormats = [BarcodeFormat.QR_CODE],
                TryHarder = true,
                TryInverted = true,
            },
        };

        // DecodeMultiple does not honour TryInverted, so white-on-black codes are tried explicitly.
        var found = reader.DecodeMultiple(source) ?? reader.DecodeMultiple(source.invert());
        return found is null ? [] : found.Where(r => !string.IsNullOrEmpty(r.Text)).ToList();
    }

    // Reading order: rows top to bottom, left to right inside a row. Codes whose tops are closer than
    // half a code height count as the same row, so a slightly tilted pair is not swapped.
    private static IEnumerable<Result> Order(List<Result> results)
    {
        var boxes = results
            .Select(r =>
            {
                var pts = r.ResultPoints ?? [];
                return pts.Length == 0
                    ? (Result: r, Top: 0f, Left: 0f, Size: 1f)
                    : (Result: r, Top: pts.Min(p => p.Y), Left: pts.Min(p => p.X), Size: Math.Max(1f, pts.Max(p => p.Y) - pts.Min(p => p.Y)));
            })
            .OrderBy(b => b.Top)
            .ToList();

        var rows = new List<List<(Result Result, float Top, float Left, float Size)>>();
        foreach (var box in boxes)
        {
            var row = rows.Count > 0 ? rows[^1] : null;
            if (row is not null && Math.Abs(box.Top - row[0].Top) < row[0].Size / 2)
            {
                row.Add(box);
            }
            else
            {
                rows.Add([box]);
            }
        }

        return rows.SelectMany(row => row.OrderBy(b => b.Left)).Select(b => b.Result);
    }

    private static string Truncate(string text)
    {
        if (text.Length <= MaxTextLength)
        {
            return text;
        }

        var cut = MaxTextLength - 1;
        if (char.IsHighSurrogate(text[cut - 1]))
        {
            cut--; // do not split a surrogate pair
        }

        return text[..cut] + "…";
    }

    private static T ReadSource<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is MagickBlobErrorException or IOException or UnauthorizedAccessException)
        {
            throw new SourceReadException(ex);
        }
    }

    private static void InitializeSecurityAndLimits()
    {
        // Same policy and limits as the image converter (WP-06 req. 4 and 6). Duplicated on purpose: the
        // converters must not reference each other. Whichever converter loads first initialises Magick.NET.
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

        ResourceLimits.Memory = 1024UL * 1024UL * 1024UL;
        ResourceLimits.Width = 30000;
        ResourceLimits.Height = 30000;
    }

    private sealed class SourceReadException(Exception inner) : Exception("The source file could not be read.", inner);

    private sealed class ImageTooLargeException : Exception;
}
