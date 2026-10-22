using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WheelConvert.App.Interop;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Wheel;
using DragEventArgs = System.Windows.DragEventArgs;
using ShapePath = System.Windows.Shapes.Path;

namespace WheelConvert.App.Wheel;

/// <summary>
/// Circular, non-activating drop target (WP-04). Never opens, reads or stats dropped files: only path strings.
/// ADR-003: every drag handler sets Copy first and can only end on Copy or None.
/// </summary>
public partial class WheelWindow : Window, IWheelView
{
    private const double InnerRadius = 40;
    private const double OuterRadius = 125;

    // WP-04 req. 7: the hotkey wheel closes by itself after this long with no drag activity over it.
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(30);

    // Safety net for req. 8: if neither Drop nor DragLeave arrives after a deferred hide, finish hiding anyway.
    private static readonly TimeSpan PendingHideTimeout = TimeSpan.FromSeconds(2);

    private static readonly Brush SegmentFill = Frozen(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush HighlightFill = Frozen(new SolidColorBrush(Color.FromArgb(0xE6, 0x2D, 0x7F, 0xF9)));
    private static readonly Brush SegmentStroke = Frozen(new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)));

    private readonly DispatcherTimer _idleTimer;
    private readonly DispatcherTimer _pendingHideTimer;
    private readonly List<ShapePath> _segmentShapes = [];

    private IReadOnlyList<string> _paths = [];
    private IReadOnlyList<ConversionTarget> _targets = [];
    private int _highlighted = -1;
    private bool _dragInProgress;
    private bool _hidePending;

    public WheelWindow()
    {
        InitializeComponent();
        _idleTimer = new DispatcherTimer(IdleTimeout, DispatcherPriority.Normal, OnIdleTimerTick, Dispatcher) { IsEnabled = false };
        _pendingHideTimer = new DispatcherTimer(PendingHideTimeout, DispatcherPriority.Normal, OnPendingHideTimerTick, Dispatcher) { IsEnabled = false };
        SourceInitialized += OnSourceInitialized;
        DragEnter += OnDragEnter;
        DragOver += OnDragOver;
        DragLeave += OnDragLeave;
        Drop += OnDrop;
    }

    public Func<IReadOnlyList<string>, IReadOnlyList<ConversionTarget>>? TargetsProvider { get; set; }

    /// <summary>False while a hide is pending (req. 8): the wheel is already dismissed and only waits for Drop/DragLeave.</summary>
    public bool IsOpen => IsVisible && !_hidePending;

    public event EventHandler<WheelDropEventArgs>? Dropped;

    public void ShowAt(ScreenPoint point)
    {
        _pendingHideTimer.Stop();
        _hidePending = false;
        if (!_dragInProgress)
        {
            ClearSegments();
            CenterLabel.Text = string.Empty;
        }

        PlaceAt(point);
        if (!IsVisible)
        {
            Show();

            // Re-place after showing in case the first show applied WPF's own position.
            PlaceAt(point);
        }

        RestartIdleTimer();
    }

    public void HideWheel()
    {
        _idleTimer.Stop();
        if (_dragInProgress)
        {
            // WP-04 req. 8: on mouse-up the dismiss can arrive before Explorer delivers Drop. Stay fully visible
            // until Drop or DragLeave has been handled: Windows doesn't deliver drops to fully transparent pixels.
            _hidePending = true;
            _pendingHideTimer.Stop();
            _pendingHideTimer.Start();
            return;
        }

        HideNow();
    }

    private void HideNow()
    {
        _idleTimer.Stop();
        _pendingHideTimer.Stop();
        _hidePending = false;
        _dragInProgress = false;
        Hide();
        ClearSegments();
        CenterLabel.Text = string.Empty;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // WP-04 req. 2: ShowActivated=False alone still lets a click activate the window; the extended style
        // keeps Explorer's title bar active while the wheel shows.
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        if (style == 0 && Marshal.GetLastPInvokeError() != 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var updated = style | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        if (NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, updated) == 0 && Marshal.GetLastPInvokeError() != 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    /// <summary>
    /// Centres the wheel on a point in physical pixels, clamped to that monitor's work area. Positions in physical
    /// pixels (SetWindowPos) rather than via Left/Top: WPF converts Left/Top with the window's CURRENT monitor DPI,
    /// which is wrong when the point is on a monitor with a different scale.
    /// </summary>
    private void PlaceAt(ScreenPoint point)
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        var monitor = NativeMethods.MonitorFromPoint(new NativeMethods.POINT { X = point.X, Y = point.Y }, NativeMethods.MONITOR_DEFAULTTONEAREST);

        var dpi = NativeMethods.GetDpiForMonitor(monitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0
            ? dpiX
            : VisualTreeHelper.GetDpi(this).PixelsPerInchX;
        var size = (int)Math.Ceiling(Width * dpi / 96.0);

        var info = new NativeMethods.MONITORINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        var work = NativeMethods.GetMonitorInfo(monitor, ref info)
            ? info.rcWork
            : new NativeMethods.RECT { Left = int.MinValue / 2, Top = int.MinValue / 2, Right = int.MaxValue / 2, Bottom = int.MaxValue / 2 };

        // Max after Min: if the work area is ever smaller than the wheel, keep its top-left corner on-screen.
        var x = Math.Max(work.Left, Math.Min(point.X - (size / 2), work.Right - size));
        var y = Math.Max(work.Top, Math.Min(point.Y - (size / 2), work.Bottom - size));

        // Twice: if the first move crosses onto a monitor with another DPI, WPF rescales the window on
        // WM_DPICHANGED and may shift it. The second move, at the new DPI, lands exactly.
        const uint flags = NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (!NativeMethods.SetWindowPos(hwnd, 0, x, y, 0, 0, flags))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        var effects = DragDropEffects.None;
        try
        {
            _dragInProgress = true;
            RestartIdleTimer();
            _paths = ReadPaths(e.Data);
            _targets = [];
            ClearSegments();

            if (_paths.Count == 0)
            {
                CenterLabel.Text = "Not a file";
                return;
            }

            _targets = TargetsProvider?.Invoke(_paths) ?? [];
            if (_targets.Count == 0)
            {
                CenterLabel.Text = "Nothing available";
                return;
            }

            CenterLabel.Text = _paths.Count == 1 ? ExtensionLabel(_paths[0]) : $"+{_paths.Count} files";
            DrawSegments();
            SetHighlight(SegmentAt(e));
            effects = DragDropEffects.Copy;
        }
        finally
        {
            // Only Copy or None can leave this handler, also when an exception is thrown (ADR-003).
            e.Effects = effects;
            e.Handled = true;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        var effects = DragDropEffects.None;
        try
        {
            RestartIdleTimer();
            if (_targets.Count > 0)
            {
                SetHighlight(SegmentAt(e));
                effects = DragDropEffects.Copy;
            }
        }
        finally
        {
            e.Effects = effects;
            e.Handled = true;
        }
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        try
        {
            SetHighlight(-1);
            _dragInProgress = false;
            if (_hidePending)
            {
                HideNow();
            }
            else
            {
                RestartIdleTimer();
            }
        }
        finally
        {
            e.Handled = true;
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Copy;
        var effects = DragDropEffects.None;
        try
        {
            var index = _targets.Count > 0 ? SegmentAt(e) : -1;
            if (index >= 0)
            {
                var paths = _paths;
                var target = _targets[index];

                // Raised after this handler returns, so Explorer's drag loop isn't blocked by the subscriber.
                Dispatcher.BeginInvoke(() => Dropped?.Invoke(this, new WheelDropEventArgs(paths, target)));
                effects = DragDropEffects.Copy;
            }

            HideNow();
        }
        finally
        {
            e.Effects = effects;
            e.Handled = true;
        }
    }

    private void OnIdleTimerTick(object? sender, EventArgs e) => HideWheel();

    private void OnPendingHideTimerTick(object? sender, EventArgs e) => HideNow();

    private void RestartIdleTimer()
    {
        _idleTimer.Stop();
        _idleTimer.Start();
    }

    private int SegmentAt(DragEventArgs e)
    {
        // GetPosition is in DIPs relative to the window; WheelGeometry wants offsets from the centre.
        var position = e.GetPosition(this);
        var dx = position.X - (ActualWidth / 2);
        var dy = position.Y - (ActualHeight / 2);
        return WheelGeometry.HitTest(dx, dy, _targets.Count, InnerRadius, OuterRadius);
    }

    private void DrawSegments()
    {
        var center = new Point(Width / 2, Height / 2);
        for (var i = 0; i < _targets.Count; i++)
        {
            var shape = new ShapePath
            {
                Data = SegmentVisuals.CreateSector(i, _targets.Count, center, InnerRadius, OuterRadius),
                Fill = SegmentFill,
                Stroke = SegmentStroke,
                StrokeThickness = 1,
            };
            SegmentCanvas.Children.Add(shape);
            _segmentShapes.Add(shape);
        }

        for (var i = 0; i < _targets.Count; i++)
        {
            var label = new TextBlock
            {
                Text = _targets[i].Label,
                Foreground = Brushes.White,
                FontSize = 12,
                IsHitTestVisible = false,
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var at = SegmentVisuals.LabelCenter(i, _targets.Count, center, InnerRadius, OuterRadius);
            Canvas.SetLeft(label, at.X - (label.DesiredSize.Width / 2));
            Canvas.SetTop(label, at.Y - (label.DesiredSize.Height / 2));
            SegmentCanvas.Children.Add(label);
        }
    }

    private void ClearSegments()
    {
        SegmentCanvas.Children.Clear();
        _segmentShapes.Clear();
        _highlighted = -1;
        if (!_dragInProgress)
        {
            _paths = [];
            _targets = [];
        }
    }

    private void SetHighlight(int index)
    {
        if (index == _highlighted)
        {
            return;
        }

        if (_highlighted >= 0 && _highlighted < _segmentShapes.Count)
        {
            _segmentShapes[_highlighted].Fill = SegmentFill;
        }

        if (index >= 0 && index < _segmentShapes.Count)
        {
            _segmentShapes[index].Fill = HighlightFill;
        }

        _highlighted = index;
    }

    /// <summary>Paths only; nothing on disk is touched (req. 9).</summary>
    private static string[] ReadPaths(IDataObject data)
    {
        try
        {
            if (!data.GetDataPresent(DataFormats.FileDrop) || data.GetData(DataFormats.FileDrop) is not string[] paths)
            {
                return [];
            }

            return paths.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        }
        catch (COMException)
        {
            // Some drag sources (e.g. virtual items) advertise FileDrop but fail to render it: treat as "not a file".
            return [];
        }
    }

    private static string ExtensionLabel(string path)
    {
        var extension = System.IO.Path.GetExtension(path);
        return string.IsNullOrEmpty(extension) ? "File" : extension.ToLowerInvariant();
    }

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
