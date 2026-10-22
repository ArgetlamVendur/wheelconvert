using System.Security.Cryptography;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using WheelConvert.Core.Output;

namespace WheelConvert.Converters.Pdf.Tests;

/// <summary>
/// A fresh folder under the system temp directory, deleted afterwards. Every test PDF is generated here with PDFsharp;
/// no test ever reads the user's scratch folder (AGENTS.md §4.11).
/// </summary>
public sealed class TestFolder : IDisposable
{
    public TestFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wc-pdf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Page i (0-based) is <c>100 + i</c> points wide, so tests can tell pages apart without drawing text.</summary>
    public static double WidthOf(int pageIndex) => 100 + pageIndex;

    public static int IndexOf(PdfPage page) => (int)Math.Round(page.Width.Point) - 100;

    public string CreatePdf(int pages, string name = "doc.pdf", string? userPassword = null, string? ownerPassword = null, int rotate = 0)
    {
        var path = System.IO.Path.Combine(Path, name);
        using var document = new PdfDocument();
        for (var i = 0; i < pages; i++)
        {
            var page = document.AddPage();
            page.Width = PdfSharp.Drawing.XUnit.FromPoint(WidthOf(i));
            page.Height = PdfSharp.Drawing.XUnit.FromPoint(300);
            page.Rotate = rotate;
        }

        if (userPassword is not null)
        {
            document.SecuritySettings.UserPassword = userPassword;
        }

        if (ownerPassword is not null)
        {
            document.SecuritySettings.OwnerPassword = ownerPassword;
        }

        document.Save(path);
        return path;
    }

    public string CreateFile(string name, byte[] content)
    {
        var path = System.IO.Path.Combine(Path, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    public string[] TempFiles() => Directory.GetFiles(Path).Where(SafeOutputWriter.IsTempFile).ToArray();

    public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    public static PdfDocument Read(string path) => PdfReader.Open(path, PdfDocumentOpenMode.Import);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover folder in %TEMP% is harmless and must not fail the test run.
        }
    }
}
