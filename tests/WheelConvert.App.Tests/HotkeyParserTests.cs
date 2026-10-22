using WheelConvert.App.Triggers;
using Xunit;

namespace WheelConvert.App.Tests;

public class HotkeyParserTests
{
    // Win32 RegisterHotKey modifier constants
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    // Win32 Virtual Key constants
    private const uint VkTab = 0x09;
    private const uint VkReturn = 0x0D;
    private const uint VkEscape = 0x1B;
    private const uint VkSpace = 0x20;
    private const uint VkF1 = 0x70;
    private const uint VkF12 = 0x7B;
    private const uint VkF24 = 0x87;

    [Theory]
    [InlineData("Ctrl+Alt+Space", ModControl | ModAlt | ModNoRepeat, VkSpace)]
    [InlineData("ctrl+alt+space", ModControl | ModAlt | ModNoRepeat, VkSpace)]
    [InlineData("  Ctrl  +  Alt  +  Space  ", ModControl | ModAlt | ModNoRepeat, VkSpace)]
    [InlineData("Shift+Enter", ModShift | ModNoRepeat, VkReturn)]
    [InlineData("SHIFT+RETURN", ModShift | ModNoRepeat, VkReturn)]
    [InlineData("Alt+Tab", ModAlt | ModNoRepeat, VkTab)]
    [InlineData("Ctrl+Esc", ModControl | ModNoRepeat, VkEscape)]
    [InlineData("Ctrl+Escape", ModControl | ModNoRepeat, VkEscape)]
    [InlineData("Win+F1", ModWin | ModNoRepeat, VkF1)]
    [InlineData("Windows+F12", ModWin | ModNoRepeat, VkF12)]
    [InlineData("Control+F24", ModControl | ModNoRepeat, VkF24)]
    [InlineData("Ctrl+A", ModControl | ModNoRepeat, 0x41)]
    [InlineData("Ctrl+z", ModControl | ModNoRepeat, 0x5A)]
    [InlineData("Alt+0", ModAlt | ModNoRepeat, 0x30)]
    [InlineData("Alt+9", ModAlt | ModNoRepeat, 0x39)]
    [InlineData("Ctrl+Shift+Alt+Win+Space", ModControl | ModShift | ModAlt | ModWin | ModNoRepeat, VkSpace)]
    public void TryParse_ValidInputs_ReturnsTrueAndCorrectFlags(string input, uint expectedModifiers, uint expectedVk)
    {
        var result = HotkeyParser.TryParse(input, out var modifiers, out var vk);

        Assert.True(result);
        Assert.Equal(expectedModifiers, modifiers);
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Space")]                  // No modifier
    [InlineData("Ctrl+")]                  // Empty key
    [InlineData("+Ctrl+Space")]            // Leading separator
    [InlineData("Ctrl++Space")]            // Double separator
    [InlineData("Ctrl+Space+")]            // Trailing separator
    [InlineData("Ctrl+Foo")]               // Unknown key
    [InlineData("Ctrl")]                   // Only modifier
    [InlineData("Ctrl+Alt")]               // Only modifiers
    [InlineData("Ctrl+A+B")]               // Multiple keys
    [InlineData("Ctrl+Ctrl+Space")]        // Duplicate modifier
    [InlineData("Ctrl+Alt+F0")]            // F0 out of range
    [InlineData("Ctrl+Alt+F25")]           // F25 out of range
    [InlineData("Ctrl+Alt+!")]             // Unsupported character
    public void TryParse_InvalidInputs_ReturnsFalseAndZeroOuts(string? input)
    {
        var result = HotkeyParser.TryParse(input, out var modifiers, out var vk);

        Assert.False(result);
        Assert.Equal(0u, modifiers);
        Assert.Equal(0u, vk);
    }

    [Fact]
    public void HotkeyTrigger_InvalidHotkey_StartFailsWithoutThrowing()
    {
        using var trigger = new HotkeyTrigger("Invalid+Hotkey+String");

        var started = trigger.Start();

        Assert.False(started);
        Assert.NotNull(trigger.LastError);
        Assert.Contains("isn't valid", trigger.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HotkeyTrigger_StopAndDispose_AreIdempotent()
    {
        using var trigger = new HotkeyTrigger("Ctrl+Alt+Space");

        // Calling Stop or Dispose multiple times before or after Start must never throw
        trigger.Stop();
        trigger.Stop();
        trigger.Dispose();
        trigger.Dispose();
    }
}
