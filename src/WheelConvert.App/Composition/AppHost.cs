using WheelConvert.App.Tray;
using WheelConvert.App.Wheel;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Jobs;
using WheelConvert.Core.Settings;

namespace WheelConvert.App.Composition;

/// <summary>Owns every long-lived object; wires their events and starts and stops them in order. Owned by WP-11.</summary>
public sealed class AppHost : IAsyncDisposable
{
    // WP-11 req. 11: the whole shutdown must finish in under 5 s, even if a converter ignores cancellation.
    private static readonly TimeSpan QueueShutdownBudget = TimeSpan.FromSeconds(4);

    private readonly TriggerManager _triggers;
    private readonly IWheelView _wheel;
    private readonly ConversionJobQueue _queue;
    private readonly TrayIcon _tray;
    private readonly DropCoordinator _coordinator;
    private readonly FormatRegistry _registry;
    private readonly SettingsHolder _settings;
    private readonly RecentFolders _recentFolders;
    private readonly ILog _log;
    private bool _started;
    private bool _disposed;

    public AppHost(
        TriggerManager triggers,
        IWheelView wheel,
        ConversionJobQueue queue,
        TrayIcon tray,
        DropCoordinator coordinator,
        FormatRegistry registry,
        SettingsHolder settings,
        RecentFolders recentFolders,
        ILog log)
    {
        _triggers = triggers;
        _wheel = wheel;
        _queue = queue;
        _tray = tray;
        _coordinator = coordinator;
        _registry = registry;
        _settings = settings;
        _recentFolders = recentFolders;
        _log = log;
    }

    /// <summary>For the global exception handlers in App.</summary>
    public INotifier Notifier => _tray;

    /// <summary>UI thread.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _wheel.Dropped += OnWheelDropped;
        _tray.ExitRequested += OnExitRequested;
        _tray.RefreshRequested += OnRefreshRequested;
        _tray.TriggerModeChanged += OnTriggerModeChanged;
        _queue.JobStarted += OnJobStarted;
        _triggers.Apply(_settings.Current.Trigger);

        // ADR-002: remove temps a crashed run left behind. Background: folders may be on slow network drives.
        _ = Task.Run(_recentFolders.CleanUpStaleTemps);
        _log.Info("WheelConvert started.");
    }

    /// <summary>
    /// Call on the UI thread. Everything that must run there (triggers, wheel, tray) is done before the first await,
    /// because App.OnExit blocks the UI thread on this call. So the tray is disposed before the queue finishes
    /// draining; the coordinator is stopped first, so no notification is attempted after that.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _triggers.Dispose();
        _wheel.HideWheel();
        _wheel.Dropped -= OnWheelDropped;
        _coordinator.Stop();
        _queue.JobStarted -= OnJobStarted;
        _queue.CancelAll();
        _tray.ExitRequested -= OnExitRequested;
        _tray.RefreshRequested -= OnRefreshRequested;
        _tray.TriggerModeChanged -= OnTriggerModeChanged;
        _tray.Dispose(); // NotifyIcon must be disposed on the UI thread.

        var queueDisposed = _queue.DisposeAsync().AsTask();
        if (await Task.WhenAny(queueDisposed, Task.Delay(QueueShutdownBudget)).ConfigureAwait(false) != queueDisposed)
        {
            _log.Warn("Shutdown did not wait for the queue any longer; a converter ignored cancellation.");
        }

        _log.Info("WheelConvert stopped.");
    }

    private void OnWheelDropped(object? sender, WheelDropEventArgs e)
    {
        _triggers.NotifyDropped();
        _ = HandleDropAsync(e.Paths, e.Target);
    }

    private async Task HandleDropAsync(IReadOnlyList<string> paths, ConversionTarget target)
    {
        try
        {
            await _coordinator.HandleDropAsync(paths, target);
        }
        catch (ObjectDisposedException) when (_disposed)
        {
            // Dropped while the app was exiting; nothing to convert any more.
        }
        catch (Exception ex)
        {
            _log.Error($"Could not handle a drop on {target.Id}.", ex);
            _tray.Notify(NotificationKind.Error, UserText.Title, "Couldn't start the conversion. Details are in the log.");
        }
    }

    // Worker thread, before the converter creates temp files in the source's folder.
    private void OnJobStarted(object? sender, JobEventArgs e) => _recentFolders.Record(e.Request.SourcePath);

    private void OnExitRequested(object? sender, EventArgs e) => System.Windows.Application.Current.Shutdown();

    private void OnRefreshRequested(object? sender, EventArgs e) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        try
        {
            // Health checks may load native libraries: never on the UI thread.
            await Task.Run(_registry.Refresh);
        }
        catch (Exception ex)
        {
            _log.Error("Converter check failed.", ex);
            _tray.Notify(NotificationKind.Error, UserText.Title, "Couldn't check the converters. Details are in the log.");
            return;
        }

        if (_disposed)
        {
            return;
        }

        // Stubs report NotImplemented: those are not something the user can fix.
        var missing = _registry.Unavailable.Where(u => u.Health.Reason != ConversionError.NotImplemented).ToList();
        if (missing.Count == 0)
        {
            _tray.Notify(NotificationKind.Success, UserText.Title, "Check complete: nothing is missing.");
        }
        else
        {
            var first = missing[0];
            _tray.Notify(NotificationKind.Warning, UserText.Title, UserText.Fit($"{first.ConverterId}: {first.Health.Message ?? "not available."}"));
        }
    }

    private void OnTriggerModeChanged(object? sender, TriggerMode mode)
    {
        if (_disposed)
        {
            return;
        }

        switch (_settings.Update(_settings.Current with { Trigger = mode }))
        {
            case SettingsSaveOutcome.Locked:
                _tray.Notify(NotificationKind.Warning, UserText.Title, "Settings file is locked; change not saved.");
                break;
            case SettingsSaveOutcome.Failed:
                _tray.Notify(NotificationKind.Warning, UserText.Title, "Couldn't save settings; the change lasts until you exit.");
                break;
        }

        _triggers.Apply(mode);
    }
}
