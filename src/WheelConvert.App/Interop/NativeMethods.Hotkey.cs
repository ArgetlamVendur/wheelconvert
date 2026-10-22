using System.Runtime.InteropServices;

namespace WheelConvert.App.Interop;

/// <summary>Win32 hotkey and mouse cursor APIs owned by WP-03.</summary>
internal static partial class NativeMethods
{
    /// <summary>Special HWND value to create a message-only window.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/winmsg/window-features#message-only-windows</remarks>
    internal static readonly nint HWND_MESSAGE = new(-3);

    /// <summary>Posted when the user presses a hot key registered by the RegisterHotKey function.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/inputdev/wm-hotkey</remarks>
    internal const int WM_HOTKEY = 0x0312;

    /// <summary>The hot key is already registered.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/debug/system-error-codes--1300-1699-</remarks>
    internal const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    /// <summary>Either ALT key must be held down.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey</remarks>
    internal const uint MOD_ALT = 0x0001;

    /// <summary>Either CTRL key must be held down.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey</remarks>
    internal const uint MOD_CONTROL = 0x0002;

    /// <summary>Either SHIFT key must be held down.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey</remarks>
    internal const uint MOD_SHIFT = 0x0004;

    /// <summary>Either WINDOWS key was held down.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey</remarks>
    internal const uint MOD_WIN = 0x0008;

    /// <summary>Changes the hotkey behavior so that the keyboard auto-repeat does not generate multiple WM_HOTKEY messages.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey</remarks>
    internal const uint MOD_NOREPEAT = 0x4000;

    /// <summary>TAB key.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes</remarks>
    internal const uint VK_TAB = 0x09;

    /// <summary>ENTER key.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes</remarks>
    internal const uint VK_RETURN = 0x0D;

    /// <summary>ESC key.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes</remarks>
    internal const uint VK_ESCAPE = 0x1B;

    /// <summary>SPACEBAR.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes</remarks>
    internal const uint VK_SPACE = 0x20;

    /// <summary>F1 key.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes</remarks>
    internal const uint VK_F1 = 0x70;

    /// <summary>F24 key.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes</remarks>
    internal const uint VK_F24 = 0x87;

    /// <summary>The POINT structure defines the x- and y-coordinates of a point.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/windef/ns-windef-point</remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int x;
        public int y;
    }

    /// <summary>Defines a system-wide hot key.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerhotkey</remarks>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    /// <summary>Frees a hot key previously registered by the calling thread.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-unregisterhotkey</remarks>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint hWnd, int id);

    /// <summary>Retrieves the cursor's position, in screen coordinates.</summary>
    /// <remarks>https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getcursorpos</remarks>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out POINT lpPoint);
}
