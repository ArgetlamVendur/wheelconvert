using System.Runtime.Versioning;
using PDFtoImage;
using PDFtoImage.Exceptions;
using SkiaSharp;
using CorePasswordException = WheelConvert.Core.Contracts.PdfPasswordProtectedException;

namespace WheelConvert.Converters.Pdf;

/// <summary>
/// Every PDFtoImage (PDFium + SkiaSharp) call in the process goes through here. PDFium is not thread-safe, so one
/// static gate serialises all calls, shared by every <see cref="PdfPageRenderer"/> and <see cref="PdfConverter"/>.
/// Sources are opened read-only and shared, because another app may have the PDF open.
/// Errors: encrypted → <see cref="CorePasswordException"/>, unreadable → <see cref="InvalidDataException"/>.
/// Callers check <see cref="PdfiumPlatform.IsSupported"/> first.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal static class Pdfium
{
    public const int ImageDpi = 150;

    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static Task<int> GetPageCountAsync(string pdfPath, CancellationToken cancellationToken) =>
        RunAsync(
            () =>
            {
                using var pdf = OpenSource(pdfPath);
                return Conversion.GetPageCount(pdf, leaveOpen: true);
            },
            cancellationToken);

    /// <summary>PNG bytes of one page, longest side ≤ <paramref name="maxPixels"/>.</summary>
    public static Task<byte[]> RenderThumbnailPngAsync(string pdfPath, int pageIndex, int maxPixels, CancellationToken cancellationToken) =>
        RunAsync(
            () =>
            {
                using var pdf = OpenSource(pdfPath);
                var size = Conversion.GetPageSize(pdf, pageIndex, leaveOpen: true);
                pdf.Position = 0;
                var options = size.Width >= size.Height
                    ? new RenderOptions(Width: maxPixels, WithAspectRatio: true, WithAnnotations: true, WithFormFill: true)
                    : new RenderOptions(Height: maxPixels, WithAspectRatio: true, WithAnnotations: true, WithFormFill: true);
                using var rendered = Conversion.ToImage(pdf, pageIndex, leaveOpen: true, options: options);

                // The page size ignores /Rotate and rounding can add a pixel: shrink if the result is still too big.
                using var fitted = FitWithin(rendered, maxPixels);
                using var data = (fitted ?? rendered).Encode(SKEncodedImageFormat.Png, 100);
                return data.ToArray();
            },
            cancellationToken);

    /// <summary>Renders one page at <see cref="ImageDpi"/> into <paramref name="pngPath"/>, which must not exist yet.</summary>
    public static Task SavePagePngAsync(string pdfPath, int pageIndex, string pngPath, CancellationToken cancellationToken) =>
        RunAsync(
            () =>
            {
                using var pdf = OpenSource(pdfPath);
                using var png = new FileStream(pngPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                var options = new RenderOptions(Dpi: ImageDpi, WithAnnotations: true, WithFormFill: true);
                Conversion.SavePng(png, pdf, pageIndex, leaveOpen: true, options: options);
                return true;
            },
            cancellationToken);

    /// <summary>Loads PDFium and SkiaSharp by rendering the embedded 1-page sample. Throws if either native library fails.</summary>
    public static void SelfTest()
    {
        Gate.Wait();
        try
        {
            using var pdf = PdfHealthSample.Open();
            if (Conversion.GetPageCount(pdf, leaveOpen: true) != 1)
            {
                throw new InvalidDataException("Health-check PDF did not report one page.");
            }

            pdf.Position = 0;
            using var bitmap = Conversion.ToImage(pdf, 0, leaveOpen: true, options: new RenderOptions(Dpi: 10));
        }
        finally
        {
            Gate.Release();
        }
    }

    private static FileStream OpenSource(string pdfPath) =>
        new(pdfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static SKBitmap? FitWithin(SKBitmap bitmap, int maxPixels)
    {
        var longest = Math.Max(bitmap.Width, bitmap.Height);
        if (longest <= maxPixels)
        {
            return null;
        }

        var scale = (double)maxPixels / longest;
        var info = new SKImageInfo(
            Math.Max(1, (int)Math.Floor(bitmap.Width * scale)),
            Math.Max(1, (int)Math.Floor(bitmap.Height * scale)));
        return bitmap.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new InvalidDataException("Couldn't resize the page image.");
    }

    private static async Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Off the caller's thread: WP-09 calls the renderer from the UI thread.
            return await Task.Run(() => Translate(work), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static T Translate<T>(Func<T> work)
    {
        try
        {
            return work();
        }
        catch (Exception ex) when (ex is PDFtoImage.Exceptions.PdfPasswordProtectedException or PdfUnsupportedSecuritySchemeException)
        {
            throw new CorePasswordException("This PDF is password-protected.", ex);
        }
        catch (PdfPageNotFoundException ex)
        {
            throw new ArgumentOutOfRangeException("This page doesn't exist in the PDF.", ex);
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException("This PDF can't be read.", ex);
        }
    }
}

/// <summary>PDFtoImage ships PDFium and SkiaSharp natives for these platforms only.</summary>
internal static class PdfiumPlatform
{
    [SupportedOSPlatformGuard("windows")]
    [SupportedOSPlatformGuard("linux")]
    [SupportedOSPlatformGuard("macos")]
    public static bool IsSupported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
}
