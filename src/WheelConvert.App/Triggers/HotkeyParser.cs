using System.Globalization;
using WheelConvert.App.Interop;

namespace WheelConvert.App.Triggers;

/// <summary>
/// Parses hotkey configuration strings (e.g. "Ctrl+Alt+Space") into Win32 modifier flags and virtual key codes.
/// Owned by WP-03.
/// </summary>
public static class HotkeyParser
{
    /// <summary>
    /// Parses a hotkey string into Win32 modifiers (always including MOD_NOREPEAT) and virtual-key code.
    /// Case-insensitive, '+' separated, whitespace tolerant. Requires at least one modifier and exactly one key.
    /// Supported modifiers: Ctrl, Alt, Shift, Win (and Control, Windows).
    /// Supported keys: A–Z, 0–9, F1–F24, Space, Enter, Return, Tab, Esc, Escape.
    /// </summary>
    public static bool TryParse(string? hotkeyText, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(hotkeyText))
        {
            return false;
        }

        var parts = hotkeyText.Split('+');
        if (parts.Length == 0)
        {
            return false;
        }

        uint parsedModifiers = 0;
        uint parsedVk = 0;
        bool keyFound = false;

        foreach (var rawPart in parts)
        {
            var part = rawPart.Trim();
            if (part.Length == 0)
            {
                return false;
            }

            if (TryMatchModifier(part, out var modifierFlag))
            {
                if ((parsedModifiers & modifierFlag) != 0)
                {
                    // Duplicate modifier is invalid
                    return false;
                }

                parsedModifiers |= modifierFlag;
            }
            else if (TryMatchKey(part, out var vk))
            {
                if (keyFound)
                {
                    // Only one key allowed
                    return false;
                }

                keyFound = true;
                parsedVk = vk;
            }
            else
            {
                // Unrecognized token
                return false;
            }
        }

        if (parsedModifiers == 0 || !keyFound)
        {
            return false;
        }

        modifiers = parsedModifiers | NativeMethods.MOD_NOREPEAT;
        virtualKey = parsedVk;
        return true;
    }

    private static bool TryMatchModifier(string token, out uint flag)
    {
        if (token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("Control", StringComparison.OrdinalIgnoreCase))
        {
            flag = NativeMethods.MOD_CONTROL;
            return true;
        }

        if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase))
        {
            flag = NativeMethods.MOD_ALT;
            return true;
        }

        if (token.Equals("Shift", StringComparison.OrdinalIgnoreCase))
        {
            flag = NativeMethods.MOD_SHIFT;
            return true;
        }

        if (token.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("Windows", StringComparison.OrdinalIgnoreCase))
        {
            flag = NativeMethods.MOD_WIN;
            return true;
        }

        flag = 0;
        return false;
    }

    private static bool TryMatchKey(string token, out uint vk)
    {
        vk = 0;

        // Single character: A-Z or 0-9
        if (token.Length == 1)
        {
            char c = token[0];
            if (c is >= 'a' and <= 'z')
            {
                vk = (uint)(c - 'a' + 0x41);
                return true;
            }

            if (c is >= 'A' and <= 'Z')
            {
                vk = (uint)(c - 'A' + 0x41);
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                vk = (uint)(c - '0' + 0x30);
                return true;
            }

            return false;
        }

        // Named keys
        if (token.Equals("Space", StringComparison.OrdinalIgnoreCase))
        {
            vk = NativeMethods.VK_SPACE;
            return true;
        }

        if (token.Equals("Enter", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("Return", StringComparison.OrdinalIgnoreCase))
        {
            vk = NativeMethods.VK_RETURN;
            return true;
        }

        if (token.Equals("Tab", StringComparison.OrdinalIgnoreCase))
        {
            vk = NativeMethods.VK_TAB;
            return true;
        }

        if (token.Equals("Esc", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("Escape", StringComparison.OrdinalIgnoreCase))
        {
            vk = NativeMethods.VK_ESCAPE;
            return true;
        }

        // Function keys: F1 through F24
        if ((token[0] == 'F' || token[0] == 'f') &&
            int.TryParse(token.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var fIndex))
        {
            if (fIndex is >= 1 and <= 24)
            {
                vk = (uint)(NativeMethods.VK_F1 + (fIndex - 1));
                return true;
            }
        }

        return false;
    }
}
