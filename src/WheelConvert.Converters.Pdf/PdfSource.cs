using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using WheelConvert.Core.Contracts;

namespace WheelConvert.Converters.Pdf;

/// <summary>
/// A source PDF opened with PDFsharp in <see cref="PdfDocumentOpenMode.Import"/> mode (read-only, CONTRACTS C2 S1).
/// Keeps the read-only stream open for the document's lifetime.
/// </summary>
internal sealed class PdfSource : IDisposable
{
    private readonly FileStream _stream;

    private PdfSource(FileStream stream, PdfDocument document)
    {
        _stream = stream;
        Document = document;
    }

    public PdfDocument Document { get; }

    /// <summary>Opens <paramref name="path"/>, or returns the failure to report.</summary>
    /// <exception cref="FileNotFoundException">The source vanished.</exception>
    /// <exception cref="DirectoryNotFoundException">The source's folder vanished.</exception>
    /// <exception cref="IOException">Another app holds the file exclusively.</exception>
    public static PdfSource? TryOpen(string path, out ConversionResult? failure)
    {
        failure = null;
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var askedForPassword = false;
        try
        {
            // Never prompt: an encrypted PDF is reported, not unlocked. The callback tells "encrypted" apart from "corrupt"
            // without parsing PDFsharp's exception messages.
            var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import, args =>
            {
                askedForPassword = true;
                args.Abort = true;
            });

            // PDFsharp 6.2 returns null (no exception) when the password prompt is aborted, despite the non-null annotation.
            if (askedForPassword || document is null)
            {
                document?.Dispose();
                stream.Dispose();
                failure = ConversionResult.Fail(ConversionError.PasswordProtected, "This PDF is password-protected.");
                return null;
            }

            return new PdfSource(stream, document);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            stream.Dispose();
            failure = askedForPassword
                ? ConversionResult.Fail(ConversionError.PasswordProtected, "This PDF is password-protected.")
                : ConversionResult.Fail(ConversionError.CorruptInput, "This PDF can't be read. It may be damaged.");
            return null;
        }
    }

    public void Dispose()
    {
        Document.Dispose();
        _stream.Dispose();
    }
}
