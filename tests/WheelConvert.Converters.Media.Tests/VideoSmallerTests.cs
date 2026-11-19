using System.Text.RegularExpressions;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Infrastructure;
using WheelConvert.Core.Output;
using Xunit;

namespace WheelConvert.Converters.Media.Tests;

/// <summary>WP-16 video.smaller: arguments, size check (fake runner, run everywhere) and acceptance tests 6-8 against real FFmpeg.</summary>
public sealed partial class VideoSmallerTests : IDisposable
{
    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    // ---- Arguments ----

    [Fact]
    public void Arguments_carry_the_exact_preset_and_keep_the_wp21_protections()
    {
        var args = FfmpegArguments.For(TargetCatalog.VideoSmaller.Id, Path.Combine(_folder.Path, "a.mov"), Path.Combine(_folder.Path, "a.mp4"));

        string After(string flag) => args[args.ToList().IndexOf(flag) + 1];
        Assert.Equal("libx264", After("-c:v"));
        Assert.Equal("medium", After("-preset"));
        Assert.Equal("28", After("-crf"));
        Assert.Equal("yuv420p", After("-pix_fmt"));
        Assert.Equal("aac", After("-c:a"));
        Assert.Equal("96k", After("-b:a"));
        Assert.Equal("+faststart", After("-movflags"));
        Assert.Equal(FfmpegArguments.SmallerFilter, After("-vf"));
        Assert.Equal("file", After("-protocol_whitelist"));
        Assert.Equal(FfmpegArguments.InputFormatWhitelist, After("-format_whitelist"));
        Assert.StartsWith("file:", After("-i"), StringComparison.Ordinal);
        Assert.Equal(Path.Combine(_folder.Path, "a.mp4"), args[^1]);
    }

    [Fact]
    public void Converter_supports_video_smaller_for_video_only()
    {
        var converter = new MediaConverter(() => null);
        Assert.Contains(TargetCatalog.VideoSmaller.Id, converter.SupportedTargetIds);
        Assert.True(converter.CanConvert(".mov", TargetCatalog.VideoSmaller.Id));
        Assert.False(converter.CanConvert(".mp3", TargetCatalog.VideoSmaller.Id));
    }

    // ---- 5 % size check and label (fake runner) ----

