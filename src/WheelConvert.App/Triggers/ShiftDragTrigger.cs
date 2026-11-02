using System.Runtime.InteropServices;
using System.Windows.Threading;
using WheelConvert.App.Interop;
using WheelConvert.Core.Contracts;

namespace WheelConvert.App.Triggers;

/// <summary>
/// Shift-drag trigger (WP-02). A global low-level mouse hook that opens the wheel when the user holds Shift and
/// left-drags something that started over File Explorer or the desktop. Never swallows mouse input.
/// </summary>
public sealed class ShiftDragTrigger : ITrigger
{
    // Top-level window classes of an Explorer folder window and of the desktop (two variants).
    private const string ExplorerClass = "CabinetWClass";
    private const string DesktopClass = "Progman";
    private const string DesktopWorkerClass = "WorkerW";

    // Offsets into MSLLHOOKSTRUCT, computed once so the callback can read pt without marshalling the whole struct.
    private static readonly int PtXOffset = (int)Marshal.OffsetOf<NativeMethods.MSLLHOOKSTRUCT>(nameof(NativeMethods.MSLLHOOKSTRUCT.pt))
        + (int)Marshal.OffsetOf<NativeMethods.POINT>(nameof(NativeMethods.POINT.x));

    private static readonly int PtYOffset = (int)Marshal.OffsetOf<NativeMethods.MSLLHOOKSTRUCT>(nameof(NativeMethods.MSLLHOOKSTRUCT.pt))
        + (int)Marshal.OffsetOf<NativeMethods.POINT>(nameof(NativeMethods.POINT.y));

    private readonly int _dragThresholdPx;

    // Kept in a field for the hook's lifetime: Windows only holds a raw function pointer, so a collected delegate crashes the app.
    private readonly NativeMethods.LowLevelMouseProc _proc;

    // Cached so raising an event doesn't allocate a new delegate each time.
    private readonly Action<ScreenPoint> _raiseWheelRequested;
    private readonly Action _raiseWheelDismissRequested;

    // Reused by the mouse-down class-name check; the callback only runs on the hook thread, so sharing is safe.
    private readonly char[] _classNameBuffer = new char[64];

    private GestureStateMachine _machine;
    private Dispatcher? _dispatcher;
    private nint _hook;
    private bool _disposed;

    public ShiftDragTrigger(int dragThresholdPx)
    {
        _dragThresholdPx = dragThresholdPx;
        _machine = new GestureStateMachine(dragThresholdPx);
        _proc = HookCallback;
        _raiseWheelRequested = RaiseWheelRequested;
        _raiseWheelDismissRequested = RaiseWheelDismissRequested;
    }

    public string Name => "Shift-drag";

    public string? LastError { get; private set; }

    /// <summary>
    /// Last time the hook callback ran (UTC), or when the hook was last installed. WP-11 uses it to detect a hook
    /// Windows silently removed (LowLevelHooksTimeout).
    /// </summary>
    public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;

    public event EventHandler<WheelRequestedEventArgs>? WheelRequested;

    public event EventHandler? WheelDismissRequested;

