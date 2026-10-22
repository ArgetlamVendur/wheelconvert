using System.Runtime.InteropServices;

namespace WheelConvert.App.Interop;

/// <summary>User-input timing, used by WP-11's hook liveness check. Owned by WP-11.</summary>
internal static partial class NativeMethods
{
    /// <summary>Time of the last input event (keyboard or mouse) in this session.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getlastinputinfo
    /// The doc mentions no GetLastError, so SetLastError is not set.</remarks>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetLastInputInfo(ref LASTINPUTINFO plii);

    /// <summary>https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-lastinputinfo</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct LASTINPUTINFO
    {
        /// <summary>UINT cbSize: must be set to sizeof(LASTINPUTINFO) before the call.</summary>
        public uint cbSize;

        /// <summary>DWORD dwTime: tick count (GetTickCount) of the last input event.</summary>
        public uint dwTime;
    }
}
