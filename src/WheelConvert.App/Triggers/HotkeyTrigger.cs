using System.Runtime.InteropServices;
using System.Windows.Interop;
using WheelConvert.App.Interop;
using WheelConvert.Core.Contracts;

namespace WheelConvert.App.Triggers;

/// <summary>
/// Global hotkey trigger (WP-03). Registers a system-wide hotkey via a message-only window
/// and raises <see cref="WheelRequested"/> with the mouse position on press.
/// Fallback for when the Shift-drag hook cannot be used (ADR-006).
/// </summary>
public sealed class HotkeyTrigger : ITrigger
{
    private const int HotkeyId = 0x4357; // Unique ID ('C''W') for this hotkey on our window

    private readonly string _hotkey;
    private HwndSource? _source;
    private bool _isRegistered;
    private bool _disposed;

    /// <param name="hotkey">AppSettings.Hotkey, e.g. "Ctrl+Alt+Space".</param>
    public HotkeyTrigger(string hotkey)
    {
        _hotkey = hotkey;
    }

    public string Name => "Hotkey";

    public string? LastError { get; private set; }

    public event EventHandler<WheelRequestedEventArgs>? WheelRequested;

#pragma warning disable CS0067 // Event is required by ITrigger contract; WP-11 manages closing on repeat press
    public event EventHandler? WheelDismissRequested;
#pragma warning restore CS0067

    /// <summary>
    /// Registers the hotkey on a message-only window. Never throws.
    /// Returns false and sets <see cref="LastError"/> on failure.
    /// </summary>
    public bool Start()
    {
        if (_disposed)
        {
            LastError = "Hotkey trigger has been disposed.";
            return false;
        }

        if (_isRegistered)
        {
            return true;
        }

        if (!HotkeyParser.TryParse(_hotkey, out var modifiers, out var vk))
        {
            LastError = $"Hotkey '{_hotkey}' isn't valid.";
            return false;
        }

        try
        {
            var parameters = new HwndSourceParameters("WheelConvert_HotkeyHost")
            {
                ParentWindow = NativeMethods.HWND_MESSAGE,
            };

            var source = new HwndSource(parameters);
            source.AddHook(HwndHook);
            _source = source;

            if (!NativeMethods.RegisterHotKey(source.Handle, HotkeyId, modifiers, vk))
            {
                var error = Marshal.GetLastWin32Error();
                LastError = error == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED
                    ? $"{_hotkey} is already used by another app."
                    : $"Couldn't register hotkey '{_hotkey}' (error {error}).";

                source.RemoveHook(HwndHook);
                source.Dispose();
                _source = null;
                return false;
            }

            _isRegistered = true;
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Couldn't start hotkey trigger: {ex.Message}";
            Stop();
            return false;
        }
    }

    /// <summary>
    /// Unregisters the hotkey and disposes the message-only window. Idempotent and safe to call multiple times.
    /// </summary>
    public void Stop()
    {
        if (_isRegistered && _source != null && _source.Handle != nint.Zero)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _isRegistered = false;
        }

        if (_source != null)
        {
            _source.RemoveHook(HwndHook);
            _source.Dispose();
            _source = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private nint HwndHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            var screenPoint = NativeMethods.GetCursorPos(out var pt)
                ? new ScreenPoint(pt.x, pt.y)
                : new ScreenPoint(0, 0);

            // Always raise WheelRequested; WP-11 turns a second press into a dismiss if the wheel is open.
            WheelRequested?.Invoke(this, new WheelRequestedEventArgs(screenPoint));
            handled = true;
        }

        return nint.Zero;
    }
}
