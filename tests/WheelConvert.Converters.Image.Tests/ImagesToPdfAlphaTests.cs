using ImageMagick;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Output;
using Xunit;

namespace WheelConvert.Converters.Image.Tests;

/// <summary>
/// WP-15: a PNG with transparency keeps its opaque pixels and gets white behind the transparent ones.
/// Without the flatten the transparent pixels embed as black (Magick writes RGB, dropping the alpha channel).
/// </summary>
[Collection("MagickLimits")]
public sealed class ImagesToPdfAlphaTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wc-pdf-alpha-" + Guid.NewGuid().ToString("N"));

    public ImagesToPdfAlphaTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Best effort clean-up of the temp folder.
        }
    }

    [Fact]
    public async Task HalfTransparentPng_OpaquePartKept_TransparentPartWhite()
    {
        var path = Path.Combine(_dir, "half.png");
        using (var image = new MagickImage(MagickColors.Transparent, 100, 10))
        {
            using var blue = new MagickImage(MagickColors.Blue, 50, 10);
            image.Composite(blue, 0, 0, CompositeOperator.Over);
            image.Write(path, MagickFormat.Png32);
        }

        var request = ConversionRequest.Create(path, TargetCatalog.ImageToPdf);
        var result = await new ImageConverter(new ListLog()).ConvertAsync(request, new SafeOutputWriter(), null, CancellationToken.None);

        Assert.True(result.Success);
        using var document = PdfReader.Open(result.OutputPaths[0], PdfDocumentOpenMode.Import);
        var xObjects = document.Pages[0].Resources.Elements.GetDictionary("/XObject");
        Assert.NotNull(xObjects);
        var dict = Assert.IsType<PdfDictionary>(Assert.IsType<PdfReference>(xObjects.Elements.Values.Single()).Value);
        Assert.True(dict.Stream.TryUncompress());
        var raw = dict.Stream.Value;
        var width = dict.Elements.GetInteger("/Width");

        // First pixel of the first row is blue, last pixel of the first row is white.
        Assert.Equal([0, 0, 255], raw[..3]);
        Assert.Equal([255, 255, 255], raw[((width - 1) * 3)..(width * 3)]);
    }
}
