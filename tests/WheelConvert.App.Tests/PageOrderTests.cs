using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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

    [Fact]
    public async Task ProbeZeroPagesIsUnreadable()
    {
        var p = await Probe(() => 0);
        Assert.Equal("This file can't be read as a PDF.", p.ErrorMessage);
    }

    private static BitmapSource TinyImage()
    {
        var image = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2);
        image.Freeze();
        return image;
    }

    [Fact]
    public void ResetBringsBackSameItemWithItsThumbnail()
    {
        var vm = new PageOrderViewModel(3);
        var second = vm.Pages[1];
        var thumbnail = TinyImage();
        second.Thumbnail = thumbnail;
        vm.Remove(1);
        vm.Reset();
        Assert.Same(second, vm.Pages[1]);
        Assert.Same(thumbnail, vm.Pages[1].Thumbnail);
    }

    [Fact]
    public void MoveItemOfRemovedPageIsRejected()
    {
        var vm = new PageOrderViewModel(3);
        var held = vm.Pages[2];
        vm.Remove(2);
        Assert.False(vm.MoveItem(held, 0));
        Assert.Equal("1,2", Join(vm.Result));
    }

    [Fact]
    public void MoveItemMovesByInstance()
    {
        var vm = new PageOrderViewModel(3);
        Assert.True(vm.MoveItem(vm.Pages[2], 0));
        Assert.Equal("3,1,2", Join(vm.Result));
    }

    private sealed class WindowRenderer : IPdfPageRenderer
    {
        private readonly object _lock = new();
        private readonly List<(int Index, int MaxPixels)> _calls = [];
        private CancellationToken _lastToken;

        public ManualResetEventSlim? FirstCallGate { get; init; }

        public int? FailIndex { get; init; }

        public bool BlockUntilCancelled { get; init; }

        public CancellationToken LastToken
        {
            get
            {
                lock (_lock)
                {
                    return _lastToken;
                }
            }
        }

        public List<(int Index, int MaxPixels)> Calls
        {
            get
            {
                lock (_lock)
                {
                    return [.. _calls];
                }
            }
        }

        public Task<int> GetPageCountAsync(string pdfPath, CancellationToken cancellationToken) => Task.FromResult(5);

        public async Task<byte[]> RenderPagePngAsync(string pdfPath, int pageIndex, int maxPixels, CancellationToken cancellationToken)
        {
            int number;
            lock (_lock)
            {
                _calls.Add((pageIndex, maxPixels));
                _lastToken = cancellationToken;
                number = _calls.Count;
            }

            if (BlockUntilCancelled)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            }

            if (number == 1 && FirstCallGate is not null)
            {
                FirstCallGate.Wait(TimeSpan.FromSeconds(10), CancellationToken.None);
            }

            if (pageIndex == FailIndex)
            {
                throw new InvalidOperationException("render failed");
            }

            using var stream = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(TinyImage()));
            encoder.Save(stream);
            return stream.ToArray();
        }
    }

    private static void Pump(Func<bool> done, int maxMilliseconds = 8000)
    {
        var end = DateTime.UtcNow.AddMilliseconds(maxMilliseconds);
        while (!done() && DateTime.UtcNow < end)
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Thread.Sleep(10);
        }
    }

    /// <summary>Shows the window off-screen, runs the body, and always closes the window afterwards.</summary>
    private static void WithWindow(PageOrderViewModel vm, IPdfPageRenderer renderer, Action<PageOrderWindow> body)
    {
        RunInSta(() =>
        {
            var window = new PageOrderWindow(vm, renderer, new NullLog(), @"C:\scratch\some file.pdf")
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -10000,
                Top = -10000,
                ShowInTaskbar = false,
                ShowActivated = false,
            };
            var closed = false;
            window.Closed += (_, _) => closed = true;
            try
            {
                window.Show();
                body(window);
            }
            finally
            {
                if (!closed)
                {
                    window.Close();
                }
            }
        });
    }

    [Fact]
    public void WindowRequestsEveryPageAt320Pixels()
    {
        var vm = new PageOrderViewModel(5);
        var renderer = new WindowRenderer();
        WithWindow(vm, renderer, _ =>
        {
            Pump(() => vm.Pages.All(p => p.Thumbnail is not null));
            Assert.All(vm.Pages, p => Assert.NotNull(p.Thumbnail));
        });

        Assert.Equal([0, 1, 2, 3, 4], renderer.Calls.Select(c => c.Index));
        Assert.All(renderer.Calls, c => Assert.Equal(320, c.MaxPixels));
    }

    [Fact]
    public void RemovedPageGetsThumbnailAfterReset()
    {
        var vm = new PageOrderViewModel(5);
        using var gate = new ManualResetEventSlim();
        var renderer = new WindowRenderer { FirstCallGate = gate };
        // Page 2 is already removed when loading starts, and only comes back afterwards.
        vm.Remove(1);
        WithWindow(vm, renderer, _ =>
        {
            Pump(() => renderer.Calls.Count >= 1);
            vm.Reset();
            gate.Set();
            Pump(() => vm.Pages.All(p => p.Thumbnail is not null));
            Assert.NotNull(vm.Pages[1].Thumbnail);
        });
    }

    [Fact]
    public void FailedPageDoesNotStopLaterPages()
    {
        var vm = new PageOrderViewModel(5);
        var renderer = new WindowRenderer { FailIndex = 1 };
        WithWindow(vm, renderer, _ =>
        {
            Pump(() => vm.Pages[4].Thumbnail is not null);
            Assert.NotNull(vm.Pages[0].Thumbnail);
            Assert.NotNull(vm.Pages[2].Thumbnail);
            Assert.NotNull(vm.Pages[4].Thumbnail);
            Assert.Null(vm.Pages[1].Thumbnail);
            Assert.Equal("No preview", vm.Pages[1].Placeholder);
            Assert.Equal("Loading…", vm.Pages[0].Placeholder);
        });
    }

    [Fact]
    public void ClosingWindowCancelsRendering()
    {
        var vm = new PageOrderViewModel(5);
        var renderer = new WindowRenderer { BlockUntilCancelled = true };
        WithWindow(vm, renderer, window =>
        {
            Pump(() => renderer.Calls.Count >= 1);
            var token = renderer.LastToken;
            Assert.False(token.IsCancellationRequested);
            window.Close();
            Pump(() => token.IsCancellationRequested);
            Assert.True(token.IsCancellationRequested);
            Pump(() => renderer.Calls.Count > 1, 500); // Gives a wrongly continuing loader time to ask again.
            Assert.Single(renderer.Calls);
            Assert.Null(vm.FinalResult);
        });
    }

    [Fact]
    public void SaveWithNoPagesKeepsWindowOpen()
    {
        var vm = new PageOrderViewModel(2);
        WithWindow(vm, new WindowRenderer(), window =>
        {
            vm.Remove(1);
            vm.Remove(0);
            var save = (Button)window.FindName("SaveButton");
            Assert.False(save.IsEnabled);
            save.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Pump(() => false, 300);
            Assert.True(window.IsVisible);
            Assert.Null(vm.FinalResult);
        });
    }

    [Fact]
    public void DeleteIsIgnoredWhileAPageIsHeld()
    {
        var vm = new PageOrderViewModel(4);
        WithWindow(vm, new WindowRenderer(), window =>
        {
            var list = (ListBox)window.FindName("PageList");
            list.SelectedIndex = 1;

            void Press(Key key) =>
                list.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                });

            // A real mouse press sets this field (with mouse capture, which a test without a mouse cannot hold).
            var dragField = typeof(PageOrderWindow).GetField("_dragItem", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(dragField);
            dragField.SetValue(window, vm.Pages[1]);
            Press(Key.Delete);
            Assert.Equal("1,2,3,4", Join(vm.Result));

            dragField.SetValue(window, null);
            Press(Key.Delete);
            Assert.Equal("1,3,4", Join(vm.Result));
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA test timed out.");
        if (caught != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(caught).Throw();
        }
    }
}
