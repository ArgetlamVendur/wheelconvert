using System.ComponentModel;
using System.Diagnostics;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Infrastructure;
using WheelConvert.Core.Output;
using Xunit;

namespace WheelConvert.Converters.Media.Tests;

/// <summary>Converter behaviour with a fake FFmpeg runner: runs everywhere, including CI without FFmpeg.</summary>
public sealed class MediaConverterTests : IDisposable
{
    private static readonly string FakeExeName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private string FakeFfmpeg()
    {
        var bin = Path.Combine(_folder.Path, "bin");
        Directory.CreateDirectory(bin);
        var exe = Path.Combine(bin, FakeExeName);
        File.WriteAllBytes(exe, []);
        return exe;
    }

    private MediaConverter Converter(FakeRunner runner, string? ffmpegPath = null) =>
        new(() => null, _ => ffmpegPath ?? FakeFfmpeg(), runner.RunAsync);

    private string Source(string name = "tone.wav") => _folder.CreateFile(name, [0x52, 0x49, 0x46, 0x46, 9, 9, 9]);

    // ---- Acceptance test 2: CheckHealth with a fake PATH ----

    [Fact]
    public void Health_is_missing_with_install_hint_when_ffmpeg_is_not_on_path()
    {
        var emptyDir = Path.Combine(_folder.Path, "empty");
        Directory.CreateDirectory(emptyDir);
        // A configured path that does not exist yields "missing" without any PATH/registry/winget lookup,
        // so this holds on machines where FFmpeg is installed.
        var converter = new MediaConverter(() => Path.Combine(_folder.Path, "nowhere", FakeExeName), c => ToolLocator.Find("ffmpeg", c, emptyDir, []), new FakeRunner().RunAsync);

        var health = converter.CheckHealth();

        Assert.False(health.IsAvailable);
        Assert.Equal(ConversionError.ToolMissing, health.Reason);
        Assert.Equal("Install FFmpeg: winget install Gyan.FFmpeg", health.Message);
    }

