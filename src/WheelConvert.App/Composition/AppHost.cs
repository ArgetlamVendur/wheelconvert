using WheelConvert.App.Tray;
using WheelConvert.App.Wheel;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Jobs;

namespace WheelConvert.App.Composition;

/// <summary>Owns every long-lived object; starts and stops them in order. Owned by WP-11.</summary>
public sealed class AppHost : IAsyncDisposable
{
    private readonly IReadOnlyList<ITrigger> _triggers;
    private readonly IWheelView _wheel;
    private readonly ConversionJobQueue _queue;
    private readonly TrayIcon _tray;
    private readonly DropCoordinator _coordinator;
    private readonly ILog _log;

    public AppHost(IReadOnlyList<ITrigger> triggers, IWheelView wheel, ConversionJobQueue queue, TrayIcon tray, DropCoordinator coordinator, ILog log)
    {
        _triggers = triggers;
        _wheel = wheel;
        _queue = queue;
        _tray = tray;
        _coordinator = coordinator;
        _log = log;
    }

    // TODO(WP-11): wire trigger events → wheel; wheel.Dropped → coordinator; tray events; ADR-006 fallback.
    public void Start()
    {
        _ = _wheel;
        _ = _coordinator;
        foreach (var trigger in _triggers)
        {
            if (!trigger.Start())
            {
                _log.Warn($"Trigger '{trigger.Name}' did not start: {trigger.LastError}");
            }
        }
    }

    /// <summary>Starts on the UI thread: UI objects are disposed before the first await.</summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var trigger in _triggers)
        {
            trigger.Dispose();
        }

        _wheel.HideWheel();
        _queue.CancelAll();
        _tray.Dispose(); // NotifyIcon must be disposed on the UI thread, so before the await below.
        await _queue.DisposeAsync().ConfigureAwait(false);
    }
}
