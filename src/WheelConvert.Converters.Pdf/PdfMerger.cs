using PdfSharp.Pdf;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Output;

namespace WheelConvert.Converters.Pdf;

/// <summary>
/// <c>pdf.merge</c> (docs/modules/WP-14-pdf-merge.md): imports every page of every input, in
/// <see cref="ConversionRequest.AllSourcePaths"/> order, into one new document. Inputs are opened read-only
/// (CONTRACTS C2 S1); the output goes next to the first input via the output writer. Bookmarks and document-level
/// metadata of the inputs are not carried over.
/// </summary>
internal static class PdfMerger
{
    public static ConversionResult Merge(
        ConversionRequest request,
        IOutputWriter output,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var paths = request.AllSourcePaths;
        if (paths.Count < 2)
        {
            return ConversionResult.Fail(ConversionError.Unsupported, "Drop two or more PDFs to merge.");
        }

        // Imported pages keep referring to their source documents, so every input stays open until the save is done.
        var sources = new List<PdfSource>(paths.Count);
        string? temp = null;
        var committed = false;
        try
        {
            using var target = new PdfDocument();
            for (var i = 0; i < paths.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Path.GetFileName(paths[i]);

                PdfSource? source;
                ConversionResult? failure;
                try
                {
                    source = PdfSource.TryOpen(paths[i], out failure);
                }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                {
                    return ConversionResult.Fail(ConversionError.SourceMissing, $"{name} is no longer there.");
                }

                if (source is null)
                {
                    return failure!.Error == ConversionError.PasswordProtected // TryOpen sets the failure whenever it returns null.
                        ? ConversionResult.Fail(ConversionError.PasswordProtected, $"{name} is password-protected.")
                        : ConversionResult.Fail(ConversionError.CorruptInput, $"Couldn't read {name}.");
                }

                sources.Add(source);
                var pages = source.Document.Pages;
                if (pages.Count == 0)
                {
                    return ConversionResult.Fail(ConversionError.CorruptInput, $"Couldn't read {name}.");
                }

                for (var p = 0; p < pages.Count; p++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    target.AddPage(pages[p]);
                }

                progress?.Report((i + 1) / (double)paths.Count);
            }

            temp = output.CreateTempPath(request.SourcePath, ".pdf");
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                target.Save(stream);
            }

            var final = output.Commit(temp, request.SourcePath, ".pdf", OutputLabels.Merged);
            committed = true;
            return ConversionResult.Ok([final]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ConversionResult.Fail(ConversionError.OutputFailed, "Couldn't save the file here.");
        }
        finally
        {
            if (temp is not null && !committed)
            {
                output.Discard(temp);
            }

            foreach (var source in sources)
            {
                source.Dispose();
            }
        }
    }
}
