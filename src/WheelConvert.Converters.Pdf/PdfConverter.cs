using System.Runtime.Versioning;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Output;

namespace WheelConvert.Converters.Pdf;

/// <summary>
/// All <c>pdf.*</c> targets (docs/modules/WP-08-pdf-operations.md). Page operations import pages into new documents
/// with PDFsharp; <c>pdf.to-images</c> renders with PDFium via <see cref="Pdfium"/>.
/// </summary>
public sealed class PdfConverter : IConverter
{
    private const string OutputFailedMessage = "Couldn't save the file here.";
    private const string CorruptMessage = "This PDF can't be read. It may be damaged.";
    private const string PasswordMessage = "This PDF is password-protected.";

    // Only success is cached: native libraries load once per process, and a failure should be retried on "Check again".
    private static volatile bool _healthy;

    public string Id => "pdf";

    public IReadOnlyCollection<string> SupportedTargetIds { get; } =
    [
        TargetCatalog.PdfSplitPages.Id, TargetCatalog.PdfToImages.Id, TargetCatalog.PdfRotate90.Id,
        TargetCatalog.PdfReorder.Id, TargetCatalog.PdfSplitHalf.Id, TargetCatalog.PdfExtractOdd.Id,
        TargetCatalog.PdfExtractEven.Id, TargetCatalog.PdfReverse.Id,
    ];

    public bool CanConvert(string sourceExtension, string targetId) =>
        sourceExtension == ".pdf" && SupportedTargetIds.Contains(targetId);

