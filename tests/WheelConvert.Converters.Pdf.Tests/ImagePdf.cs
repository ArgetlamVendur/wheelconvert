using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using SkiaSharp;

namespace WheelConvert.Converters.Pdf.Tests;

/// <summary>Builds test PDFs that carry raw JPEG image XObjects (PDFsharp's core build can't embed JPEGs itself).</summary>
internal static class ImagePdf
{
    /// <summary>A smooth gradient with light noise, so the JPEG is big at high quality and shrinks at 70.</summary>
    public static byte[] MakeJpeg(int width, int height, bool gray = false, int quality = 95)
    {
        var rng = new Random(42);
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, gray ? SKColorType.Gray8 : SKColorType.Rgba8888, SKAlphaType.Opaque));
        var pixels = bitmap.GetPixelSpan();
        var bytesPerPixel = gray ? 1 : 4;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * bytesPerPixel;
                var noise = rng.Next(-30, 30);
                byte Clamp(int v) => (byte)Math.Clamp(v + noise, 0, 255);
                if (gray)
                {
                    pixels[offset] = Clamp(x * 255 / width);
                }
                else
                {
                    pixels[offset] = Clamp(x * 255 / width);
                    pixels[offset + 1] = Clamp(y * 255 / height);
                    pixels[offset + 2] = Clamp((x + y) * 255 / (width + height));
                    pixels[offset + 3] = 255;
                }
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data.ToArray();
    }

    /// <summary>Adds an image XObject to <paramref name="document"/> and returns it (not yet used on any page).</summary>
    public static PdfDictionary AddImage(PdfDocument document, byte[] jpeg, int width, int height, string colorSpace = "/DeviceRGB", bool addSMask = false)
    {
        var image = new PdfDictionary(document);
        image.Elements["/Type"] = new PdfName("/XObject");
        image.Elements["/Subtype"] = new PdfName("/Image");
        image.Elements.SetInteger("/Width", width);
        image.Elements.SetInteger("/Height", height);
        image.Elements["/ColorSpace"] = new PdfName(colorSpace);
        image.Elements.SetInteger("/BitsPerComponent", 8);
        image.Elements["/Filter"] = new PdfName("/DCTDecode");
        image.CreateStream(jpeg);
        document.Internals.AddObject(image);
        if (addSMask)
        {
            var mask = new PdfDictionary(document);
            mask.Elements["/Type"] = new PdfName("/XObject");
            mask.Elements["/Subtype"] = new PdfName("/Image");
            mask.Elements.SetInteger("/Width", 1);
            mask.Elements.SetInteger("/Height", 1);
            mask.Elements["/ColorSpace"] = new PdfName("/DeviceGray");
            mask.Elements.SetInteger("/BitsPerComponent", 8);
            mask.CreateStream([255]);
            document.Internals.AddObject(mask);
            image.Elements["/SMask"] = mask.Reference;
        }

        return image;
    }

    /// <summary>Draws <paramref name="image"/> over the whole page.</summary>
    public static PdfPage AddPageWith(PdfDocument document, PdfDictionary image)
    {
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(400);
        page.Height = XUnit.FromPoint(300);
        var xObjects = new PdfDictionary(document);
        xObjects.Elements["/Im0"] = image.Reference;
        page.Resources.Elements["/XObject"] = xObjects;
        var content = page.Contents.AppendContent();
        content.CreateStream("q 400 0 0 300 0 0 cm /Im0 Do Q"u8.ToArray());
        return page;
    }
}