    private MediaConverter Fake(FakeRunner runner, ILog? log = null)
    {
        var bin = Path.Combine(_folder.Path, "bin");
        Directory.CreateDirectory(bin);
        var exe = Path.Combine(bin, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        File.WriteAllBytes(exe, []);
        return new MediaConverter(() => null, _ => exe, runner.RunAsync, log ?? new ListLog());
    }

    [Theory]
    [InlineData(950, true)] // exactly 5 % smaller counts
    [InlineData(949, true)]
    [InlineData(951, false)]
    [InlineData(2000, false)]
    public async Task Five_percent_rule_decides_commit_or_no_result(int outputSize, bool expectCommit)
    {
        var source = _folder.CreateFile("clip.mp4", new byte[1000]);
        var runner = new FakeRunner { WriteOutput = new byte[outputSize] };
        var log = new ListLog();

        var result = await Fake(runner, log).ConvertAsync(ConversionRequest.Create(source, TargetCatalog.VideoSmaller), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.Equal(1, runner.Calls);
        Assert.Empty(_folder.TempFiles());
        if (expectCommit)
        {
            Assert.True(result.Success, result.Message);
            Assert.Equal(_folder.File("clip (smaller).mp4"), result.OutputPaths.Single());
        }
        else
        {
            Assert.Equal(ConversionError.NoResult, result.Error);
            Assert.Equal("This video is already about as small as it gets.", result.Message);
            Assert.Empty(result.OutputPaths);
            Assert.Equal([source], Directory.GetFiles(_folder.Path));
            Assert.Empty(log.Lines); // not an FFmpeg failure: nothing is logged
        }
    }

    [Fact]
    public async Task Other_video_targets_are_not_subject_to_the_size_check()
    {
        var source = _folder.CreateFile("clip.mkv", new byte[10]);
        var runner = new FakeRunner { WriteOutput = new byte[5000] };

        var result = await Fake(runner).ConvertAsync(ConversionRequest.Create(source, TargetCatalog.VideoMp4), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public async Task Ffmpeg_failure_is_logged_with_file_names_only()
    {
        var source = _folder.CreateFile("clip.mp4", new byte[1000]);
        var stdErr = "Input #0 from 'file:" + source + "':\nError opening output files: Invalid argument";
        var runner = new FakeRunner { WriteOutput = null, Result = new ProcessResult(1, string.Empty, stdErr, TimedOut: false) };
        var log = new ListLog();

        var result = await Fake(runner, log).ConvertAsync(ConversionRequest.Create(source, TargetCatalog.VideoSmaller), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.False(result.Success);
        var line = Assert.Single(log.Lines);
        Assert.DoesNotContain(_folder.Path, line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("clip.mp4", line, StringComparison.Ordinal);
    }

    // ---- Acceptance 6-8 against real FFmpeg ----

    private static MediaConverter Real()
    {
        var converter = new MediaConverter(() => null, c => ToolLocator.Find("ffmpeg", c), (exe, args, timeout, ct, onLine) => ProcessRunner.RunAsync(exe, args, timeout, ct, onLine), new ListLog());
        Assert.True(converter.CheckHealth().IsAvailable);
        return converter;
    }

    /// <summary>A high-bitrate source, so the 720p CRF 28 result is far smaller than 95 % of it.</summary>
    private async Task<string> SourceAsync(string name, int width, int height, string pixFmt = "yuv420p", string codec = "mpeg4")
    {
        var path = _folder.File(name);
        var quality = codec == "libx264" ? new[] { "-crf", "1", "-preset", "ultrafast" } : new[] { "-q:v", "1" };
        await RealFfmpeg.GenerateAsync(
            [
                "-f", "lavfi", "-i", $"testsrc=duration=1:size={width}x{height}:rate=10",
                "-f", "lavfi", "-i", "sine=frequency=440:duration=1",
                "-c:v", codec, .. quality, "-pix_fmt", pixFmt, "-c:a", "aac", "-shortest", path,
            ]);
        return path;
    }

    private static async Task<(int Width, int Height, string Info)> SizeOfAsync(string file)
    {
        var result = await ProcessRunner.RunAsync(
            RealFfmpeg.Path ?? throw new InvalidOperationException(RealFfmpeg.SkipReason),
            ["-hide_banner", "-nostdin", "-i", file],
            TimeSpan.FromMinutes(1),
            CancellationToken.None);
        var match = VideoSizePattern().Match(result.StdErr);
        Assert.True(match.Success, "No video stream line in: " + result.StdErr);
        return (int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), result.StdErr);
    }

    [GeneratedRegex(@"Video: .*?, (\d{2,5})x(\d{2,5})[ ,\[]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VideoSizePattern();

    [FfmpegTheory]
    [InlineData(1920, 1080, 1280, 720)] // acceptance 6
    [InlineData(1080, 1920, 720, 1280)] // acceptance 7: portrait
    [InlineData(1000, 1000, 720, 720)] // square
    [InlineData(1921, 1081, 1280, 720)] // odd landscape source: width rounds to an even number
    [InlineData(1081, 1921, 720, 1280)] // odd portrait source
    public async Task Video_is_scaled_to_a_short_edge_of_720_with_even_sides(int w, int h, int expectedW, int expectedH)
    {
        var source = await SourceAsync("in.mkv", w, h, pixFmt: w % 2 == 1 || h % 2 == 1 ? "yuv444p" : "yuv420p", codec: w % 2 == 1 || h % 2 == 1 ? "libx264" : "mpeg4");
        var before = TestFolder.Hash(source);

        var result = await Real().ConvertAsync(ConversionRequest.Create(source, TargetCatalog.VideoSmaller), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        var output = result.OutputPaths.Single();
        Assert.Equal(_folder.File("in (smaller).mp4"), output);
        var (width, height, info) = await SizeOfAsync(output);
        Assert.Equal((expectedW, expectedH), (width, height));
        Assert.Equal(0, width % 2);
        Assert.Equal(0, height % 2);
        Assert.Contains("h264", info, StringComparison.Ordinal);
        Assert.Contains("aac", info, StringComparison.Ordinal);
        Assert.True(new FileInfo(output).Length < new FileInfo(source).Length * 0.95);
        Assert.True(await RealFfmpeg.DecodesCleanlyAsync(output));
        Assert.Equal(before, TestFolder.Hash(source));
        Assert.Empty(_folder.TempFiles());
    }

    [FfmpegTheory]
    [InlineData(640, 360, 640, 360)] // acceptance 8: not upscaled
    [InlineData(720, 406, 720, 406)] // short edge exactly 720 is untouched
    [InlineData(360, 640, 360, 640)] // portrait, not upscaled
    [InlineData(641, 361, 640, 360)] // odd and small: not enlarged, rounded down to even
    public async Task Video_at_or_below_720_is_never_upscaled(int w, int h, int expectedW, int expectedH)
    {
        var odd = w % 2 == 1 || h % 2 == 1;
        var source = await SourceAsync("in.mkv", w, h, pixFmt: odd ? "yuv444p" : "yuv420p", codec: odd ? "libx264" : "mpeg4");

        var result = await Real().ConvertAsync(ConversionRequest.Create(source, TargetCatalog.VideoSmaller), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        var (width, height, _) = await SizeOfAsync(result.OutputPaths.Single());
        Assert.Equal((expectedW, expectedH), (width, height));
    }

    [FfmpegFact]
    public async Task Already_small_video_gives_no_result_and_leaves_no_temp_file()
    {
        var source = _folder.File("tiny.mp4");
        await RealFfmpeg.GenerateAsync(
            "-f", "lavfi", "-i", "color=c=black:size=64x64:duration=1:rate=5", "-c:v", "libx264", "-crf", "51", "-preset", "veryslow", "-pix_fmt", "yuv420p", "-movflags", "+faststart", source);

        var result = await Real().ConvertAsync(ConversionRequest.Create(source, TargetCatalog.VideoSmaller), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.Equal(ConversionError.NoResult, result.Error);
        Assert.Equal("This video is already about as small as it gets.", result.Message);
        Assert.Empty(_folder.TempFiles());
        Assert.Equal([source], Directory.GetFiles(_folder.Path));
    }
}
