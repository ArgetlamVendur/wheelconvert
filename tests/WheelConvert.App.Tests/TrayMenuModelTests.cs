using Microsoft.Win32;
using WheelConvert.App.Tray;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Settings;
using Xunit;
using Forms = System.Windows.Forms;

namespace WheelConvert.App.Tests;

public class TrayMenuModelTests
{
    private static readonly IReadOnlyList<(string, ConverterHealth)> NoneMissing = [];

    private static TrayMenuModel Model(AppSettings settings, params (string, ConverterHealth)[] unavailable) =>
        TrayMenuModel.Create(settings, unavailable, "0.1.0+abc123");

    // --- Trigger radio -------------------------------------------------------------------------------------------

    [Fact]
    public void Default_settings_select_ShiftDrag()
    {
        Assert.Equal(TriggerMode.ShiftDrag, Model(new AppSettings()).SelectedTrigger);
    }

    [Theory]
    [InlineData(TriggerMode.ShiftDrag)]
    [InlineData(TriggerMode.Hotkey)]
    [InlineData(TriggerMode.Both)]
    public void Selected_trigger_is_the_saved_mode(TriggerMode mode)
    {
        Assert.Equal(mode, Model(new AppSettings { Trigger = mode }).SelectedTrigger);
    }

    [Fact]
    public void Unknown_mode_value_falls_back_to_ShiftDrag()
    {
        Assert.Equal(TriggerMode.ShiftDrag, Model(new AppSettings { Trigger = (TriggerMode)99 }).SelectedTrigger);
    }

    // --- How to use ---------------------------------------------------------------------------------------------

    [Fact]
    public void HowTo_in_ShiftDrag_mode_does_not_mention_the_hotkey()
    {
        var text = Model(new AppSettings { Trigger = TriggerMode.ShiftDrag, Hotkey = "Ctrl+Alt+Space" }).HowTo;
        Assert.Contains("Shift", text);
        Assert.DoesNotContain("Ctrl+Alt+Space", text);
    }

    [Fact]
    public void HowTo_in_Hotkey_mode_uses_the_configured_hotkey_and_not_shift_drag()
    {
        var text = Model(new AppSettings { Trigger = TriggerMode.Hotkey, Hotkey = "Ctrl+Shift+F9" }).HowTo;
        Assert.Contains("Ctrl+Shift+F9", text);
        Assert.DoesNotContain("Hold Shift", text);
    }