    /// <summary>
    /// Installs the hook. Must run on the UI thread: WH_MOUSE_LL callbacks arrive through the installing thread's
    /// message loop, which WPF's dispatcher pumps. Never throws.
    /// </summary>
    public bool Start()
    {
        if (_disposed)
        {
            LastError = "Shift-drag has been shut down.";
            return false;
        }

        if (_hook != 0)
        {
            return true;
        }

        try
        {
            // FromThread (not CurrentDispatcher) so a wrong-thread call fails instead of creating a dispatcher that never pumps.
            var dispatcher = Dispatcher.FromThread(Thread.CurrentThread);
            if (dispatcher is null)
            {
                LastError = "Shift-drag could not start (not on the UI thread).";
                return false;
            }

            var module = NativeMethods.GetModuleHandle(null);
            if (module == 0)
            {
                LastError = $"Shift-drag could not start (error {Marshal.GetLastWin32Error()}).";
                return false;
            }

            _machine = new GestureStateMachine(_dragThresholdPx);
            _dispatcher = dispatcher;
            var hook = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_MOUSE_LL, Marshal.GetFunctionPointerForDelegate(_proc), module, 0);
            if (hook == 0)
            {
                LastError = $"Shift-drag could not start (error {Marshal.GetLastWin32Error()}).";
                return false;
            }

            _hook = hook;
            LastError = null;

            // A fresh hook counts as activity, so WP-11's liveness check doesn't reinstall it while the user only types.
            LastActivityUtc = DateTime.UtcNow;
            return true;
        }
        catch (Exception)
        {
            LastError = "Shift-drag could not start.";
            return false;
        }
    }

    /// <summary>Removes the hook. Idempotent.</summary>
    public void Stop()
    {
        var hook = _hook;
        if (hook == 0)
        {
            return;
        }

        _hook = 0;

        // Ignore failure: Windows may already have removed a timed-out hook, and Stop must not throw.
        _ = NativeMethods.UnhookWindowsHookEx(hook);
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

    /// <summary>
    /// Runs for every mouse event system-wide, so it must stay tiny: no I/O, logging or locks (AGENTS.md §4.4).
    /// Every path returns CallNextHookEx; nothing is swallowed. Injected events (LLMHF_INJECTED) are not filtered
    /// because touchpads and tablets inject (req. 7).
    /// </summary>
    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        nint result = 0;
        try
        {
            if (nCode == NativeMethods.HC_ACTION)
            {
                OnMouseMessage((int)wParam, lParam);
            }
        }
        catch (Exception)
        {
            // An exception escaping into user32 would crash the app; a missed gesture is harmless.
        }
        finally
        {
            result = NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        return result;
    }

    private void OnMouseMessage(int message, nint lParam)
    {
        LastActivityUtc = DateTime.UtcNow;

        MouseEvent kind;
        var shiftDown = false;
        var overExplorer = false;
        switch (message)
        {
            case NativeMethods.WM_MOUSEMOVE:
                // Most events are idle moves; skip them before reading anything.
                if (!_machine.IsWaitingForDrag)
                {
                    return;
                }

                kind = MouseEvent.Move;
                shiftDown = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
                break;

            case NativeMethods.WM_LBUTTONDOWN:
                kind = MouseEvent.LeftDown;
                break;

            case NativeMethods.WM_LBUTTONUP:
                kind = MouseEvent.LeftUp;
                break;

            default:
                return;
        }

        // pt is already in physical pixels (PerMonitorV2 manifest); passed through unchanged (CONTRACTS C3).
        var point = new ScreenPoint(Marshal.ReadInt32(lParam, PtXOffset), Marshal.ReadInt32(lParam, PtYOffset));
        if (kind == MouseEvent.LeftDown)
        {
            overExplorer = IsExplorerOrDesktopAt(point);
        }

        var output = _machine.Process(kind, point, shiftDown, overExplorer);
        switch (output.Action)
        {
            case GestureAction.OpenWheel:
                _dispatcher?.BeginInvoke(DispatcherPriority.Normal, _raiseWheelRequested, output.Point);
                break;

            case GestureAction.Dismiss:
                _dispatcher?.BeginInvoke(DispatcherPriority.Normal, _raiseWheelDismissRequested);
                break;
        }
    }

    /// <summary>Req. 4: checked at mouse-down only, nothing cached across drags. The one allowed Win32 lookup in the callback.</summary>
    private bool IsExplorerOrDesktopAt(ScreenPoint point)
    {
        var window = NativeMethods.WindowFromPoint(new NativeMethods.POINT { x = point.X, y = point.Y });
        if (window == 0)
        {
            return false;
        }

        var root = NativeMethods.GetAncestor(window, NativeMethods.GA_ROOT);
        if (root == 0)
        {
            return false;
        }

        var length = NativeMethods.GetClassName(root, _classNameBuffer, _classNameBuffer.Length);
        if (length <= 0)
        {
            return false;
        }

        // Window class names are case-insensitive; compare spans so the check doesn't allocate a string.
        ReadOnlySpan<char> name = _classNameBuffer.AsSpan(0, length);
        return name.Equals(ExplorerClass, StringComparison.OrdinalIgnoreCase)
            || name.Equals(DesktopClass, StringComparison.OrdinalIgnoreCase)
            || name.Equals(DesktopWorkerClass, StringComparison.OrdinalIgnoreCase);
    }

    private void RaiseWheelRequested(ScreenPoint point)
    {
        // Stop() may have run after this was queued; a stopped trigger raises nothing.
        if (_hook != 0)
        {
            WheelRequested?.Invoke(this, new WheelRequestedEventArgs(point));
        }
    }

    private void RaiseWheelDismissRequested()
    {
        if (_hook != 0)
        {
            WheelDismissRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
