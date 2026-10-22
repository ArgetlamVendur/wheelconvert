using System.Runtime.InteropServices;

namespace WheelConvert.App.Interop;

/// <summary>Window styles, placement and monitor/DPI queries for the wheel. Owned by WP-04.</summary>
internal static partial class NativeMethods
{
    /// <summary>nIndex for the extended window style.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getwindowlongptrw</remarks>
    internal const int GWL_EXSTYLE = -20;

    /// <summary>Clicking or showing the window never activates it, so Explorer keeps focus.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/winmsg/extended-window-styles</remarks>
    internal const nint WS_EX_NOACTIVATE = 0x08000000;

    /// <summary>Tool window: not in the taskbar or Alt+Tab.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/winmsg/extended-window-styles</remarks>
    internal const nint WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos</summary>
    internal const uint SWP_NOSIZE = 0x0001;

    /// <summary>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos</summary>
    internal const uint SWP_NOZORDER = 0x0004;

    /// <summary>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos</summary>
    internal const uint SWP_NOACTIVATE = 0x0010;

    /// <summary>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-monitorfrompoint</summary>
    internal const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    /// <summary>MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI: the DPI that accounts for the user's scaling setting.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/shellscalingapi/ne-shellscalingapi-monitor_dpi_type</remarks>
    internal const int MDT_EFFECTIVE_DPI = 0;

    /// <summary>
    /// 64-bit only export (the App is win-x64). Returns 0 on failure; GetLastError tells failure from a real 0.
    /// </summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getwindowlongptrw</remarks>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    /// <summary>64-bit only export. Returns the previous value, or 0 on failure (check GetLastError).</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowlongptrw</remarks>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    /// <summary>Moves a window in physical pixels (this process is per-monitor DPI aware, see app.manifest).</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos</remarks>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    /// <summary>With MONITOR_DEFAULTTONEAREST never returns null. The doc mentions no GetLastError.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-monitorfrompoint</remarks>
    [LibraryImport("user32.dll")]
    internal static partial nint MonitorFromPoint(POINT pt, uint dwFlags);

    /// <summary>The doc mentions no GetLastError, so SetLastError is not set.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getmonitorinfow</remarks>
    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    /// <summary>Returns an HRESULT (S_OK = 0). Windows 8.1+.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/shellscalingapi/nf-shellscalingapi-getdpiformonitor</remarks>
    [LibraryImport("shcore.dll")]
    internal static partial int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    /// <summary>https://learn.microsoft.com/windows/win32/api/windef/ns-windef-rect</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        /// <summary>LONG left.</summary>
        public int Left;

        /// <summary>LONG top.</summary>
        public int Top;

        /// <summary>LONG right (exclusive).</summary>
        public int Right;

        /// <summary>LONG bottom (exclusive).</summary>
        public int Bottom;
    }

    /// <summary>https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-monitorinfo</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MONITORINFO
    {
        /// <summary>DWORD cbSize: must be set to sizeof(MONITORINFO) before the call.</summary>
        public uint cbSize;

        /// <summary>RECT rcMonitor: the whole monitor, in virtual-screen coordinates.</summary>
        public RECT rcMonitor;

        /// <summary>RECT rcWork: the monitor minus the taskbar and docked bars.</summary>
        public RECT rcWork;

        /// <summary>DWORD dwFlags: MONITORINFOF_PRIMARY or 0.</summary>
        public uint dwFlags;
    }
}