    public ConverterHealth CheckHealth()
    {
        if (_healthy)
        {
            return ConverterHealth.Ready;
        }

        if (!PdfiumPlatform.IsSupported)
        {
            return ConverterHealth.Missing("PDF library failed to load.");
        }

        try
        {
            using (var sample = PdfHealthSample.Open())
            using (var document = PdfReader.Open(sample, PdfDocumentOpenMode.Import))
            {
                if (document.PageCount != 1)
                {
                    return ConverterHealth.Missing("PDF library failed to load.");
                }
            }

            Pdfium.SelfTest();
            _healthy = true;
            return ConverterHealth.Ready;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // DllNotFoundException, BadImageFormatException, TypeInitializationException…: any failure means "not usable".
            return ConverterHealth.Missing("PDF library failed to load.");
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
            if (request.Target.Id == TargetCatalog.PdfToImages.Id)
            {
                return PdfiumPlatform.IsSupported
                    ? await ToImagesAsync(request, output, progress, cancellationToken).ConfigureAwait(false)
                    : ConversionResult.Fail(ConversionError.Unsupported, "PDF to images isn't available on this system.");
            }

            return await Task.Run(() => EditPagesAsync(request, output, progress, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
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
            return ConversionResult.Fail(ConversionError.Unknown, "Couldn't open this PDF. Another app may be using it.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ConversionResult.Fail(ConversionError.Unknown, "Something went wrong while working on this PDF.");
        }
    }

    private static async Task<ConversionResult> EditPagesAsync(
        ConversionRequest request,
        IOutputWriter output,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var source = PdfSource.TryOpen(request.SourcePath, out var openFailure);
        if (source is null)
        {
            return openFailure!; // TryOpen sets the failure whenever it returns null.
        }

        var pageCount = source.Document.PageCount;
        if (pageCount == 0)
        {
            return ConversionResult.Fail(ConversionError.CorruptInput, "This PDF has no pages.");
        }

        var rotate = request.Target.Id == TargetCatalog.PdfRotate90.Id;
        var plan = PlanPieces(request, pageCount);
        if (plan.Failure is not null)
        {
            return plan.Failure;
        }

        return await WritePiecesAsync(
            request.SourcePath,
            ".pdf",
            plan.Pieces,
            plan.Unit,
            output,
            progress,
            (piece, temp, ct) =>
            {
                SavePdf(source.Document, piece.PageIndices, rotate, temp, ct);
                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static PiecePlan PlanPieces(ConversionRequest request, int n)
    {
        var all = Enumerable.Range(0, n);
        var targetId = request.Target.Id;

        if (targetId == TargetCatalog.PdfSplitPages.Id)
        {
            return PiecePlan.Of("pages", all.Select(i => new Piece(OutputLabels.Page(i + 1, n), [i])));
        }

        if (targetId == TargetCatalog.PdfSplitHalf.Id)
        {
            if (n < 2)
            {
                return PiecePlan.Fail(ConversionError.Unsupported, "Only one page.");
            }

            var firstHalf = (n + 1) / 2;
            return PiecePlan.Of(
                "parts",
                [
                    new Piece(OutputLabels.Part(1), all.Take(firstHalf).ToArray()),
                    new Piece(OutputLabels.Part(2), all.Skip(firstHalf).ToArray()),
                ]);
        }

        if (targetId == TargetCatalog.PdfExtractOdd.Id)
        {
            return PiecePlan.Single(OutputLabels.OddPages, all.Where(i => i % 2 == 0));
        }

        if (targetId == TargetCatalog.PdfExtractEven.Id)
        {
            return n < 2
                ? PiecePlan.Fail(ConversionError.Unsupported, "No even pages.")
                : PiecePlan.Single(OutputLabels.EvenPages, all.Where(i => i % 2 == 1));
        }

        if (targetId == TargetCatalog.PdfRotate90.Id)
        {
            return PiecePlan.Single(OutputLabels.Rotated, all);
        }

        if (targetId == TargetCatalog.PdfReverse.Id)
        {
            return PiecePlan.Single(OutputLabels.Reversed, all.Reverse());
        }

        if (targetId == TargetCatalog.PdfReorder.Id)
        {
            return PageOrder.TryParse(request.GetOption(OptionKeys.PageOrder), n, out var order)
                ? PiecePlan.Single(OutputLabels.Reordered, order)
                : PiecePlan.Fail(ConversionError.Unsupported, "Invalid page order.");
        }

        return PiecePlan.Fail(ConversionError.Unsupported, "This PDF tool isn't available.");
    }

    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    private static async Task<ConversionResult> ToImagesAsync(
        ConversionRequest request,
        IOutputWriter output,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var source = request.SourcePath;
        int n;
        try
        {
            n = await Pdfium.GetPageCountAsync(source, cancellationToken).ConfigureAwait(false);
        }
        catch (PdfPasswordProtectedException)
        {
            return ConversionResult.Fail(ConversionError.PasswordProtected, PasswordMessage);
        }
        catch (InvalidDataException)
        {
            return ConversionResult.Fail(ConversionError.CorruptInput, CorruptMessage);
        }

        if (n == 0)
        {
            return ConversionResult.Fail(ConversionError.CorruptInput, "This PDF has no pages.");
        }

        var pieces = Enumerable.Range(0, n).Select(i => new Piece(OutputLabels.Page(i + 1, n), [i])).ToArray();
        return await WritePiecesAsync(
            source,
            ".png",
            pieces,
            "pages",
            output,
            progress,
            (piece, temp, ct) => Pdfium.SavePagePngAsync(source, piece.PageIndices[0], temp, ct),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes and commits each piece in turn (CONTRACTS C2 S2–S5): a temp from the writer, commit, discard on any failure.
    /// On failure the pieces already committed stay and are listed in the result. Cancellation is checked between pieces;
    /// cancelling keeps committed pieces and discards the current temp.
    /// </summary>
    private static async Task<ConversionResult> WritePiecesAsync(
        string sourcePath,
        string extension,
        IReadOnlyList<Piece> pieces,
        string unit,
        IOutputWriter output,
        IProgress<double>? progress,
        Func<Piece, string, CancellationToken, Task> writePiece,
        CancellationToken cancellationToken)
    {
        var committed = new List<string>(pieces.Count);
        for (var i = 0; i < pieces.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var temp = output.CreateTempPath(sourcePath, extension);
            var done = false;
            try
            {
                await writePiece(pieces[i], temp, cancellationToken).ConfigureAwait(false);
                committed.Add(output.Commit(temp, sourcePath, extension, pieces[i].Label));
                done = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                var (error, reason) = Classify(ex);
                var message = committed.Count == 0
                    ? reason
                    : $"Stopped after {committed.Count} of {pieces.Count} {unit}: {reason}";
                return ConversionResult.Fail(error, message, committed);
            }
            finally
            {
                if (!done)
                {
                    output.Discard(temp);
                }
            }

            progress?.Report((i + 1) / (double)pieces.Count);
        }

        return ConversionResult.Ok(committed);
    }

    private static (ConversionError Error, string Reason) Classify(Exception ex) => ex switch
    {
        PdfPasswordProtectedException => (ConversionError.PasswordProtected, PasswordMessage),
        InvalidDataException => (ConversionError.CorruptInput, CorruptMessage),

        // IOException includes FileNotFoundException from Commit (CONTRACTS C1: all of these are OutputFailed).
        IOException or UnauthorizedAccessException or InvalidOperationException => (ConversionError.OutputFailed, OutputFailedMessage),
        _ => (ConversionError.Unknown, "Something went wrong while working on this PDF."),
    };

    private static void SavePdf(PdfDocument source, int[] pageIndices, bool rotate, string tempPath, CancellationToken cancellationToken)
    {
        using var target = new PdfDocument();
        foreach (var index in pageIndices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = target.AddPage(source.Pages[index]);
            if (rotate)
            {
                // Normalise first: malformed files can carry negative or > 360 values.
                page.Rotate = (((page.Rotate % 360) + 360) % 360 + 90) % 360;
            }
        }

        using var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        target.Save(stream);
    }

    private static ConversionResult SourceMissing() =>
        ConversionResult.Fail(ConversionError.SourceMissing, "The file is no longer there.");

    private sealed record Piece(string Label, int[] PageIndices);

    private sealed record PiecePlan(IReadOnlyList<Piece> Pieces, string Unit, ConversionResult? Failure)
    {
        public static PiecePlan Of(string unit, IEnumerable<Piece> pieces) => new(pieces.ToArray(), unit, null);

        public static PiecePlan Single(string label, IEnumerable<int> pageIndices) =>
            new([new Piece(label, pageIndices.ToArray())], "files", null);

        public static PiecePlan Fail(ConversionError error, string message) =>
            new([], string.Empty, ConversionResult.Fail(error, message));
    }
}
