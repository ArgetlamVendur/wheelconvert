using ImageMagick;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Infrastructure;
using WheelConvert.Core.Output;

namespace WheelConvert.Converters.Image;

/// <summary>
/// WP-15: one or more images become one PDF, one image per A4 page. Each image is decoded, drawn and disposed
/// before the next is read, so 40 phone photos never sit in memory at once.
/// </summary>
internal static class ImagesToPdf
{
    // A4 in points, the margin (about 6 mm) and the largest resolution worth embedding.
    private const double PageShort = 595;
    private const double PageLong = 842;
    private const double Margin = 18;
    private const double MaxDpi = 300;
    private const uint JpegQuality = 85;

    // Source formats that are photographs: re-encoded as JPEG. Everything else is a graphic and stays lossless.
    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.Ordinal)
    {
        ".jpg", ".jpeg", ".heic", ".heif", ".webp", ".avif",
    };

    public static async Task<ConversionResult> ConvertAsync(
        ConversionRequest request,
        IOutputWriter output,
        IProgress<double>? progress,
        ILog log,
        CancellationToken cancellationToken)
    {
        string? temp = null;
        var committed = false;
        try
        {
            var paths = request.AllSourcePaths;
            foreach (var path in paths)
            {
                if (!File.Exists(path))
                {
                    return ConversionResult.Fail(ConversionError.SourceMissing, "The source file was not found.");
                }

                if (!ImageConverter.IsSupportedInput(Path.GetExtension(path).ToLowerInvariant()))
                {
                    return ConversionResult.Fail(ConversionError.Unsupported, "Cannot convert this file format.");
                }
            }

            temp = output.CreateTempPath(request.SourcePath, ".pdf");
            var tempPath = temp;

            await Task.Run(() => Build(paths, tempPath, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(1.0);

            var label = paths.Count > 1 ? OutputLabels.Combined : request.OutputLabel;
            var final = output.Commit(temp, request.SourcePath, ".pdf", label);
            committed = true;
            return ConversionResult.Ok(final);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ImageFailure failure)
        {
            if (failure.Cause is not null)
            {
                LogFailure(log, failure.Cause, failure.FileName);
            }

            return ConversionResult.Fail(failure.Error, failure.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Raised by CreateTempPath, the PDF save or Commit: every image was already read successfully.
            return ConversionResult.Fail(ConversionError.OutputFailed, "Couldn't save the converted file here.");
        }
        catch (Exception ex)
        {
            LogFailure(log, ex, Path.GetFileName(request.SourcePath));
            return ConversionResult.Fail(ConversionError.Unknown, "Something went wrong while converting the images.");
        }
        finally
        {
            if (temp != null && !committed)
            {
                output.Discard(temp);
            }
        }
    }

    private static void Build(IReadOnlyList<string> paths, string tempPath, IProgress<double>? progress, CancellationToken ct)
    {
        using var document = new PdfDocument();
        for (var i = 0; i < paths.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            AddPage(document, paths[i]);
            progress?.Report(0.95 * (i + 1) / paths.Count);
        }

        ct.ThrowIfCancellationRequested();
        document.Save(tempPath);
    }

    private static void AddPage(PdfDocument document, string path)
    {
        var name = Path.GetFileName(path);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var (bytes, width, height) = Prepare(path, name, ext);

        var landscape = width > height;
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(landscape ? PageLong : PageShort);
        page.Height = XUnit.FromPoint(landscape ? PageShort : PageLong);

        var boxW = page.Width.Point - (2 * Margin);
        var boxH = page.Height.Point - (2 * Margin);
        var scale = Math.Min(boxW / width, boxH / height);
        var w = width * scale;
        var h = height * scale;

        using var stream = new MemoryStream(bytes, writable: false);
        using var xImage = XImage.FromStream(stream);
        using var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(xImage, (page.Width.Point - w) / 2, (page.Height.Point - h) / 2, w, h);
    }

    /// <summary>Decodes one image and returns the bytes to embed plus the upright pixel size. The Magick image is disposed here.</summary>
    private static (byte[] Bytes, uint Width, uint Height) Prepare(string path, string name, string ext)
    {
        try
        {
            var format = ImageConverter.FormatFor(ext);
            var info = new MagickImageInfo(path, new MagickReadSettings { Format = format });
            if (info.Width > ResourceLimits.Width || info.Height > ResourceLimits.Height)
            {
                throw new ImageFailure(ConversionError.Unsupported, $"{name} is too large.", null, name);
            }

            // First frame only: animated GIFs and multi-page TIFFs contribute one page.
            using var image = new MagickImage(path, new MagickReadSettings { Format = format, FrameIndex = 0, FrameCount = 1 });
            image.AutoOrient();

            if (image.HasAlpha)
            {
                image.ColorAlpha(MagickColors.White);
                image.HasAlpha = false;
            }

            if (image.ColorSpace == ColorSpace.CMYK)
            {
                image.ColorSpace = ColorSpace.sRGB;
            }

            // Largest size that is still useful: 300 DPI at the size the image is placed on the page.
            var landscape = image.Width > image.Height;
            var boxW = (landscape ? PageLong : PageShort) - (2 * Margin);
            var boxH = (landscape ? PageShort : PageLong) - (2 * Margin);
            var placedW = image.Width * Math.Min(boxW / image.Width, boxH / image.Height);
            var maxW = (uint)Math.Ceiling(placedW / 72 * MaxDpi);
            if (image.Width > maxW)
            {
                image.Resize(maxW, 0);
            }

            // Privacy: no EXIF, GPS or colour profile travels into the PDF.
            image.Strip();
            image.Depth = 8;

            using var buffer = new MemoryStream();
            if (PhotoExtensions.Contains(ext))
            {
                image.Quality = JpegQuality;
                image.Settings.Interlace = Interlace.NoInterlace;
                image.Write(buffer, MagickFormat.Jpeg);
            }
            else
            {
                // Colour type 2 = plain RGB, which PDFsharp's PNG reader always handles.
                image.Settings.SetDefine(MagickFormat.Png, "color-type", "2");
                image.Write(buffer, MagickFormat.Png);
            }

            return (buffer.ToArray(), image.Width, image.Height);
        }
        catch (ImageFailure)
        {
            throw;
        }
        catch (MagickResourceLimitErrorException ex)
        {
            throw new ImageFailure(ConversionError.Unsupported, $"{name} is too large.", ex, name);
        }
        catch (Exception ex) when (ex is MagickException or IOException or UnauthorizedAccessException)
        {
            throw new ImageFailure(ConversionError.CorruptInput, $"Couldn't read {name}.", ex, name);
        }
    }

    private static void LogFailure(ILog log, Exception ex, string fileName)
    {
        try
        {
            var text = $"Images to PDF failed for {fileName}: {ex.GetType().Name}: {ex.Message}";
            log.Warn(text.Length <= 500 ? text : text[..500]);
        }
        catch (Exception logEx) when (logEx is not OutOfMemoryException)
        {
            // Logging must never change the conversion result.
        }
    }

    private sealed class ImageFailure(ConversionError error, string message, Exception? cause, string fileName) : Exception(message, cause)
    {
        public ConversionError Error { get; } = error;

        public string FileName { get; } = fileName;

        public Exception? Cause { get; } = cause;
    }
}
