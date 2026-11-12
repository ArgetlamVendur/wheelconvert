using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Threading;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Infrastructure;
using WheelConvert.Core.Settings;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using IOPath = System.IO.Path;

namespace WheelConvert.App.Tray;

/// <summary>
/// WP-05 (docs/modules/WP-05-tray-settings.md, ADR-007): tray icon, context menu and balloon notifications.
/// Implements INotifier; Notify may be called from any thread. Constructor is FROZEN (CompositionRoot calls it).
/// Never saves settings or restarts triggers itself: it raises events and WP-11 acts on them.
/// </summary>
public sealed class TrayIcon : INotifier, IDisposable
{
    private const string IconResourceName = "WheelConvert.App.wheelconvert.ico";

    private readonly Forms.NotifyIcon _icon;
    private readonly Drawing.Icon? _ownedIcon;
    private readonly Dispatcher _dispatcher;
    private readonly Func<AppSettings> _settings;
    private readonly FormatRegistry _registry;
    private readonly StartupRegistration _startup = new();
    private readonly string? _version;

    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _header;
    private readonly Forms.ToolStripMenuItem _status;
    private readonly Forms.ToolStripMenuItem _startWithWindows;
    private readonly Dictionary<TriggerMode, Forms.ToolStripMenuItem> _triggerItems = [];

    // Only touched on the UI thread (Notify marshals there before reading or writing it).
    private string? _pendingReveal;
    private bool _disposed;

    public TrayIcon(Dispatcher dispatcher, Func<AppSettings> settings, FormatRegistry registry)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _version = typeof(TrayIcon).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        _menu = new Forms.ContextMenuStrip();
        _header = new Forms.ToolStripMenuItem(TrayMenuModel.HeaderText(_version)) { Enabled = false };
        _status = new Forms.ToolStripMenuItem("Status");
        _startWithWindows = new Forms.ToolStripMenuItem("Start with Windows");
        BuildMenu();

