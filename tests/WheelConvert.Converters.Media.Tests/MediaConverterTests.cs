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

    // ---- Untrusted text in stderr (review finding 1, re-review R1) ----
    // Tails below are real FFmpeg output shapes (FFmpeg 9.0.2, captured for the review and again for this fix).

    private static readonly string[] Mp3Encoder = ["libmp3lame"];

    private const string NoStreamsTail =
        "Stream map '' matches no streams.\n" +
        "To ignore this, add a trailing '?' to the map.\n" +
        "Failed to set value '0:a:0' for option 'map': Invalid argument\n" +
        "Error parsing options for output file out.mp3.\n" +
        "Error opening output files: Invalid argument\n";

    private static string MissingEncoderTail(string name, string kind = "aost") =>
        $"[{kind}#0:0 @ 00000244fae69d40] Unknown encoder '{name}'\n" +
        $"[{kind}#0:0 @ 00000244fae69d40] Error selecting an encoder\n" +
        "Error opening output file out.mp3.\n" +
        "Error opening output files: Encoder not found\n";

    private static string Dump(string folder, string titleTag) =>
        $"Input #0, matroska,webm, from '{folder}\\clip.mkv':\n" +
        "  Metadata:\n" +
        $"    title           : {titleTag}\n" +
        "  Duration: 00:00:01.00, start: 0.000000, bitrate: 1000 kb/s\n" +
        "  Stream #0:0: Video: h264, yuv420p, 1280x720, 12 fps\n";

    [Theory]
    [InlineData("Invalid data found")]
    [InlineData("Invalid data found when processing input")]
    [InlineData("Unknown encoder 'pwned'")]
    [InlineData("Unknown encoder 'libmp3lame'")]
    public void Title_tag_text_does_not_decide_the_error(string titleTag)
    {
        var folder = Path.Combine(_folder.Path, "videos");

        var result = MediaConverter.MapFailure(Dump(folder, titleTag) + NoStreamsTail, Path.Combine(folder, "clip.mkv"), Mp3Encoder);

        Assert.Equal(ConversionError.Unsupported, result.Error);
        Assert.Equal("This file has no audio track.", result.Message);
    }

    [Fact]
    public void Title_tag_text_cannot_turn_a_generic_failure_into_another_error()
    {
        var folder = Path.Combine(_folder.Path, "videos");
        var stderr = Dump(folder, "Unknown encoder 'libmp3lame'") + "Conversion failed!\n";

        var result = MediaConverter.MapFailure(stderr, Path.Combine(folder, "clip.mkv"), Mp3Encoder);

        Assert.Equal(ConversionError.ToolFailed, result.Error);
        Assert.Equal("FFmpeg could not convert this file.", result.Message);
    }

    [Theory]
    [InlineData("Invalid data found")]
    [InlineData("Unknown encoder 'libmp3lame'")]
    public void Folder_name_text_does_not_decide_the_error(string folderName)
    {
        var folder = Path.Combine(_folder.Path, folderName);
        var temp = Path.Combine(folder, "clip.wctmp.mp3");
        // FFmpeg echoes the output path in "Output #0" and in its error lines.
        var stderr = Dump(folder, "plain") +
            $"Output #0, mp3, to '{temp}':\n" +
            NoStreamsTail.Replace("out.mp3", temp, StringComparison.Ordinal);

        var result = MediaConverter.MapFailure(stderr, Path.Combine(folder, "clip.mkv"), Mp3Encoder);

        Assert.Equal(ConversionError.Unsupported, result.Error);
    }

    [Fact]
    public void Folder_name_text_does_not_decide_the_error_when_ffmpeg_prints_only_the_folder()
    {
        var folder = Path.Combine(_folder.Path, "Invalid data found");

        var result = MediaConverter.MapFailure($"Conversion failed in {folder}\n", Path.Combine(folder, "clip.mkv"), Mp3Encoder);

        Assert.Equal(ConversionError.ToolFailed, result.Error);
    }

    [Fact]
    public void Real_corrupt_input_diagnostic_is_corrupt_input()
    {
        var source = Path.Combine(_folder.Path, "bad.wav");
        var stderr =
            "[in#0 @ 000002118b862a80] Error opening input: Invalid data found when processing input\n" +
            $"Error opening input file {source}.\n" +
            "Error opening input files: Invalid data found when processing input\n";

        var result = MediaConverter.MapFailure(stderr, source, Mp3Encoder);

        Assert.Equal(ConversionError.CorruptInput, result.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Older_ffmpeg_corrupt_input_line_is_matched_against_the_exact_source_path(bool forwardSlashes)
    {
        var source = Path.Combine(_folder.Path, "bad.wav");
        var printed = forwardSlashes ? source.Replace('\\', '/') : source;

        var ours = MediaConverter.MapFailure($"{printed}: Invalid data found when processing input\n", source, Mp3Encoder);
        var other = MediaConverter.MapFailure($"{printed}x: Invalid data found when processing input\n", source, Mp3Encoder);

        Assert.Equal(ConversionError.CorruptInput, ours.Error);
        Assert.Equal(ConversionError.ToolFailed, other.Error);
    }

    [Fact]
    public void Real_missing_encoder_diagnostic_is_tool_missing_with_the_requested_encoder_name()
    {
        var folder = Path.Combine(_folder.Path, "audio");

        var result = MediaConverter.MapFailure(Dump(folder, "plain") + MissingEncoderTail("libvorbis"), Path.Combine(folder, "tone.wav"), ["libvorbis"]);

        Assert.Equal(ConversionError.ToolMissing, result.Error);
        Assert.Equal("Your FFmpeg build lacks the libvorbis encoder.", result.Message);
    }

    [Fact]
    public void Missing_video_encoder_is_tool_missing_when_one_of_several_requested_encoders()
    {
        var result = MediaConverter.MapFailure(MissingEncoderTail("libx264", "vost"), Path.Combine(_folder.Path, "a.mkv"), ["libx264", "aac"]);

        Assert.Equal(ConversionError.ToolMissing, result.Error);
        Assert.Equal("Your FFmpeg build lacks the libx264 encoder.", result.Message);
    }

    [Theory]
    [InlineData("pwned")]
    [InlineData("libx264")]
    public void Encoder_that_was_not_requested_is_never_echoed_or_reported(string name)
    {
        var result = MediaConverter.MapFailure(MissingEncoderTail(name), Path.Combine(_folder.Path, "a.wav"), Mp3Encoder);

        Assert.Equal(ConversionError.ToolFailed, result.Error);
        Assert.DoesNotContain(name, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Progress_lines_after_the_diagnostics_do_not_hide_them()
    {
        var result = MediaConverter.MapFailure(MissingEncoderTail("libmp3lame") + "out_time=00:00:00.000000\nprogress=end\n", Path.Combine(_folder.Path, "a.wav"), Mp3Encoder);

        Assert.Equal(ConversionError.ToolMissing, result.Error);
    }

    [Fact]
    public void No_audio_track_diagnostic_is_unsupported()
    {
        var folder = Path.Combine(_folder.Path, "videos");

        var result = MediaConverter.MapFailure(Dump(folder, "plain") + NoStreamsTail, Path.Combine(folder, "clip.mkv"), Mp3Encoder);

        Assert.Equal(ConversionError.Unsupported, result.Error);
        Assert.Equal("This file has no audio track.", result.Message);
    }

    // ---- Re-review 2, R2: the "no audio track" result needs FFmpeg's whole closing sequence at fixed positions ----

    [Fact]
    public void Forged_no_streams_line_from_a_track_language_tag_cannot_choose_no_audio_track()
    {
        // Real stderr tail (FFmpeg 9.0.2, audio-only .mkv converted to GIF) when the track language was
        // "x)<LF>Stream map '' matches no streams.<LF>z=". The forged line sits at column 0; the "z=" remainder looks like a progress line.
        const string tail =
            "  Stream #0:0(x)\n" +
            "Stream map '' matches no streams.\n" +
            "z=): Audio: pcm_s16le, 44100 Hz, mono, s16, 705 kb/s\n" +
            "Output #0, gif, to 'C:\\Users\\u\\.~wc-1234.gif':\n" +
            "[out#0/gif @ 000001953b3226c0] Output file does not contain any stream\n" +
            "Error opening output file C:\\Users\\u\\.~wc-1234.gif.\n" +
            "Error opening output files: Invalid argument\n";

        var result = MediaConverter.MapFailure(tail, Path.Combine(_folder.Path, "a.mkv"), []);

        Assert.Equal(ConversionError.ToolFailed, result.Error);
        Assert.Equal("FFmpeg could not convert this file.", result.Message);
    }

    [Fact]
    public void Progress_looking_lines_in_the_middle_are_not_removed_so_they_cannot_shift_a_forged_line_into_place()
    {
        // A forged no-streams line plus a "z=" line, then FFmpeg's closing lines. If "z=" were dropped, the forged line would sit at ^5.
        var stderr = "Stream map '' matches no streams.\nz=): Audio\n" +
            "To ignore this, add a trailing '?' to the map.\n" +
            "Failed to set value '0:a:0' for option 'map': Invalid argument\n" +
            "Error parsing options for output file x.mp3.\n" +
            "Error opening output files: Invalid argument\n";

        Assert.Equal(ConversionError.ToolFailed, MediaConverter.MapFailure(stderr, Path.Combine(_folder.Path, "a.mkv"), Mp3Encoder).Error);
    }

    [Fact]
    public void No_audio_track_needs_the_no_streams_line_exactly_five_from_the_end()
    {
        // Check a3. A window (any of the last few lines) or "at least N lines" would accept the shifted variants.
        var l = NoStreamsTail.TrimEnd('\n').Split('\n');
        Assert.Equal(ConversionError.Unsupported, Map(string.Join('\n', l)).Error);

        Assert.Equal(ConversionError.ToolFailed, Map($"x\nfiller\n{l[1]}\n{l[2]}\n{l[3]}\n{l[4]}").Error);                    // ^5 is not a no-streams line
        Assert.Equal(ConversionError.ToolFailed, Map(string.Join('\n', l.Skip(1))).Error);                                // too few lines
        Assert.Equal(ConversionError.ToolFailed, Map($"x\n{l[0]}\nf1\nf2\n{l[3]}\n{l[4]}").Error);                            // no-streams at ^5, rest filler
        Assert.Equal(ConversionError.ToolFailed, Map($"x\n{l[0]}\n{l[1]}\nf2\n{l[3]}\n{l[4]}").Error);                        // filler at ^3
        Assert.Equal(ConversionError.ToolFailed, Map($"{l[0]}\nf1\n{l[1]}\n{l[2]}\n{l[3]}\n{l[4]}").Error);                   // extra line after the no-streams line
        Assert.Equal(ConversionError.ToolFailed, Map($"x\n{l[0]}\n{l[1]}\n{l[2]}\nf3\n{l[4]}").Error);                        // filler at ^2

        ConversionResult Map(string stderr) => MediaConverter.MapFailure(stderr, Path.Combine(_folder.Path, "a.mkv"), Mp3Encoder);
    }

    [Theory]
    [InlineData("Error opening output files: Cannot allocate memory")]
    [InlineData("Conversion failed!")]
    [InlineData("Error opening output files: Invalid argument.")]
    public void No_audio_track_needs_ffmpegs_fixed_last_line(string lastLine)
    {
        // Check a4. The first four closing lines are right; the last line is not FFmpeg's.
        var stderr = NoStreamsTail.Replace("Error opening output files: Invalid argument", lastLine, StringComparison.Ordinal);

        var result = MediaConverter.MapFailure(stderr, Path.Combine(_folder.Path, "a.mkv"), Mp3Encoder);

        Assert.Equal(ConversionError.ToolFailed, result.Error);
    }

    [Theory]
    [InlineData("To ignore this, add a trailing '?' to the map.", "To ignore this, add a trailing '?' to the map")]
    [InlineData("Failed to set value '0:a:0' for option 'map': Invalid argument", "Failed to set value '0:v:0' for option 'map': Invalid argument")]
    [InlineData("Failed to set value '0:a:0' for option 'map': Invalid argument", "Failed to set value '0:a:0' for option 'map': Invalid argument x")]
    [InlineData("Error parsing options for output file ", "Error opening output file ")]
    public void No_audio_track_rejects_altered_closing_lines(string original, string replacement)
    {
        var stderr = NoStreamsTail.Replace(original, replacement, StringComparison.Ordinal);

        Assert.Equal(ConversionError.ToolFailed, MediaConverter.MapFailure(stderr, Path.Combine(_folder.Path, "a.mkv"), Mp3Encoder).Error);
    }

    [Fact]
    public void Missing_encoder_needs_the_unknown_encoder_line_exactly_four_from_the_end_with_the_same_prefix()
    {
        // Check a2: (1) filler lines around the encoder lines, so "Unknown encoder" is not at ^4; (2) a different pointer or kind in the two lines.
        const string unknown = "[aost#0:0 @ 00000244fae69d40] Unknown encoder 'libmp3lame'\n";
        const string selecting = "[aost#0:0 @ 00000244fae69d40] Error selecting an encoder\n";
        const string closing = "Error opening output file out.mp3.\nError opening output files: Encoder not found\n";
        var path = Path.Combine(_folder.Path, "a.wav");

        Assert.Equal(ConversionError.ToolMissing, MediaConverter.MapFailure(unknown + selecting + closing, path, Mp3Encoder).Error);
        Assert.Equal(ConversionError.ToolFailed, MediaConverter.MapFailure(unknown + "filler\n" + selecting + closing, path, Mp3Encoder).Error);
        Assert.Equal(ConversionError.ToolFailed, MediaConverter.MapFailure(unknown + selecting + "filler\n" + closing, path, Mp3Encoder).Error);
        Assert.Equal(ConversionError.ToolFailed, MediaConverter.MapFailure(unknown + selecting.Replace("9d40", "9d41", StringComparison.Ordinal) + closing, path, Mp3Encoder).Error);
        Assert.Equal(ConversionError.ToolFailed, MediaConverter.MapFailure(unknown + selecting.Replace("aost", "vost", StringComparison.Ordinal) + closing, path, Mp3Encoder).Error);
        Assert.Equal(ConversionError.ToolFailed, MediaConverter.MapFailure(unknown + selecting + closing.Replace("output file", "input file", StringComparison.Ordinal), path, Mp3Encoder).Error);
    }

    [Fact]
    public void Blank_lines_at_the_very_end_are_ignored()
    {
        // N4: whitespace-only trailing lines do not turn a real diagnosis into the generic error.
        var result = MediaConverter.MapFailure(NoStreamsTail + "   \n\n", Path.Combine(_folder.Path, "a.mkv"), Mp3Encoder);

        Assert.Equal(ConversionError.Unsupported, result.Error);
    }

    // ---- Re-review R1: the three demonstrated bypasses ----

    [Theory]
    [InlineData("Unknown encoder 'libx264'", "libx264")]
    [InlineData("Invalid data found", "libx264")]
    [InlineData("Unknown encoder 'flac'", "flac")]
    public void Concat_script_named_like_audio_cannot_decide_the_error(string phrase, string requested)
    {
        // Bypass 1: FFmpeg opens the "song.mp3" text file as an ffconcat script and prints the script's own text
        // in an unindented line. Real tail (the script said: file <phrase>).
        var source = Path.Combine(_folder.Path, "song.mp3");
        var stderr =
            $"[in#0 @ 0000016777b7bc80] Unsafe file name '{phrase}'\n" +
            "[in#0 @ 0000016777ae1740] Error opening input: Operation not permitted\n" +
            $"Error opening input file {source}.\n" +
            "Error opening input files: Operation not permitted\n";

        var result = MediaConverter.MapFailure(stderr, source, [requested]);

        Assert.Equal(ConversionError.ToolFailed, result.Error);
        Assert.Equal("FFmpeg could not convert this file.", result.Message);
    }

    [Theory]
    [InlineData("Unknown encoder 'libmp3lame'", ConversionError.Unsupported)]
    [InlineData("Invalid data found when processing input", ConversionError.Unsupported)]
    public void Truncated_tag_flood_cannot_decide_the_error(string phrase, ConversionError expected)
    {
        // Bypass 2: ProcessRunner keeps the last 64 KB and cuts inside an indented metadata line, so the fragment starts at column 0.
        var folder = Path.Combine(_folder.Path, "videos");
        var stderr =
            "[…truncated…]\n" +
            $"{phrase}. {phrase}. {phrase}\n" +
            $"    comment{new string(' ', 10)}: {phrase}\n" +
            "  Duration: 00:00:01.00, start: 0.000000, bitrate: 1000 kb/s\n" +
            "  Stream #0:0: Video: h264, yuv420p, 1280x720, 12 fps\n" +
            NoStreamsTail;

        var result = MediaConverter.MapFailure(stderr, Path.Combine(folder, "clip.mkv"), Mp3Encoder);

        Assert.Equal(expected, result.Error);
        Assert.Equal("This file has no audio track.", result.Message);
    }

    [Fact]
    public void Partial_first_line_after_the_truncation_marker_is_discarded()
    {
        // If the kept text were only that fragment, it must not be read as a diagnostic.
        var stderr = "[…truncated…]\nError opening input files: Invalid data found when processing input\n";

        var result = MediaConverter.MapFailure(stderr, Path.Combine(_folder.Path, "a.wav"), Mp3Encoder);

        Assert.Equal(ConversionError.ToolFailed, result.Error);
    }

    [Fact]
    public void File_name_that_ffmpeg_echoes_differently_cannot_decide_the_error()
    {
        // Bypass 3: a lone surrogate in the name is printed as U+FFFD, so the printed path is not the path we passed.
        var folder = _folder.Path;
        var stderr =
            "[in#0 @ 0000000000000000] Error opening input: No such file or directory\n" +
            $"Error opening input file {folder}\\� Unknown encoder 'libx264'.mkv.\n" +
            "Error opening input files: No such file or directory\n";

        var result = MediaConverter.MapFailure(stderr, Path.Combine(folder, "\uD800 Unknown encoder 'libx264'.mkv"), ["libx264", "aac"]);

        Assert.Equal(ConversionError.ToolFailed, result.Error);
        Assert.DoesNotContain("libx264", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Lookalike_diagnostic_lines_earlier_in_stderr_are_ignored()
    {
        // Whatever the file makes FFmpeg print earlier, only the closing lines count.
        var stderr = MissingEncoderTail("libmp3lame") + "Error opening input files: Invalid data found when processing input\n" + NoStreamsTail;

        var result = MediaConverter.MapFailure(stderr, Path.Combine(_folder.Path, "a.mkv"), Mp3Encoder);

        Assert.Equal(ConversionError.Unsupported, result.Error);
    }

    [Fact]
    public void Requested_encoders_are_the_codec_arguments_of_the_target()
    {
        var args = FfmpegArguments.For(TargetCatalog.VideoMp4.Id, Path.Combine(_folder.Path, "a.mkv"), Path.Combine(_folder.Path, "a.mp4"));

        Assert.Equal(new[] { "aac", "libx264" }, MediaConverter.RequestedEncoders(args).Order().ToArray());
        Assert.Empty(MediaConverter.RequestedEncoders(FfmpegArguments.For(TargetCatalog.VideoGif.Id, Path.Combine(_folder.Path, "a.mkv"), Path.Combine(_folder.Path, "a.gif"))));
    }

    [Theory]
    [InlineData("libvorbis", ConversionError.ToolMissing)]
    [InlineData("libx264", ConversionError.ToolFailed)]
    public async Task Converter_reports_a_missing_encoder_only_if_it_asked_for_it(string encoder, ConversionError expected)
    {
        var source = Source();
        var runner = new FakeRunner { Result = new ProcessResult(1, string.Empty, MissingEncoderTail(encoder), false) };

        var result = await Converter(runner).ConvertAsync(ConversionRequest.Create(source, TargetCatalog.AudioOgg), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.Equal(expected, result.Error);
        Assert.Empty(_folder.TempFiles());
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
        { "[in#0 @ 0x1] Error opening input: Invalid data found when processing input\nError opening input file x.wav.\nError opening input files: Invalid data found when processing input\n", ConversionError.CorruptInput, "This file couldn't be read. It may be damaged." },
        { "frame=1\n[aost#0:0 @ 0x1] Unknown encoder 'libmp3lame'\n[aost#0:0 @ 0x1] Error selecting an encoder\nError opening output file x.mp3.\nError opening output files: Encoder not found\n", ConversionError.ToolMissing, "Your FFmpeg build lacks the libmp3lame encoder." },
        { "[aost#0:0 @ 0x1] Unknown encoder 'evil'\n[aost#0:0 @ 0x1] Error selecting an encoder\nError opening output file x.mp3.\nError opening output files: Encoder not found\n", ConversionError.ToolFailed, "FFmpeg could not convert this file." },
        { "Stream map '' matches no streams.\nTo ignore this, add a trailing '?' to the map.\nFailed to set value '0:a:0' for option 'map': Invalid argument\nError parsing options for output file x.mp3.\nError opening output files: Invalid argument\n", ConversionError.Unsupported, "This file has no audio track." },
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
        Assert.Equal("FFmpeg took too long and was stopped.", result.Message);
        Assert.Empty(_folder.TempFiles());
    }

    [Fact]
    public async Task Timeout_with_exit_zero_is_still_a_failure_and_commits_nothing()
    {
        // A killed run can report exit code 0; the partial temp file must not be published.
        var runner = new FakeRunner { Result = new ProcessResult(0, string.Empty, string.Empty, TimedOut: true) };
        var source = Source();

        var result = await Converter(runner).ConvertAsync(ConversionRequest.Create(source, TargetCatalog.AudioMp3), new SafeOutputWriter(), null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("FFmpeg took too long and was stopped.", result.Message);
        Assert.Empty(_folder.TempFiles());
        Assert.Equal(new[] { source }, Directory.GetFiles(_folder.Path));
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
