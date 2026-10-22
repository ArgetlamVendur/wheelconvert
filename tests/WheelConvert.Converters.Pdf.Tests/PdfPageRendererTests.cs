using SkiaSharp;
using WheelConvert.Core.Contracts;
using Xunit;

namespace WheelConvert.Converters.Pdf.Tests;

/// <summary>Acceptance test 9 plus the renderer's error contract and thread safety.</summary>
public sealed class PdfPageRendererTests : IDisposable
{
    private readonly TestFolder _folder = new();
    private readonly PdfPageRenderer _renderer = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public async Task PageCount_MatchesDocument()
    {
        var source = _folder.CreatePdf(7);

        Assert.Equal(7, await _renderer.GetPageCountAsync(source, CancellationToken.None));
    }

    [Theory]
    [InlineData(0, 160)]
    [InlineData(90, 160)]
    [InlineData(0, 1000)]
    public async Task Thumbnail_FitsWithinMaxPixels(int rotate, int maxPixels)
    {
        var source = _folder.CreatePdf(2, rotate: rotate);

        var png = await _renderer.RenderPagePngAsync(source, 1, maxPixels, CancellationToken.None);

        using var bitmap = SKBitmap.Decode(png);
        Assert.NotNull(bitmap);
        Assert.InRange(Math.Max(bitmap.Width, bitmap.Height), maxPixels - 2, maxPixels);
    }

    [Fact]
    public async Task Encrypted_ThrowsPasswordProtected()
    {
        var source = _folder.CreatePdf(1, userPassword: "secret", ownerPassword: "owner");

        await Assert.ThrowsAsync<PdfPasswordProtectedException>(() => _renderer.GetPageCountAsync(source, CancellationToken.None));
        await Assert.ThrowsAsync<PdfPasswordProtectedException>(() => _renderer.RenderPagePngAsync(source, 0, 100, CancellationToken.None));
    }

    [Fact]
    public async Task Garbage_ThrowsInvalidData()
    {
        var source = _folder.CreateFile("junk.pdf", [.. Enumerable.Repeat((byte)0x41, 2048)]);

        await Assert.ThrowsAsync<InvalidDataException>(() => _renderer.GetPageCountAsync(source, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => _renderer.RenderPagePngAsync(source, 0, 100, CancellationToken.None));
    }

    [Fact]
    public async Task PageOutOfRange_Throws()
    {
        var source = _folder.CreatePdf(2);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _renderer.RenderPagePngAsync(source, 5, 100, CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentCallsFromTwoInstances_AllSucceed()
    {
        var source = _folder.CreatePdf(6);
        var other = new PdfPageRenderer();

        var tasks = Enumerable.Range(0, 24).Select(i => (i % 2 == 0 ? _renderer : other)
            .RenderPagePngAsync(source, i % 6, 120, CancellationToken.None));
        var results = await Task.WhenAll(tasks);

        Assert.All(results, png =>
        {
            using var bitmap = SKBitmap.Decode(png);
            Assert.NotNull(bitmap);
        });
    }

    [Fact]
    public async Task CancelledToken_Throws()
    {
        var source = _folder.CreatePdf(1);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _renderer.RenderPagePngAsync(source, 0, 100, new CancellationToken(canceled: true)));
    }
}