    [Fact]
    public void HowTo_in_Both_mode_mentions_both()
    {
        var text = Model(new AppSettings { Trigger = TriggerMode.Both, Hotkey = "Win+F2" }).HowTo;
        Assert.Contains("Hold Shift", text);
        Assert.Contains("Win+F2", text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HowTo_with_blank_hotkey_uses_the_default(string hotkey)
    {
        var text = TrayMenuModel.HowToText(new AppSettings { Trigger = TriggerMode.Hotkey, Hotkey = hotkey });
        Assert.Contains(new AppSettings().Hotkey, text);
    }

    [Theory]
    [InlineData(TriggerMode.ShiftDrag)]
    [InlineData(TriggerMode.Hotkey)]
    [InlineData(TriggerMode.Both)]
    public void HowTo_is_at_most_120_characters_even_with_a_huge_hotkey(TriggerMode mode)
    {
        var text = TrayMenuModel.HowToText(new AppSettings { Trigger = mode, Hotkey = new string('X', 500) });
        Assert.InRange(text.Length, 1, TrayMenuModel.MaxUserTextLength);
    }

    // --- Status submenu -----------------------------------------------------------------------------------------

    [Fact]
    public void Status_shows_all_ready_when_nothing_is_missing()
    {
        var model = TrayMenuModel.Create(new AppSettings(), NoneMissing, null);
        Assert.True(model.AllReady);
        Assert.Equal([TrayMenuModel.AllReadyLine], model.StatusLines);
    }

    [Fact]
    public void Missing_ffmpeg_without_configured_path_suggests_winget()
    {
        var model = Model(new AppSettings(), ("media", ConverterHealth.Missing("whatever the converter says")));
        Assert.False(model.AllReady);
        Assert.Equal(["Audio/video: install FFmpeg (winget install Gyan.FFmpeg)"], model.StatusLines);
    }

    [Fact]
    public void Missing_ffmpeg_with_configured_path_says_the_path_was_not_found()
    {
        var model = Model(new AppSettings { FfmpegPath = @"C:\tools\ffmpeg.exe" }, ("media", ConverterHealth.Missing("x")));
        Assert.Equal(["Audio/video: the configured FFmpeg path wasn't found"], model.StatusLines);
    }

    [Fact]
    public void Not_implemented_converter_says_not_available_yet()
    {
        var line = Assert.Single(Model(new AppSettings(), ("qr", ConverterHealth.NotImplemented)).StatusLines);
        Assert.Equal("QR codes: not available in this version yet", line);
    }

    [Fact]
    public void Other_problems_use_the_converter_message()
    {
        var health = new ConverterHealth(false, ConversionError.Unknown, "Couldn't load the PDF engine.");
        Assert.Equal(["PDF: Couldn't load the PDF engine."], Model(new AppSettings(), ("pdf", health)).StatusLines);
    }

    [Fact]
    public void Problem_without_message_and_unknown_id_still_gives_a_line()
    {
        var health = new ConverterHealth(false, ConversionError.Unknown, null);
        Assert.Equal(["newthing: not available"], Model(new AppSettings(), ("newthing", health)).StatusLines);
    }

    [Fact]
    public void One_line_per_unavailable_converter_in_order()
    {
        var model = Model(new AppSettings(), ("media", ConverterHealth.Missing("x")), ("qr", ConverterHealth.NotImplemented));
        Assert.Equal(2, model.StatusLines.Count);
        Assert.StartsWith("Audio/video:", model.StatusLines[0]);
        Assert.StartsWith("QR codes:", model.StatusLines[1]);
    }

    [Fact]
    public void Status_lines_are_at_most_120_characters()
    {
        var health = new ConverterHealth(false, ConversionError.Unknown, new string('m', 400));
        Assert.All(Model(new AppSettings(), ("pdf", health)).StatusLines, l => Assert.True(l.Length <= TrayMenuModel.MaxUserTextLength));
    }

    // --- Header, tooltip, text limits ---------------------------------------------------------------------------

    [Theory]
    [InlineData("0.1.0+abc123def", "WheelConvert 0.1.0")]
    [InlineData("0.1.0", "WheelConvert 0.1.0")]
    [InlineData(null, "WheelConvert")]
    [InlineData("", "WheelConvert")]
    public void Header_shows_name_and_version_without_commit_hash(string? version, string expected)
    {
        Assert.Equal(expected, TrayMenuModel.HeaderText(version));
    }

    [Fact]
    public void Tooltip_name_fits_the_NotifyIcon_limit()
    {
        Assert.Equal("WheelConvert", TrayMenuModel.Fit(TrayMenuModel.AppName, TrayMenuModel.MaxTooltipLength));
        Assert.Equal(TrayMenuModel.MaxTooltipLength, TrayMenuModel.Fit(new string('a', 100), TrayMenuModel.MaxTooltipLength).Length);
    }

    [Fact]
    public void Fit_cuts_with_ellipsis_and_handles_null()
    {
        Assert.Equal("abc", TrayMenuModel.Fit("abc", 3));
        Assert.Equal("ab…", TrayMenuModel.Fit("abcd", 3));
        Assert.Equal(string.Empty, TrayMenuModel.Fit(null, 3));
    }

    [Fact]
    public void Missing_file_text_names_the_file()
    {
        Assert.Equal("The log file doesn't exist yet.", TrayMenuModel.MissingFileText("log file"));
    }

    // --- Notifications ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(NotificationKind.Success, true, true)]
    [InlineData(NotificationKind.Success, false, false)]
    [InlineData(NotificationKind.Warning, false, true)]
    [InlineData(NotificationKind.Error, false, true)]
    [InlineData(NotificationKind.Warning, true, true)]
    public void Success_balloons_respect_the_setting_but_problems_always_show(NotificationKind kind, bool showSuccess, bool expected)
    {
        Assert.Equal(expected, TrayMenuModel.ShouldShow(kind, new AppSettings { ShowSuccessNotifications = showSuccess }));
    }

    [Theory]
    [InlineData(NotificationKind.Success, Forms.ToolTipIcon.Info)]
    [InlineData(NotificationKind.Warning, Forms.ToolTipIcon.Warning)]
    [InlineData(NotificationKind.Error, Forms.ToolTipIcon.Error)]
    public void Notification_kind_maps_to_balloon_icon(NotificationKind kind, Forms.ToolTipIcon expected)
    {
        Assert.Equal(expected, TrayMenuModel.BalloonIcon(kind));
    }

    // --- Embedded icon ------------------------------------------------------------------------------------------

    [Fact]
    public void App_embeds_the_icon_with_16_32_48_and_256_px_images()
    {
        using var stream = typeof(TrayIcon).Assembly.GetManifestResourceStream("WheelConvert.App.wheelconvert.ico");
        Assert.NotNull(stream);
        using var reader = new BinaryReader(stream);
        Assert.Equal(0, reader.ReadUInt16()); // reserved
        Assert.Equal(1, reader.ReadUInt16()); // type 1 = icon
        var count = reader.ReadUInt16();
        var sizes = new List<int>();
        for (var i = 0; i < count; i++)
        {
            var width = reader.ReadByte();
            reader.ReadBytes(15);
            sizes.Add(width == 0 ? 256 : width); // ICO stores 256 as 0.
        }

        Assert.Equal([16, 32, 48, 256], sizes.Order());
    }
}

/// <summary>
/// StartupRegistration against a scratch key under HKCU\Software\WheelConvert.Tests, never the real Run key.
/// Kept in this file because only TrayMenuModelTests.cs was approved as a new test file for WP-05.
/// </summary>
public sealed class StartupRegistrationTests : IDisposable
{
    private readonly string _keyPath = $@"Software\WheelConvert.Tests\{Guid.NewGuid():N}";
    private const string ExePath = @"C:\Program Files\WheelConvert\WheelConvert.exe";

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
        using var parent = Registry.CurrentUser.OpenSubKey(@"Software\WheelConvert.Tests", writable: true);
        if (parent is not null && parent.SubKeyCount == 0 && parent.ValueCount == 0)
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\WheelConvert.Tests", throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void Default_targets_the_HKCU_Run_value_WheelConvert()
    {
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", StartupRegistration.RunKeyPath);
        Assert.Equal("WheelConvert", StartupRegistration.DefaultValueName);
    }

    [Fact]
    public void Enable_writes_the_quoted_exe_path_and_Disable_removes_it()
    {
        var reg = new StartupRegistration(_keyPath, "WheelConvert", ExePath);
        Assert.False(reg.IsEnabled());

        reg.Enable();
        Assert.True(reg.IsEnabled());
        using (var key = Registry.CurrentUser.OpenSubKey(_keyPath))
        {
            Assert.Equal($"\"{ExePath}\"", key?.GetValue("WheelConvert"));
        }

        reg.Disable();
        Assert.False(reg.IsEnabled());
        using (var key = Registry.CurrentUser.OpenSubKey(_keyPath))
        {
            Assert.Null(key?.GetValue("WheelConvert"));
        }
    }

    [Fact]
    public void Disable_when_nothing_is_there_does_not_throw()
    {
        new StartupRegistration(_keyPath, "WheelConvert", ExePath).Disable();
    }

    [Fact]
    public void Value_from_an_older_copy_elsewhere_reads_as_off_and_Enable_replaces_it()
    {
        using (var key = Registry.CurrentUser.CreateSubKey(_keyPath))
        {
            key.SetValue("WheelConvert", @"""D:\old\WheelConvert.exe""");
        }

        var reg = new StartupRegistration(_keyPath, "WheelConvert", ExePath);
        Assert.False(reg.IsEnabled());
        reg.Enable();
        Assert.True(reg.IsEnabled());
    }

    [Fact]
    public void Unknown_exe_path_is_off_and_Enable_throws_InvalidOperation()
    {
        var reg = new StartupRegistration(_keyPath, "WheelConvert", null);
        Assert.False(reg.IsEnabled());
        Assert.Throws<InvalidOperationException>(reg.Enable);
    }
}