    [Fact]
    public void Health_is_ready_with_fake_path_fast_and_never_runs_ffmpeg()
    {
        var exe = FakeFfmpeg();
        var runner = new FakeRunner();
        var converter = new MediaConverter(() => null, c => ToolLocator.Find("ffmpeg", c, Path.GetDirectoryName(exe), []), runner.RunAsync);
        converter.CheckHealth(); // warm-up (JIT)

        var sw = Stopwatch.StartNew();
        var health = converter.CheckHealth();
        sw.Stop();

        Assert.True(health.IsAvailable);
        Assert.Equal(0, runner.Calls);
        Assert.True(sw.ElapsedMilliseconds < 50, $"CheckHealth took {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Configured_path_that_does_not_exist_is_missing()
    {
        var converter = new MediaConverter(
            () => Path.Combine(_folder.Path, "nowhere", FakeExeName),
            c => ToolLocator.Find("ffmpeg", c, string.Empty, []),
            new FakeRunner().RunAsync);

        Assert.False(converter.CheckHealth().IsAvailable);
    }

    [Fact]
    public void Throwing_settings_provider_means_missing_not_crash()
    {
        var converter = new MediaConverter(() => throw new InvalidOperationException("settings"), c => "x", new FakeRunner().RunAsync);

        Assert.False(converter.CheckHealth().IsAvailable);
    }

    [Fact]
    public async Task Path_is_cached_until_next_CheckHealth()
    {
        var exe = FakeFfmpeg();
        var dir = Path.GetDirectoryName(exe);
        var runner = new FakeRunner();
        var converter = new MediaConverter(() => null, c => ToolLocator.Find("ffmpeg", c, dir, []), runner.RunAsync);
        Assert.True(converter.CheckHealth().IsAvailable);

        File.Delete(exe); // test's own temp folder
        var result = await converter.ConvertAsync(ConversionRequest.Create(Source(), TargetCatalog.AudioFlac), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(exe, runner.Executable);
        Assert.False(converter.CheckHealth().IsAvailable); // "Check again" picks up the change without restart
    }

    [Fact]
    public async Task Convert_without_ffmpeg_is_tool_missing_and_writes_nothing()
    {
        var runner = new FakeRunner();
        var converter = new MediaConverter(() => null, _ => null, runner.RunAsync);
        var source = Source();

        var result = await converter.ConvertAsync(ConversionRequest.Create(source, TargetCatalog.AudioMp3), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.Equal(ConversionError.ToolMissing, result.Error);
        Assert.Equal(0, runner.Calls);
        Assert.Single(Directory.GetFiles(_folder.Path));
    }

    // ---- Acceptance test 5 (CI part): cancellation discards the temp ----

    [Fact]
    public async Task Cancel_mid_conversion_throws_and_discards_temp()
    {
        var runner = new FakeRunner { BlockUntilCancelled = true };
        var source = Source();
        var hash = TestFolder.Hash(source);
        using var cts = new CancellationTokenSource();

        var task = Converter(runner).ConvertAsync(ConversionRequest.Create(source, TargetCatalog.AudioMp3), new SafeOutputWriter(), null, cts.Token);
        await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(_folder.TempFiles()); // the "FFmpeg" really wrote a partial temp
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Empty(_folder.TempFiles());
        Assert.Equal(hash, TestFolder.Hash(source));
        Assert.Equal(new[] { source }, Directory.GetFiles(_folder.Path));
    }

    [Fact]
    public async Task Already_cancelled_token_throws_before_anything_runs()
    {
        var runner = new FakeRunner();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Converter(runner).ConvertAsync(ConversionRequest.Create(Source(), TargetCatalog.AudioMp3), new SafeOutputWriter(), null, cts.Token));
        Assert.Equal(0, runner.Calls);
        Assert.Empty(_folder.TempFiles());
    }

    // ---- Untrusted file names ----

    [Fact]
    public async Task Input_named_dash_x_reaches_ffmpeg_as_full_path()
    {
        var runner = new FakeRunner();
        var source = Source("-x.wav");

        var result = await Converter(runner).ConvertAsync(ConversionRequest.Create(source, TargetCatalog.AudioMp3), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.True(result.Success);
        var args = runner.Arguments!;
        Assert.DoesNotContain("-x.wav", args);
        Assert.Equal(Path.GetFullPath(source), args[args.ToList().IndexOf("-i") + 1]);
        Assert.True(Path.IsPathFullyQualified(args[^1]));
        Assert.Equal(Path.Combine(_folder.Path, "-x (converted).mp3"), result.OutputPaths.Single());
    }

    [Fact]
    public async Task File_name_containing_error_text_does_not_decide_the_error()
    {
        var source = Source("Invalid data found.wav");
        var runner = new FakeRunner
        {
            Result = new ProcessResult(1, string.Empty, $"Input #0, wav, from '{Path.GetFullPath(source)}':\nConversion failed!\n", false),
        };

        var result = await Converter(runner).ConvertAsync(ConversionRequest.Create(source, TargetCatalog.AudioMp3), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.Equal(ConversionError.ToolFailed, result.Error);
    }

    // ---- Success path, labels, timeouts, progress ----

    [Fact]
    public async Task Success_commits_with_label_and_leaves_source_alone()
    {
        var runner = new FakeRunner();
        var source = Source();
        var hash = TestFolder.Hash(source);
        var request = new ConversionRequest(source, TargetCatalog.AudioFlac, new Dictionary<string, string> { [OptionKeys.OutputLabel] = "flac copy" });

        var result = await Converter(runner).ConvertAsync(request, new SafeOutputWriter(), null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(Path.Combine(_folder.Path, "tone (flac copy).flac"), result.OutputPaths.Single());
        Assert.Equal(hash, TestFolder.Hash(source));
        Assert.Empty(_folder.TempFiles());
        Assert.Equal(TimeSpan.FromMinutes(30), runner.Timeout);
    }

    [Fact]
    public async Task Extract_mp3_uses_audio_label_and_video_timeout()
    {
        var runner = new FakeRunner();
        var source = _folder.CreateFile("clip.mov", [0, 0, 0, 1]);
        var request = new ConversionRequest(source, TargetCatalog.VideoExtractMp3, new Dictionary<string, string> { [OptionKeys.OutputLabel] = "ignored" });

        var result = await Converter(runner).ConvertAsync(request, new SafeOutputWriter(), null, CancellationToken.None);

        Assert.Equal(Path.Combine(_folder.Path, "clip (audio).mp3"), result.OutputPaths.Single());
        Assert.Equal(TimeSpan.FromHours(2), runner.Timeout);
    }

    [Fact]
    public async Task Progress_is_reported_from_stderr_lines()
    {
        var runner = new FakeRunner
        {
            StdErrLines =
            [
                "  Duration: 00:00:04.00, start: 0.000000, bitrate: 1411 kb/s",
                "out_time_us=1000000",
                "progress=continue",
                "out_time_us=N/A",
                "out_time_us=3000000",
                "progress=end",
            ],
        };
        var progress = new ProgressLog();

        await Converter(runner).ConvertAsync(ConversionRequest.Create(Source(), TargetCatalog.AudioMp3), new SafeOutputWriter(), progress, CancellationToken.None);

        Assert.Equal(new[] { 0.25, 0.75 }, progress.Values);
    }

    [Fact]
    public async Task Junk_stderr_lines_never_fail_the_conversion()
    {
        // Req. 4: progress parsing must never make a conversion fail.
        var runner = new FakeRunner { StdErrLines = ["Duration: 99:99:99", "out_time_us=abc", "\u0000=\u0000"] };

        var result = await Converter(runner).ConvertAsync(ConversionRequest.Create(Source(), TargetCatalog.AudioMp3), new SafeOutputWriter(), new ProgressLog(), CancellationToken.None);

        Assert.True(result.Success);
    }

    // ---- Failure mapping (req. 5) ----

    public static TheoryData<string, ConversionError, string> Failures => new()
    {
        { "x.wav: Invalid data found when processing input\n", ConversionError.CorruptInput, "This file couldn't be read. It may be damaged." },
        { "frame=1\n[aost#0:0 @ 0x1] Unknown encoder 'libmp3lame'\nError selecting an encoder\n", ConversionError.ToolMissing, "Your FFmpeg build lacks the libmp3lame encoder." },
        { "Unknown encoder 'evil encoder with a very long name that goes on and on'\n", ConversionError.ToolMissing, "Your FFmpeg build lacks a needed encoder." },
        { "Stream map '0:a:0' matches no streams.\n", ConversionError.Unsupported, "This file has no audio track." },
        { "Conversion failed!\nout_time_us=1\nprogress=end\n", ConversionError.ToolFailed, "FFmpeg could not convert this file." },
        { "", ConversionError.ToolFailed, "FFmpeg could not convert this file." },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task Nonzero_exit_maps_error_and_discards_temp(string stderr, ConversionError error, string message)
    {
        var runner = new FakeRunner { Result = new ProcessResult(1, string.Empty, stderr, false) };
        var source = Source();

        var result = await Converter(runner).ConvertAsync(ConversionRequest.Create(source, TargetCatalog.AudioMp3), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(error, result.Error);
        Assert.Equal(message, result.Message);
        Assert.True(message.Length <= 120);
        Assert.Empty(_folder.TempFiles());
        Assert.Equal(new[] { source }, Directory.GetFiles(_folder.Path));
    }

    [Fact]
    public async Task Timeout_is_tool_failed_and_discards_temp()
    {
        var runner = new FakeRunner { Result = new ProcessResult(-1, string.Empty, string.Empty, TimedOut: true) };

        var result = await Converter(runner).ConvertAsync(ConversionRequest.Create(Source(), TargetCatalog.AudioMp3), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.Equal(ConversionError.ToolFailed, result.Error);
        Assert.Empty(_folder.TempFiles());
    }

    [Fact]
    public async Task Ffmpeg_that_cannot_start_is_tool_missing()
    {
        var runner = new FakeRunner { Throw = new Win32Exception(2, "The system cannot find the file specified.") };

        var result = await Converter(runner).ConvertAsync(ConversionRequest.Create(Source(), TargetCatalog.AudioMp3), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.Equal(ConversionError.ToolMissing, result.Error);
        Assert.Empty(_folder.TempFiles());
    }

    [Fact]
    public async Task Exit_zero_without_output_is_output_failed()
    {
        var runner = new FakeRunner { WriteOutput = null };

        var result = await Converter(runner).ConvertAsync(ConversionRequest.Create(Source(), TargetCatalog.AudioMp3), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.Equal(ConversionError.OutputFailed, result.Error);
        Assert.Empty(_folder.TempFiles());
    }

    [Fact]
    public async Task Missing_source_is_source_missing()
    {
        var runner = new FakeRunner();

        var result = await Converter(runner).ConvertAsync(
            ConversionRequest.Create(Path.Combine(_folder.Path, "gone.wav"), TargetCatalog.AudioMp3), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.Equal(ConversionError.SourceMissing, result.Error);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task Non_media_target_is_unsupported()
    {
        var result = await Converter(new FakeRunner()).ConvertAsync(
            ConversionRequest.Create(Source(), TargetCatalog.ImagePng), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.Equal(ConversionError.Unsupported, result.Error);
    }

    // ---- CanConvert ----

    [Theory]
    [InlineData(".wav", "audio.mp3", true)]
    [InlineData(".mp3", "audio.flac", true)]
    [InlineData(".mov", "video.mp4", true)]
    [InlineData(".mp4", "video.extract-mp3", true)]
    [InlineData(".mp4", "video.gif", true)]
    [InlineData(".wav", "video.mp4", false)]
    [InlineData(".mov", "audio.mp3", false)]
    [InlineData(".png", "audio.mp3", false)]
    [InlineData(".wav", "image.png", false)]
    [InlineData(".wav", "nope", false)]
    public void CanConvert_matches_kinds(string ext, string targetId, bool expected) =>
        Assert.Equal(expected, new MediaConverter(() => null).CanConvert(ext, targetId));
}
