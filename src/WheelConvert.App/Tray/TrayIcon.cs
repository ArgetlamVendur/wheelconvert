using System.Windows.Threading;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Settings;
using Forms = System.Windows.Forms;

namespace WheelConvert.App.Tray;

/// <summary>
/// STUB: WP-05 (docs/modules/WP-05-tray-settings.md). Tray icon, context menu, and balloon notifications (ADR-007).
/// Implements INotifier; Notify may be called from any thread. Constructor is FROZEN (CompositionRoot calls it).
/// </summary>
public sealed class TrayIcon : INotifier, IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Dispatcher _dispatcher;
    private readonly Func<AppSettings> _settings;
    private readonly FormatRegistry _registry;

    public TrayIcon(Dispatcher dispatcher, Func<AppSettings> settings, FormatRegistry registry)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        // TODO(WP-05): load the embedded icon "WheelConvert.App.wheelconvert.ico"; build the context menu.
        _icon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "WheelConvert",
            Visible = true,
        };
    }

#pragma warning disable CS0067
    /// <summary>User picked Exit. WP-11 shuts the app down.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>User picked "Check again". WP-11 calls FormatRegistry.Refresh off the UI thread.</summary>
    public event EventHandler? RefreshRequested;

    /// <summary>User picked a trigger mode. WP-11 saves settings and recreates triggers.</summary>
    public event EventHandler<TriggerMode>? TriggerModeChanged;
#pragma warning restore CS0067

    // TODO(WP-05): marshal via _dispatcher; ShowBalloonTip; remember pathToReveal for BalloonTipClicked.
    public void Notify(NotificationKind kind, string title, string message, string? pathToReveal = null)
    {
        _ = (_dispatcher, _settings, _registry);
    }

    /// <summary>Call on the UI thread.</summary>
    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
