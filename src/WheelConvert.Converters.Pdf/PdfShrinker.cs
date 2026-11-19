using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using SkiaSharp;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Output;

namespace WheelConvert.Converters.Pdf;

/// <summary>
/// <c>pdf.smaller</c> (docs/modules/WP-20-pdf-smaller.md): recompresses the JPEG photos and scans inside a PDF.
/// The source is only copied (read-only) into temp A; A is edited and saved to temp B; only B is committed.
/// Both temps are always discarded on every failure path.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
[System.Runtime.Versioning.SupportedOSPlatform("macos")]
internal sealed class PdfShrinker(ILog log, Func<string, CancellationToken, Task>? renderCheck = null)
{
    internal const int MaxLongEdge = 2000;
    internal const int JpegQuality = 70;
    private const double RequiredSaving = 0.10;
    private const string NoResultMessage = "Couldn't make this PDF smaller.";
    private const string FormatMessage = "Couldn't make this PDF smaller: its images use a format this app can't shrink yet.";

    /// <summary>What the last image pass did; tests read it.</summary>
    internal sealed record Stats(int Replaced, int Skipped, int Eligible);

    internal Stats? LastStats { get; private set; }

    public async Task<ConversionResult> ShrinkAsync(
        ConversionRequest request,
        IOutputWriter output,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        string? tempA = null;
        string? tempB = null;
        var committed = false;
        try
        {
            tempA = output.CreateTempPath(request.SourcePath, ".pdf");
            tempB = output.CreateTempPath(request.SourcePath, ".pdf");

            // The source is opened read-only and shared: another app may have it open.
            FileStream from;
            try
            {
                from = new FileStream(request.SourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return ConversionResult.Fail(ConversionError.SourceMissing, "The file is no longer there.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return ConversionResult.Fail(ConversionError.Unknown, "Couldn't open this PDF. Another app may be using it.");
            }

            long sourceLength;
            using (from)
            {
                sourceLength = from.Length;

                // CreateTempPath only picks a name, so CreateNew fails loudly if a temp name were ever reused.
                using var to = new FileStream(tempA, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await from.CopyToAsync(to, cancellationToken).ConfigureAwait(false);
            }

            ConversionResult? failure;
            Stats stats;
            using (var document = OpenForEdit(tempA, out failure))
            {
                if (document is null)
                {
                    return failure!; // OpenForEdit sets the failure whenever it returns null.
                }

                if (document.PageCount == 0)
                {
                    return ConversionResult.Fail(ConversionError.CorruptInput, "This PDF has no pages.");
                }

                stats = ShrinkImages(document, progress, cancellationToken);
                LastStats = stats;
                document.Options.CompressContentStreams = true;

                // PDFsharp only compresses streams it has wrapped as page contents, and a loaded page wraps them lazily.
                for (var p = 0; p < document.PageCount; p++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _ = document.Pages[p].Contents.Elements.Count;
                }

                using var stream = new FileStream(tempB, FileMode.CreateNew,FileAccess.Write, FileShare.None);
                document.Save(stream);
            }

            var newLength = new FileInfo(tempB).Length;
            if (newLength > sourceLength * (1 - RequiredSaving))
            {
                return stats.Eligible == 0 && stats.Skipped > 0
                    ? ConversionResult.Fail(ConversionError.NoResult, FormatMessage)
                    : ConversionResult.Fail(ConversionError.NoResult, NoResultMessage);
            }

            try
            {
                // Page 1 must still render, or the output is not handed to the user.
                await (renderCheck?.Invoke(tempB, cancellationToken)
                    ?? Pdfium.RenderThumbnailPngAsync(tempB, 0, 32, cancellationToken)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
            {
                LogFailure(ex, request.SourcePath, [tempA, tempB]);
                return ConversionResult.Fail(ConversionError.Unknown, NoResultMessage);
            }

            var final = output.Commit(tempB, request.SourcePath, ".pdf", OutputLabels.Smaller);
            committed = true;
            return ConversionResult.Ok([final]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ConversionResult.Fail(ConversionError.OutputFailed, "Couldn't save the file here.");
        }
        finally
        {
            if (tempA is not null)
            {
                output.Discard(tempA);
            }

            if (tempB is not null && !committed)
            {
                output.Discard(tempB);
            }
        }
    }

    /// <summary>
    /// ADR-013: the log is shared, so it gets the exception type and message only (no stack trace), with every known path
    /// reduced to its file name and the text cut short. PDFium puts full temp paths in its messages.
    /// </summary>
    private void LogFailure(Exception ex, string sourcePath, IEnumerable<string> tempPaths)
    {
        try
        {
            var message = ex.Message;
            foreach (var path in tempPaths.Append(sourcePath).Select(Path.GetFullPath).OrderByDescending(p => p.Length))
            {
                var name = Path.GetFileName(path);
                message = message.Replace(path, name, StringComparison.OrdinalIgnoreCase)
                    .Replace(path.Replace(Path.DirectorySeparatorChar, '/'), name, StringComparison.OrdinalIgnoreCase)
                    .Replace(path.Replace(Path.DirectorySeparatorChar, '\\'), name, StringComparison.OrdinalIgnoreCase);
            }

            var text = $"Smaller PDF failed to render for {Path.GetFileName(sourcePath)}: {ex.GetType().Name}: {message}";
            log.Error(text.Length <= 500 ? text : text[..500]);
        }
        catch (Exception logEx) when (logEx is not OutOfMemoryException)
        {
            // Logging must never change the conversion result.
        }
    }

    private static PdfDocument? OpenForEdit(string path, out ConversionResult? failure)
    {
        failure = null;
        var askedForPassword = false;
        try
        {
            var document = PdfReader.Open(path, PdfDocumentOpenMode.Modify, args =>
            {
                askedForPassword = true;
                args.Abort = true;
            });

            // PDFsharp 6.2 returns null (no exception) when the password prompt is aborted.
            if (askedForPassword || document is null)
            {
                document?.Dispose();
                failure = ConversionResult.Fail(ConversionError.PasswordProtected, "This PDF is password-protected.");
                return null;
            }

            return document;
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException or IOException))
        {
            failure = askedForPassword
                ? ConversionResult.Fail(ConversionError.PasswordProtected, "This PDF is password-protected.")
                : ConversionResult.Fail(ConversionError.CorruptInput, "This PDF can't be read. It may be damaged.");
            return null;
        }
    }

    private static Stats ShrinkImages(PdfDocument document, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var images = new List<PdfDictionary>();
        var maskIds = new HashSet<int>();
        foreach (var obj in document.Internals.GetAllObjects())
        {
            if (obj is not PdfDictionary dict)
            {
                continue;
            }

            if (dict.Stream is not null && Name(dict, "/Subtype") == "/Image")
            {
                images.Add(dict);
            }

            // An image used as a mask of another image stays as it is: the two must keep matching.
            foreach (var key in new[] { "/SMask", "/Mask" })
            {
                if (dict.Elements[key] is PdfReference reference)
                {
                    maskIds.Add(reference.ObjectNumber);
                }
            }
        }

        // Each indirect object appears once, so an image drawn on many pages is replaced once.
        var ordered = images.OrderByDescending(d => d.Stream.Length).ToList();
        int replaced = 0, skipped = 0, eligible = 0;
        for (var i = 0; i < ordered.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dict = ordered[i];
            var isMask = dict.Reference is not null && maskIds.Contains(dict.Reference.ObjectNumber);
            if (!IsShrinkable(dict, isMask, out var gray, out var info))
            {
                skipped++;
            }
            else
            {
                eligible++;
                if (TryRecompress(dict, gray, info))
                {
                    replaced++;
                }
            }

            progress?.Report((i + 1) / (double)ordered.Count);
        }

        return new Stats(replaced, skipped, eligible);
    }

    private static bool IsShrinkable(PdfDictionary dict, bool isMask, out bool gray, out JpegInfo info)
    {
        gray = false;
        info = default;
        if (isMask
            || dict.Elements.ContainsKey("/SMask") || dict.Elements.ContainsKey("/Mask")
            || dict.Elements.ContainsKey("/Decode") || dict.Elements.ContainsKey("/SMaskInData")
            || dict.Elements.ContainsKey("/ImageMask"))
        {
            return false;
        }

        if (!IsSingleFilter(dict, "/DCTDecode") || dict.Elements.GetInteger("/BitsPerComponent") != 8)
        {
            return false;
        }

        var colorSpace = Name(dict, "/ColorSpace");
        if (colorSpace is not ("/DeviceRGB" or "/DeviceGray"))
        {
            return false;
        }

        gray = colorSpace == "/DeviceGray";
        var header = JpegInfo.TryRead(dict.Stream.Value);
        if (header is null)
        {
            return false;
        }

        info = header.Value;
        return info.Precision == 8
            && info.Components == (gray ? 1 : 3)
            && info.Width == dict.Elements.GetInteger("/Width")
            && info.Height == dict.Elements.GetInteger("/Height")
            && info.Width > 0 && info.Height > 0;
    }

    private static bool TryRecompress(PdfDictionary dict, bool gray, JpegInfo info)
    {
        try
        {
            var original = dict.Stream.Value;
            var colorType = gray ? SKColorType.Gray8 : SKColorType.Rgba8888;
            using var codec = SKCodec.Create(new SKMemoryStream(original));
            if (codec is null || codec.Info.Width != info.Width || codec.Info.Height != info.Height)
            {
                return false;
            }

            using var decoded = new SKBitmap(new SKImageInfo(info.Width, info.Height, colorType, SKAlphaType.Opaque));
            if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
            {
                return false;
            }

            var longEdge = Math.Max(info.Width, info.Height);
            SKBitmap? resized = null;
            try
            {
                var bitmap = decoded;
                if (longEdge > MaxLongEdge)
                {
                    var scale = (double)MaxLongEdge / longEdge;
                    var target = new SKImageInfo(
                        Math.Max(1, (int)Math.Round(info.Width * scale)),
                        Math.Max(1, (int)Math.Round(info.Height * scale)),
                        colorType,
                        SKAlphaType.Opaque);
                    resized = decoded.Resize(target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
                    if (resized is null)
                    {
                        return false;
                    }

                    bitmap = resized;
                }

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
                if (data is null || data.Size >= original.Length)
                {
                    return false;
                }

                var bytes = data.ToArray();
                var check = JpegInfo.TryRead(bytes);
                if (check is null || check.Value.Components != (gray ? 1 : 3) || check.Value.Precision != 8)
                {
                    return false;
                }

                dict.Stream.Value = bytes;
                dict.Elements.SetInteger("/Width", bitmap.Width);
                dict.Elements.SetInteger("/Height", bitmap.Height);
                dict.Elements.SetInteger("/Length", bytes.Length);
                return true;
            }
            finally
            {
                resized?.Dispose();
            }
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            return false; // an image that can't be decoded or encoded is left exactly as it was
        }
    }

    private static string? Name(PdfDictionary dict, string key) => Resolve(dict.Elements[key]) switch
    {
        PdfName name => name.Value,
        _ => null,
    };

    private static bool IsSingleFilter(PdfDictionary dict, string filter) => Resolve(dict.Elements["/Filter"]) switch
    {
        PdfName name => name.Value == filter,
        PdfArray { Elements.Count: 1 } array => Resolve(array.Elements[0]) is PdfName inner && inner.Value == filter,
        _ => false,
    };

    private static PdfItem? Resolve(PdfItem? item) => item is PdfReference reference ? reference.Value : item;
}
