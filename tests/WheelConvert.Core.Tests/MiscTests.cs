using WheelConvert.Core.Infrastructure;
using WheelConvert.Core.Settings;
using WheelConvert.Core.Wheel;

namespace WheelConvert.Core.Tests;

public static class MiscTests
{
    [Test]
    public static void Wheel_HitTest_FourSegments_AllQuadrants()
    {
        // 4 segments, 0 centred at top, clockwise: 0=top, 1=right, 2=bottom, 3=left.
        Assert.Equal(0, WheelGeometry.HitTest(0, -100, 4, 30, 130));
        Assert.Equal(1, WheelGeometry.HitTest(100, 0, 4, 30, 130));
        Assert.Equal(2, WheelGeometry.HitTest(0, 100, 4, 30, 130));
        Assert.Equal(3, WheelGeometry.HitTest(-100, 0, 4, 30, 130));
        Assert.Equal(0, WheelGeometry.HitTest(-10, -100, 4, 30, 130), "just left of top is still segment 0");
    }

    [Test]
    public static void Wheel_HitTest_CentreOutsideAndEdgeCases()
    {
        Assert.Equal(-1, WheelGeometry.HitTest(0, 0, 4, 30, 130));
        Assert.Equal(-1, WheelGeometry.HitTest(5, 5, 4, 30, 130));
        Assert.Equal(-1, WheelGeometry.HitTest(200, 0, 4, 30, 130));
        Assert.Equal(-1, WheelGeometry.HitTest(50, 0, 0, 30, 130));
        Assert.Equal(-1, WheelGeometry.HitTest(double.NaN, 0, 4, 30, 130));
        Assert.Equal(0, WheelGeometry.HitTest(0, -100, 1, 30, 130));
        Assert.Equal(0, WheelGeometry.HitTest(0, 100, 1, 30, 130));
    }

    [Test]
    public static void Wheel_ArcsTileTheCircle()
    {
        for (var n = 1; n <= 8; n++)
        {
            var total = 0.0;
            for (var i = 0; i < n; i++)
            {
                var (start, sweep) = WheelGeometry.GetSegmentArc(i, n);
                total += sweep;
                var mid = start + (sweep / 2);
                var (x, y) = WheelGeometry.PointOnCircle(mid, 80);
                Assert.Equal(i, WheelGeometry.HitTest(x, y, n, 30, 130), $"n={n} mid of {i}");
            }

            Assert.True(Math.Abs(total - 360) < 1e-9, "sweeps sum to 360");
        }
    }

    [Test]
    public static void Settings_DefaultsWrittenWhenMissing()
    {
        using var dir = new TempDir();
        var store = new JsonSettingsStore(Path.Combine(dir.Path, "settings.json"), new NullLog());
        var settings = store.Load();
        Assert.Equal(TriggerMode.ShiftDrag, settings.Trigger);
        Assert.True(File.Exists(store.FilePath));
    }

    [Test]
    public static void Settings_RoundTrip_AndClamp()
    {
        using var dir = new TempDir();
        var store = new JsonSettingsStore(Path.Combine(dir.Path, "settings.json"), new NullLog());
        store.Save(new AppSettings { Trigger = TriggerMode.Both, MaxConcurrentJobs = 99, DisabledTargetIds = ["image.ico", "image.ico", " "] });
        var loaded = store.Load();
        Assert.Equal(TriggerMode.Both, loaded.Trigger);
        Assert.Equal(8, loaded.MaxConcurrentJobs);
        Assert.SequenceEqual(["image.ico"], loaded.DisabledTargetIds);
        Assert.True(File.ReadAllText(store.FilePath).Contains("\"Both\"", StringComparison.Ordinal), "enums stored as names");
    }

