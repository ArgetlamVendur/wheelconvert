using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;

namespace WheelConvert.Converters.Docx;

/// <summary>
/// <c>pdf.to-docx</c> (docs/modules/WP-18-pdf-to-word.md): the PDF's text as paragraphs and headings in a .docx.
/// The layout (columns, tables, images, fonts) is not reproduced.
/// </summary>
public sealed class DocxConverter : IConverter
{
    private const string CorruptMessage = "This PDF can't be read. It may be damaged.";

    public string Id => "docx";

    public IReadOnlyCollection<string> SupportedTargetIds { get; } = [TargetCatalog.PdfToDocx.Id];

    public bool CanConvert(string sourceExtension, string targetId) =>
        sourceExtension == ".pdf" && targetId == TargetCatalog.PdfToDocx.Id;

    public ConverterHealth CheckHealth()
    {
        try
        {
            // Both libraries are managed: resolving a type from each proves the assemblies load. No user file is touched.
            _ = typeof(PdfDocument).Assembly.GetName();
            _ = typeof(WordprocessingDocument).Assembly.GetName();
            return ConverterHealth.Ready;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ConverterHealth.Missing("The Word export library failed to load.");
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

        if (!File.Exists(request.SourcePath))
        {
            return SourceMissing();
        }

        try
        {
            return await Task.Run(() => Convert(request, output, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return SourceMissing();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ConversionResult.Fail(ConversionError.OutputFailed, "Couldn't save the file here.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ConversionResult.Fail(ConversionError.Unknown, "Something went wrong while working on this PDF.");
        }
    }

    private static ConversionResult SourceMissing() =>
        ConversionResult.Fail(ConversionError.SourceMissing, "The file is gone. It may have been moved or deleted.");

    private static ConversionResult Convert(
        ConversionRequest request,
        IOutputWriter output,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var pages = new List<PageText>();
        var failure = ReadPages(request.SourcePath, pages, progress, ct);
        if (failure is not null)
        {
            return failure;
        }

        var reject = TextRules.RejectReason(
            pages.Sum(p => (long)p.LetterCount), pages.Sum(p => (long)p.CharCount), pages.Sum(p => (long)p.JunkCount), pages.Count);
        if (reject is not null)
        {
            return ConversionResult.Fail(ConversionError.NoResult, reject);
        }

        ct.ThrowIfCancellationRequested();

        const string ext = ".docx";
        var temp = output.CreateTempPath(request.SourcePath, ext);
        var committed = false;
        try
        {
            DocxWriter.Write(temp, pages);
            ct.ThrowIfCancellationRequested();
            var final = output.Commit(temp, request.SourcePath, ext, request.OutputLabel);
            committed = true;
            progress?.Report(1);
            return ConversionResult.Ok(final);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ConversionResult.Fail(ConversionError.OutputFailed, "Couldn't save the file here.");
        }
        finally
        {
            if (!committed)
            {
                output.Discard(temp);
            }
        }
    }

    /// <summary>Reads every page into <paramref name="pages"/>, or returns the failure to report. The source is opened read-only (S1).</summary>
    private static ConversionResult? ReadPages(string path, List<PageText> pages, IProgress<double>? progress, CancellationToken ct)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        PdfDocument document;
        try
        {
            // No password is ever tried: an encrypted PDF is reported, not unlocked.
            document = PdfDocument.Open(stream, new ParsingOptions { SkipMissingFonts = true });
        }
        catch (PdfDocumentEncryptedException)
        {
            return ConversionResult.Fail(ConversionError.PasswordProtected, "This PDF is password-protected.");
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            return ConversionResult.Fail(ConversionError.CorruptInput, CorruptMessage);
        }

        using (document)
        {
            try
            {
                var count = document.NumberOfPages;
                if (count == 0)
                {
                    return ConversionResult.Fail(ConversionError.CorruptInput, "This PDF has no pages.");
                }

                for (var number = 1; number <= count; number++)
                {
                    ct.ThrowIfCancellationRequested();
                    pages.Add(PdfTextExtractor.ExtractPage(document.GetPage(number)));
                    progress?.Report(0.9 * number / count);
                }

                return null;
            }
            catch (PdfDocumentEncryptedException)
            {
                return ConversionResult.Fail(ConversionError.PasswordProtected, "This PDF is password-protected.");
            }
            catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException or IOException))
            {
                return ConversionResult.Fail(ConversionError.CorruptInput, CorruptMessage);
            }
        }
    }
}
