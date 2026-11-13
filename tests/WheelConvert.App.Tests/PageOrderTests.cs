using System.IO;
using WheelConvert.App.Pdf;
using WheelConvert.Core.Contracts;

namespace WheelConvert.App.Tests;

public class PageOrderTests
{
    private static string Join(IReadOnlyList<int> r) => string.Join(',', r);

    [Fact]
    public void NewViewModelIsIdentityOrder() => Assert.Equal("1,2,3,4,5", Join(new PageOrderViewModel(5).Result));

    [Fact]
    public void MoveToFront()
    {
        var vm = new PageOrderViewModel(5);
        Assert.True(vm.Move(4, 0));
        Assert.Equal("5,1,2,3,4", Join(vm.Result));
    }

    [Fact]
    public void MoveOutOfRangeIsRejected()
    {
        var vm = new PageOrderViewModel(3);
        Assert.False(vm.Move(0, 3));
        Assert.False(vm.Move(-1, 1));
        Assert.Equal("1,2,3", Join(vm.Result));
    }

    [Fact]
    public void ScenarioFromAcceptanceTest4()
    {
        var vm = new PageOrderViewModel(5);
        vm.Move(4, 0);
        vm.Remove(2); // page 2 sits at index 2 after the move
        Assert.Equal("5,1,3,4", Join(vm.Result));
    }

    [Fact]
    public void RemoveOutOfRangeIsRejected()
    {
        var vm = new PageOrderViewModel(2);
        Assert.False(vm.Remove(2));
        Assert.Equal(2, vm.Pages.Count);
    }

    [Fact]
    public void ReverseReversesCurrentOrder()
    {
        var vm = new PageOrderViewModel(4);
        vm.Move(0, 1);
        vm.Reverse();
        Assert.Equal("4,3,1,2", Join(vm.Result));
    }

    [Fact]
    public void ResetRestoresRemovedPagesAndOrder()
    {
        var vm = new PageOrderViewModel(3);
        vm.Remove(0);
        vm.Move(0, 1);
        vm.Reset();
        Assert.Equal("1,2,3", Join(vm.Result));
    }

    [Fact]
    public void SaveDisabledWhenEmpty()
    {
        var vm = new PageOrderViewModel(1);
        vm.Remove(0);
        Assert.False(vm.CanSave);
        Assert.False(vm.Accept());
        Assert.Null(vm.FinalResult);
    }

    [Fact]
    public void CancelGivesNull()
    {
        var vm = new PageOrderViewModel(3);
        vm.Move(2, 0);
        Assert.Null(vm.FinalResult);
    }

    [Fact]
    public void AcceptGivesOrderWithSubset()
    {
        var vm = new PageOrderViewModel(3);
        vm.Remove(1);
        Assert.True(vm.Accept());
        Assert.Equal("1,3", Join(vm.FinalResult!));
    }

    [Fact]
    public void ZeroPagesRejected() => Assert.Throws<ArgumentOutOfRangeException>(() => new PageOrderViewModel(0));

    private sealed class FakeRenderer(Func<int> count) : IPdfPageRenderer
    {
        public Task<int> GetPageCountAsync(string pdfPath, CancellationToken cancellationToken) => Task.FromResult(count());

        public Task<byte[]> RenderPagePngAsync(string pdfPath, int pageIndex, int maxPixels, CancellationToken cancellationToken) =>
            Task.FromResult(Array.Empty<byte>());
    }

    private sealed class NullLog : ILog
    {
        public void Info(string message) { }

        public void Warn(string message, Exception? exception = null) { }

        public void Error(string message, Exception? exception = null) { }
    }

    private static Task<PageProbe> Probe(Func<int> count) =>
        PageOrderPrompt.ProbeAsync(new FakeRenderer(count), new NullLog(), "x.pdf", CancellationToken.None);

    [Fact]
    public async Task ProbeOk()
    {
        var p = await Probe(() => 5);
        Assert.Equal(5, p.PageCount);
        Assert.Null(p.ErrorMessage);
    }

    [Fact]
    public async Task ProbePasswordProtected() =>
        Assert.Equal("This PDF is password-protected.", (await Probe(() => throw new PdfPasswordProtectedException())).ErrorMessage);

    [Fact]
    public async Task ProbeCorrupt() =>
        Assert.Equal("This file can't be read as a PDF.", (await Probe(() => throw new InvalidDataException())).ErrorMessage);

    [Fact]
    public async Task ProbeOtherErrorIsGeneric()
    {
        var p = await Probe(() => throw new InvalidOperationException("boom"));
        Assert.NotNull(p.ErrorMessage);
        Assert.DoesNotContain("boom", p.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProbeTooManyPages() =>
        Assert.Equal("Too many pages to reorder here (301).", (await Probe(() => 301)).ErrorMessage);

    [Fact]
    public async Task ProbeExactlyLimitIsAllowed() => Assert.Null((await Probe(() => 300)).ErrorMessage);
}
