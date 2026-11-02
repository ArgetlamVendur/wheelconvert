using System.Runtime.InteropServices;

namespace WheelConvert.App.Interop;

/// <summary>Low-level mouse hook, window-under-cursor and key-state APIs. Owned by WP-02.</summary>
internal static partial class NativeMethods
{
    /// <summary>idHook for a global low-level mouse hook.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowshookexw</remarks>
    internal const int WH_MOUSE_LL = 14;

    /// <summary>nCode value: wParam and lParam contain information about a mouse message.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/winmsg/lowlevelmouseproc</remarks>
    internal const int HC_ACTION = 0;

    /// <summary>https://learn.microsoft.com/windows/win32/inputdev/wm-mousemove</summary>
    internal const int WM_MOUSEMOVE = 0x0200;

    /// <summary>https://learn.microsoft.com/windows/win32/inputdev/wm-lbuttondown</summary>
    internal const int WM_LBUTTONDOWN = 0x0201;

    /// <summary>https://learn.microsoft.com/windows/win32/inputdev/wm-lbuttonup</summary>
    internal const int WM_LBUTTONUP = 0x0202;

    /// <summary>GetAncestor flag: the root window, found by walking the chain of parent windows.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getancestor</remarks>
    internal const uint GA_ROOT = 2;

    /// <summary>SHIFT key.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes</remarks>
    internal const int VK_SHIFT = 0x10;

    /// <summary>LowLevelMouseProc callback. Must return CallNextHookEx's result for every message we don't swallow (we swallow none).</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/winmsg/lowlevelmouseproc</remarks>
    internal delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);

    /// <summary>https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-msllhookstruct</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MSLLHOOKSTRUCT
    {
        /// <summary>POINT pt: cursor position in per-monitor-aware screen coordinates (physical pixels here).</summary>
        public POINT pt;

        /// <summary>DWORD mouseData: wheel delta / X button; unused for left button and move.</summary>
        public uint mouseData;

        /// <summary>DWORD flags: LLMHF_INJECTED etc. Deliberately not filtered (WP-02 req. 7: touchpads inject).</summary>
        public uint flags;

        /// <summary>DWORD time: message time stamp.</summary>
        public uint time;

        /// <summary>ULONG_PTR dwExtraInfo: pointer-sized, so offset 24 on x64.</summary>
        public nuint dwExtraInfo;
    }

    /// <summary>
    /// Installs a hook. lpfn is a function pointer from Marshal.GetFunctionPointerForDelegate; the caller keeps the
    /// delegate alive. Returns 0 on failure (check GetLastError).
    /// </summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowshookexw</remarks>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    internal static partial nint SetWindowsHookEx(int idHook, nint lpfn, nint hmod, uint dwThreadId);

    /// <summary>Removes a hook installed by SetWindowsHookEx. Returns false on failure (check GetLastError).</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-unhookwindowshookex</remarks>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnhookWindowsHookEx(nint hhk);

    /// <summary>Passes the hook information to the next hook. The doc mentions no GetLastError.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-callnexthookex</remarks>
    [LibraryImport("user32.dll")]
    internal static partial nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    /// <summary>With null, returns the handle of the exe module. Returns 0 on failure (check GetLastError).</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/libloaderapi/nf-libloaderapi-getmodulehandlew</remarks>
    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandle(string? lpModuleName);

    /// <summary>Window at a screen point, or 0. The doc mentions no GetLastError.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-windowfrompoint</remarks>
    [LibraryImport("user32.dll")]
    internal static partial nint WindowFromPoint(POINT Point);

    /// <summary>Ancestor of a window (GA_ROOT = top-level window), or 0. The doc mentions no GetLastError.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getancestor</remarks>
    [LibraryImport("user32.dll")]
    internal static partial nint GetAncestor(nint hwnd, uint gaFlags);

    /// <summary>
    /// Copies the window's class name into lpClassName (truncated to nMaxCount incl. the null terminator).
    /// Returns the number of characters copied, or 0 on failure (check GetLastError).
    /// </summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getclassnamew</remarks>
    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int GetClassName(nint hWnd, [Out] char[] lpClassName, int nMaxCount);

    /// <summary>High bit (0x8000) set = key is down right now. The doc mentions no GetLastError.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getasynckeystate</remarks>
    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int vKey);
}
