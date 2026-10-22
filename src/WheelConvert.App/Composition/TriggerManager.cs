using System.Runtime.InteropServices;
using System.Windows.Threading;
using WheelConvert.App.Interop;
using WheelConvert.App.Triggers;
using WheelConvert.App.Wheel;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Settings;

namespace WheelConvert.App.Composition;

/// <summary>
/// WP-11 req. 3–5 and ADR-006. Creates and starts the triggers for a <see cref="TriggerMode"/>, falls back to the hotkey
/// when the Shift-drag hook can't be installed, opens/closes the wheel for trigger events, and reinstalls a hook
/// Windows silently removed. UI thread only.
/// </summary>
public sealed class TriggerManager : IDisposable
{
    // On mouse-up the hook's dismiss can arrive before Explorer delivers Drop to the wheel (WP-11 req. 3).
    private static readonly TimeSpan DismissDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan LivenessInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan HookSilenceLimit = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RecentInputWindow = TimeSpan.FromMinutes(1);

    private readonly IWheelView _wheel;
    private readonly INotifier _notifier;
    private readonly Func<AppSettings> _settings;
    private readonly ILog _log;
    private readonly DispatcherTimer _dismissTimer;
    private readonly DispatcherTimer _livenessTimer;
    private readonly List<ITrigger> _running = [];
    private ShiftDragTrigger? _shiftDrag;
    private int _dropCount;
    private int _dropCountAtDismiss;
    private bool _disposed;

    public TriggerManager(IWheelView wheel, INotifier notifier, Func<AppSettings> settings, ILog log, Dispatcher dispatcher)
    {
        _wheel = wheel;
        _notifier = notifier;
        _settings = settings;
        _log = log;
        _dismissTimer = new DispatcherTimer(DismissDelay, DispatcherPriority.Normal, OnDismissTimerTick, dispatcher) { IsEnabled = false };
        _livenessTimer = new DispatcherTimer(LivenessInterval, DispatcherPriority.Background, OnLivenessTimerTick, dispatcher) { IsEnabled = false };
    }

    /// <summary>Stops the current triggers and starts the ones for <paramref name="mode"/>. Takes effect immediately.</summary>
    public void Apply(TriggerMode mode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StopAll();

        var settings = _settings();
        string? shiftDragError = null;
        if (mode is TriggerMode.ShiftDrag or TriggerMode.Both)
        {
            var shiftDrag = new ShiftDragTrigger(settings.DragThresholdPx);
            if (TryStart(shiftDrag))
            {
                _shiftDrag = shiftDrag;
            }
            else
            {
                shiftDragError = shiftDrag.LastError ?? "unknown reason";
            }
        }

        if (mode is TriggerMode.Hotkey or TriggerMode.Both || shiftDragError is not null)
        {
            StartHotkey(settings.Hotkey, shiftDragError);
        }

        _livenessTimer.IsEnabled = _shiftDrag is not null;
        _log.Info($"Trigger mode {mode}: running [{string.Join(", ", _running.Select(t => t.Name))}].");
    }

    /// <summary>The wheel reported a drop: a pending deferred dismiss must not hide it again.</summary>
    public void NotifyDropped() => _dropCount++;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dismissTimer.Stop();
        StopAll();
    }

    /// <param name="shiftDragError">Why Shift-drag failed when this is the ADR-006 fallback; null when the hotkey was chosen.</param>
    private void StartHotkey(string hotkeyText, string? shiftDragError)
    {
        var hotkey = new HotkeyTrigger(hotkeyText);
        var started = TryStart(hotkey);
        if (shiftDragError is null)
        {
            if (!started)
            {
                _notifier.Notify(NotificationKind.Error, UserText.Title, UserText.Fit($"The hotkey isn't available: {hotkey.LastError}"));
            }

            return;
        }

        _log.Warn($"Shift-drag unavailable ({shiftDragError}); hotkey fallback {(started ? "started" : $"failed: {hotkey.LastError}")}.");
        _notifier.Notify(
            started ? NotificationKind.Warning : NotificationKind.Error,
            UserText.Title,
            UserText.Fit(started
                ? $"Shift-drag isn't available on this PC. Use {hotkeyText}."
                : $"Shift-drag and the hotkey aren't available: {hotkey.LastError}"));
    }

    private bool TryStart(ITrigger trigger)
    {
        trigger.WheelRequested += OnWheelRequested;
        trigger.WheelDismissRequested += OnWheelDismissRequested;
        if (trigger.Start())
        {
            _running.Add(trigger);
            return true;
        }

        _log.Warn($"Trigger '{trigger.Name}' did not start: {trigger.LastError}");
        Release(trigger);
        return false;
    }

    private void StopAll()
    {
        _livenessTimer.Stop();
        _shiftDrag = null;
        foreach (var trigger in _running)
        {
            Release(trigger);
        }

        _running.Clear();
    }

    private void Release(ITrigger trigger)
    {
        trigger.WheelRequested -= OnWheelRequested;
        trigger.WheelDismissRequested -= OnWheelDismissRequested;
        trigger.Dispose();
    }

    private void OnWheelRequested(object? sender, WheelRequestedEventArgs e)
    {
        _dismissTimer.Stop();

        // The hotkey doesn't track the wheel (WP-03), so pressing it again toggles here.
        if (_wheel.IsOpen && sender is HotkeyTrigger)
        {
            _wheel.HideWheel();
            return;
        }

        _wheel.ShowAt(e.Point);
    }

    private void OnWheelDismissRequested(object? sender, EventArgs e)
    {
        _dropCountAtDismiss = _dropCount;
        _dismissTimer.Stop();
        _dismissTimer.Start();
    }

    private void OnDismissTimerTick(object? sender, EventArgs e)
    {
        _dismissTimer.Stop();
        if (_dropCount == _dropCountAtDismiss)
        {
            _wheel.HideWheel();
        }
    }

    /// <summary>WP-11 req. 5: Windows silently removes a hook that was too slow; reinstall it.</summary>
    private void OnLivenessTimerTick(object? sender, EventArgs e)
    {
        var shiftDrag = _shiftDrag;
        if (shiftDrag is null
            || DateTime.UtcNow - shiftDrag.LastActivityUtc < HookSilenceLimit
            || !UserInputWithin(RecentInputWindow))
        {
            return;
        }

        shiftDrag.Stop();
        if (shiftDrag.Start())
        {
            _log.Info("Shift-drag hook reinstalled (no hook activity for 10 minutes despite recent input).");
            return;
        }

        _log.Warn($"Shift-drag hook could not be reinstalled: {shiftDrag.LastError}");
        _running.Remove(shiftDrag);
        Release(shiftDrag);
        _shiftDrag = null;
        _livenessTimer.Stop();
        if (!_running.OfType<HotkeyTrigger>().Any())
        {
            StartHotkey(_settings().Hotkey, shiftDrag.LastError ?? "unknown reason");
        }
    }

    private static bool UserInputWithin(TimeSpan window)
    {
        var info = new NativeMethods.LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.LASTINPUTINFO>() };
        if (!NativeMethods.GetLastInputInfo(ref info))
        {
            return false;
        }

        // Both values are GetTickCount-based and wrap every ~49.7 days; unsigned subtraction handles the wrap.
        var idleMs = unchecked((uint)Environment.TickCount - info.dwTime);
        return idleMs < window.TotalMilliseconds;
    }
}