    [Test]
    public static void Settings_CorruptFile_IsKeptAside_AndDefaultsUsed()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "settings.json");
        File.WriteAllText(path, "{ not json");
        var settings = new JsonSettingsStore(path, new NullLog()).Load();
        Assert.Equal(new AppSettings().MaxWheelSegments, settings.MaxWheelSegments);
        Assert.True(Directory.GetFiles(dir.Path, "settings.corrupt-*.json").Length == 1);
    }

    [Test]
    public static void Settings_UnknownEnumAndMissingFields_AreTolerated()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "settings.json");
        File.WriteAllText(path, "{ \"maxWheelSegments\": 1, \"futureSetting\": true }");
        var settings = new JsonSettingsStore(path, new NullLog()).Load();
        Assert.Equal(2, settings.MaxWheelSegments);
        Assert.Equal("Ctrl+Alt+Space", settings.Hotkey);
    }

    [Test]
    public static async Task ProcessRunner_CapturesOutput()
    {
        var (exe, args) = OperatingSystem.IsWindows()
            ? ("cmd.exe", new[] { "/c", "echo hello & echo oops 1>&2" })
            : ("/bin/sh", new[] { "-c", "echo hello; echo oops 1>&2" });
        var result = await ProcessRunner.RunAsync(exe, args, TimeSpan.FromSeconds(10), CancellationToken.None);
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.StdOut.Contains("hello", StringComparison.Ordinal));
        Assert.True(result.StdErr.Contains("oops", StringComparison.Ordinal));
        Assert.False(result.TimedOut);
    }

    [Test]
    public static async Task ProcessRunner_ArgumentsWithSpacesAndQuotes_ArePassedIntact()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // cmd.exe re-parses its arguments; covered by FFmpeg integration tests on Windows (WP-07).
        }

        var result = await ProcessRunner.RunAsync("/bin/sh", ["-c", "printf '%s' \"$1\"", "sh", "a \"quoted\" arg; rm -rf /"], TimeSpan.FromSeconds(10), CancellationToken.None);
        Assert.Equal("a \"quoted\" arg; rm -rf /", result.StdOut.TrimEnd());
    }

    [Test]
    public static async Task ProcessRunner_Timeout_KillsProcess()
    {
        var (exe, args) = OperatingSystem.IsWindows()
            ? ("ping.exe", new[] { "-n", "30", "127.0.0.1" })
            : ("/bin/sleep", new[] { "30" });
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await ProcessRunner.RunAsync(exe, args, TimeSpan.FromMilliseconds(500), CancellationToken.None);
        Assert.True(result.TimedOut);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), "returned promptly");
    }

    [Test]
    public static async Task ProcessRunner_Cancel_Throws()
    {
        var (exe, args) = OperatingSystem.IsWindows()
            ? ("ping.exe", new[] { "-n", "30", "127.0.0.1" })
            : ("/bin/sleep", new[] { "30" });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var threw = false;
        try
        {
            await ProcessRunner.RunAsync(exe, args, TimeSpan.FromSeconds(30), cts.Token);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        Assert.True(threw);
    }

    [Test]
    public static void ToolLocator_FindsConfigured_ThenPath_ThenExtraDirs()
    {
        using var dir = new TempDir();
        using var extra = new TempDir();
        var name = OperatingSystem.IsWindows() ? "faketool.exe" : "faketool";
        var inPath = dir.File(name);
        var inExtra = extra.File(name);

        Assert.Equal(inPath, ToolLocator.Find("faketool", null, dir.Path, []));
        Assert.Equal(inExtra, ToolLocator.Find("faketool", null, "", [extra.Path]));
        Assert.Equal(inExtra, ToolLocator.Find("faketool", inExtra, dir.Path, []));
        Assert.True(ToolLocator.Find("faketool", Path.Combine(dir.Path, "nope"), dir.Path, []) is null, "bad configured path must not fall back silently");
        Assert.True(ToolLocator.Find("faketool", null, "", []) is null);
    }

    [Test]
    public static void FileLog_WritesLines_AndRolls()
    {
        using var dir = new TempDir();
        var log = new FileLog(Path.Combine(dir.Path, "logs", "a.log"));
        log.Info("hello\nworld");
        log.Error("bad", new InvalidOperationException("why"));
        var lines = File.ReadAllLines(log.FilePath);
        Assert.Equal(2, lines.Length);
        Assert.True(lines[0].Contains("INFO hello ⏎ world", StringComparison.Ordinal), "newlines in messages stay on one line");
        Assert.True(lines[1].Contains("InvalidOperationException: why", StringComparison.Ordinal));

        File.WriteAllText(log.FilePath, new string('x', (int)FileLog.MaxBytes + 10));
        log.Info("after roll");
        Assert.True(File.Exists(log.FilePath + ".1"));
        Assert.Equal(1, File.ReadAllLines(log.FilePath).Length);
    }
}
