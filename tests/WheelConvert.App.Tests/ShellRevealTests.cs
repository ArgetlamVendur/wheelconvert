using WheelConvert.App.Interop;

namespace WheelConvert.App.Tests;

public sealed class ShellRevealTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wc-reveal-test-" + Guid.NewGuid().ToString("N"));

    public ShellRevealTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void CanResolve_PathWithCommaAndDash_ReturnsTrue()
    {
        // The old "explorer /select,<path>" command line broke on commas; the Shell API takes the path as-is.
        var path = Path.Combine(_dir, "a,b - -x.png");
        File.WriteAllBytes(path, [1]);

        Assert.True(RunSta(() => ShellReveal.CanResolve(path)));
    }

    [Fact]
    public async Task RevealAsync_MissingFile_ReturnsFalseAndOpensNothing()
    {
        var path = Path.Combine(_dir, "gone,.png");

        Assert.False(await ShellReveal.RevealAsync(path, null));
    }

    [Fact]
    public async Task RevealAsync_RelativePath_ReturnsFalse()
    {
        Assert.False(await ShellReveal.RevealAsync("a,b.png", null));
    }

    private static T RunSta<T>(Func<T> work)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = work(); } catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) { throw error; }
        return result;
    }
}
