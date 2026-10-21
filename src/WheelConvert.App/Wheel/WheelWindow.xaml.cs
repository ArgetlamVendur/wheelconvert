using System.Windows;
using WheelConvert.Core.Contracts;

namespace WheelConvert.App.Wheel;

/// <summary>
/// STUB: WP-04 (docs/modules/WP-04-wheel-window.md). Circular, non-activating drop target.
/// Segment hit-testing must use Core's WheelGeometry (already unit-tested).
/// </summary>
public partial class WheelWindow : Window, IWheelView
{
    public WheelWindow()
    {
        InitializeComponent();
        // TODO(WP-04): SourceInitialized → add WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW.
        // TODO(WP-04): DragEnter/DragOver/DragLeave/Drop handlers. FIRST statement in each: e.Effects = DragDropEffects.Copy (ADR-003).
    }

    public Func<IReadOnlyList<string>, IReadOnlyList<ConversionTarget>>? TargetsProvider { get; set; }

    public bool IsOpen => IsVisible;

    // Raised by WP-04's Drop handler.
#pragma warning disable CS0067
    public event EventHandler<WheelDropEventArgs>? Dropped;
#pragma warning restore CS0067

    // TODO(WP-04): convert physical px → DIPs for the monitor under the point, centre, Show().
    public void ShowAt(ScreenPoint point)
    {
        Left = point.X - (Width / 2);
        Top = point.Y - (Height / 2);
        Show();
    }

    public void HideWheel() => Hide();
}