        _ownedIcon = LoadEmbeddedIcon();
        _icon = new Forms.NotifyIcon
        {
            Icon = _ownedIcon ?? Drawing.SystemIcons.Application,
            Text = TrayMenuModel.Fit(TrayMenuModel.AppName, TrayMenuModel.MaxTooltipLength),
            ContextMenuStrip = _menu,
        };
        _icon.BalloonTipClicked += OnBalloonClicked;
        _icon.BalloonTipClosed += OnBalloonClosed;
        _icon.Visible = true;
    }

    /// <summary>User picked Exit. WP-11 shuts the app down.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>User picked "Check again". WP-11 calls FormatRegistry.Refresh off the UI thread.</summary>
    public event EventHandler? RefreshRequested;

    /// <summary>User picked a trigger mode. WP-11 saves settings and recreates triggers.</summary>
    public event EventHandler<TriggerMode>? TriggerModeChanged;

    public void Notify(NotificationKind kind, string title, string message, string? pathToReveal = null)
    {
        if (_dispatcher.CheckAccess())
        {
            ShowBalloon(kind, title, message, pathToReveal);
            return;
        }

        // INotifier never throws; BeginInvoke on a dispatcher that has shut down is a silent no-op.
        _dispatcher.BeginInvoke(() => ShowBalloon(kind, title, message, pathToReveal));
    }

    /// <summary>Call on the UI thread. Idempotent.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _icon.BalloonTipClicked -= OnBalloonClicked;
        _icon.BalloonTipClosed -= OnBalloonClosed;
        // Hiding before Dispose removes the icon at once; otherwise a ghost icon stays until the mouse passes over it.
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _ownedIcon?.Dispose();
    }

    private void ShowBalloon(NotificationKind kind, string title, string message, string? pathToReveal, bool requested = false)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (!requested && !TrayMenuModel.ShouldShow(kind, _settings()))
            {
                return;
            }

            var text = TrayMenuModel.Fit(string.IsNullOrWhiteSpace(message) ? title : message, TrayMenuModel.MaxBalloonTextLength);
            if (string.IsNullOrWhiteSpace(text))
            {
                return; // ShowBalloonTip throws on empty text.
            }

            _pendingReveal = pathToReveal;
            _icon.BalloonTipTitle = TrayMenuModel.Fit(string.IsNullOrWhiteSpace(title) ? TrayMenuModel.AppName : title, TrayMenuModel.MaxBalloonTitleLength);
            _icon.BalloonTipText = text;
            _icon.BalloonTipIcon = TrayMenuModel.BalloonIcon(kind);
            _icon.ShowBalloonTip(5000); // Windows 10+ ignores the timeout and decides itself.
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or ExternalException)
        {
            // Notifications are best-effort (Focus Assist may hide them too); callers log the underlying event.
        }
    }

    private void OnBalloonClicked(object? sender, EventArgs e)
    {
        var pending = _pendingReveal;
        _pendingReveal = null;
        if (pending is not string path || !IOPath.IsPathFullyQualified(path) || !(File.Exists(path) || Directory.Exists(path)))
        {
            return;
        }

        // Req. 2: Explorer with the file selected. Path is a separate argument (never a shell string), and fully
        // qualified, so an untrusted file name can't be read as an Explorer switch (AGENTS §4.10).
        StartExplorer(new ProcessStartInfo("explorer.exe") { ArgumentList = { "/select,", path } });
    }

    private void OnBalloonClosed(object? sender, EventArgs e) => _pendingReveal = null;

    private void BuildMenu()
    {
        var howTo = new Forms.ToolStripMenuItem("How to use");
        // The user asked for it, so it shows even when success balloons are turned off.
        howTo.Click += (_, _) => ShowBalloon(NotificationKind.Success, TrayMenuModel.AppName, CurrentModel().HowTo, null, requested: true);

        var trigger = new Forms.ToolStripMenuItem("Trigger");
        foreach (var (mode, label) in new[] { (TriggerMode.ShiftDrag, "Shift-drag"), (TriggerMode.Hotkey, "Hotkey"), (TriggerMode.Both, "Both") })
        {
            var item = new Forms.ToolStripMenuItem(label);
            item.Click += (_, _) => OnTriggerClicked(mode);
            _triggerItems[mode] = item;
            trigger.DropDownItems.Add(item);
        }

        _startWithWindows.Click += (_, _) => OnStartWithWindowsClicked();

        var openSettings = new Forms.ToolStripMenuItem("Open settings file");
        openSettings.Click += (_, _) => OpenAppFile(JsonSettingsStore.DefaultPath, "settings file");

        var openLog = new Forms.ToolStripMenuItem("Open log");
        openLog.Click += (_, _) => OpenAppFile(FileLog.DefaultPath, "log file");

        var exit = new Forms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        _menu.Items.AddRange(new Forms.ToolStripItem[]
        {
            _header,
            new Forms.ToolStripSeparator(),
            howTo,
            _status,
            trigger,
            _startWithWindows,
            new Forms.ToolStripSeparator(),
            openSettings,
            openLog,
            new Forms.ToolStripSeparator(),
            exit,
        });

        // Refresh every time the menu opens, so the radio shows the saved mode and the check shows the real
        // registry value, even after another copy of the app or the user changed them.
        _menu.Opening += (_, _) => RefreshMenuState();
        RefreshMenuState();
    }

    private void RefreshMenuState()
    {
        var model = CurrentModel();

        foreach (var (mode, item) in _triggerItems)
        {
            item.Checked = mode == model.SelectedTrigger;
        }

        _status.DropDownItems.Clear();
        foreach (var line in model.StatusLines)
        {
            _status.DropDownItems.Add(new Forms.ToolStripMenuItem(line) { Enabled = false });
        }

        _status.DropDownItems.Add(new Forms.ToolStripSeparator());
        var checkAgain = new Forms.ToolStripMenuItem("Check again");
        checkAgain.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        _status.DropDownItems.Add(checkAgain);

        _startWithWindows.Checked = ReadStartupEnabled();
    }

    private TrayMenuModel CurrentModel() => TrayMenuModel.Create(_settings(), _registry.Unavailable, _version);

    private void OnTriggerClicked(TriggerMode mode)
    {
        if (mode == CurrentModel().SelectedTrigger)
        {
            return;
        }

        TriggerModeChanged?.Invoke(this, mode);
        RefreshMenuState(); // Shows the new mode once WP-11 has applied it; otherwise keeps showing the old one.
    }

    private bool ReadStartupEnabled()
    {
        try
        {
            return _startup.IsEnabled();
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void OnStartWithWindowsClicked()
    {
        var enable = !ReadStartupEnabled();
        try
        {
            if (enable)
            {
                _startup.Enable();
            }
            else
            {
                _startup.Disable();
            }
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Notify(NotificationKind.Warning, TrayMenuModel.AppName, "Couldn't change the Start with Windows setting.");
        }

        _startWithWindows.Checked = ReadStartupEnabled();
    }

    /// <summary>Opens one of the app's own files with its default app. Explorer does the opening (no UseShellExecute).</summary>
    private void OpenAppFile(string path, string what)
    {
        if (!File.Exists(path))
        {
            Notify(NotificationKind.Warning, TrayMenuModel.AppName, TrayMenuModel.MissingFileText(what));
            return;
        }

        StartExplorer(new ProcessStartInfo("explorer.exe") { ArgumentList = { path } });
    }

    /// <summary>The only Process.Start outside ProcessRunner (WP-05 req. 2): explorer.exe, argument list, no shell.</summary>
    private void StartExplorer(ProcessStartInfo startInfo)
    {
        try
        {
            using var process = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Notify(NotificationKind.Error, TrayMenuModel.AppName, "Couldn't open File Explorer.");
        }
    }

    private static Drawing.Icon? LoadEmbeddedIcon()
    {
        try
        {
            using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream(IconResourceName);
            // Ask for the small-icon size so Windows doesn't scale down the 256 px image (blurry in the tray).
            return stream is null ? null : new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return null; // Fall back to SystemIcons.Application (req. 1).
        }
    }
}
