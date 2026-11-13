using WheelConvert.Core.Formats;
using Xunit;

namespace WheelConvert.Converters.Media.Tests;

/// <summary>Acceptance test 1: the exact argument list for every target; input and output are separate elements.</summary>
public sealed class FfmpegArgumentsTests
{
    private static readonly string Input = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "in dir", "clip ü.mov"));
    private static readonly string Output = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "in dir", ".~wc-0123.mp4"));

    private static readonly string[] Prefix =
        ["-hide_banner", "-nostdin", "-loglevel", "info", "-nostats", "-progress", "pipe:2", "-n", "-i"];

    public static TheoryData<string, string[]> Expected => new()
    {
        { "audio.mp3", ["-vn", "-map", "0:a:0", "-c:a", "libmp3lame", "-q:a", "2"] },
        { "video.extract-mp3", ["-vn", "-map", "0:a:0", "-c:a", "libmp3lame", "-q:a", "2"] },
        { "audio.m4a", ["-vn", "-map", "0:a:0", "-c:a", "aac", "-b:a", "192k"] },
        { "audio.flac", ["-vn", "-map", "0:a:0", "-c:a", "flac"] },
        { "audio.wav", ["-vn", "-map", "0:a:0", "-c:a", "pcm_s16le"] },
        { "audio.ogg", ["-vn", "-map", "0:a:0", "-c:a", "libvorbis", "-q:a", "5"] },
        { "audio.opus", ["-vn", "-map", "0:a:0", "-c:a", "libopus", "-b:a", "128k"] },
        {
            "video.mp4",
            ["-c:v", "libx264", "-preset", "medium", "-crf", "23", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart"]
        },
        { "video.webm", ["-c:v", "libvpx-vp9", "-crf", "32", "-b:v", "0", "-c:a", "libopus", "-b:a", "128k"] },
        {
            "video.gif",
            ["-vf", "fps=12,scale='min(720,iw)':-2:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse", "-loop", "0"]
        },
    };

    [Theory]
    [MemberData(nameof(Expected))]
    public void Builds_exact_list(string targetId, string[] codec)
    {
        var expected = Prefix.Append(Input).Concat(codec).Append(Output).ToArray();

        var actual = FfmpegArguments.For(targetId, Input, Output);

        Assert.Equal(expected, actual);
        Assert.Single(actual, a => a == Input);
        Assert.Equal(Output, actual[^1]);
    }

    [Fact]
    public void Every_supported_target_has_arguments()
    {
        var converter = new MediaConverter(() => null);
        Assert.Equal(converter.SupportedTargetIds.OrderBy(x => x), FfmpegArguments.TargetIds.OrderBy(x => x));
        Assert.All(FfmpegArguments.TargetIds, id => Assert.NotNull(TargetCatalog.Find(id)));
    }

    [Fact]
    public void Gif_filter_is_one_element_with_its_quotes_intact()
    {
        var args = FfmpegArguments.For("video.gif", Input, Output);
        var vf = args.ToList().IndexOf("-vf");

        Assert.Equal("fps=12,scale='min(720,iw)':-2:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse", args[vf + 1]);
        Assert.DoesNotContain(args, a => a.Contains('"', StringComparison.Ordinal));
    }

    [Fact]
    public void Input_named_like_an_option_becomes_a_full_path()
    {
        // Relative "-x.wav" (as a careless caller might pass it) must never reach FFmpeg bare.
        var args = FfmpegArguments.For("audio.mp3", "-x.wav", "out.mp3");

        Assert.DoesNotContain("-x.wav", args);
        var input = args[args.ToList().IndexOf("-i") + 1];
        Assert.Equal(Path.GetFullPath("-x.wav"), input);
        Assert.True(Path.IsPathFullyQualified(input));
        Assert.False(input.StartsWith('-'));
        Assert.True(Path.IsPathFullyQualified(args[^1]));
    }

    [Theory]
    [InlineData("image.png")]
    [InlineData("pdf.reverse")]
    [InlineData("nope")]
    public void Unknown_target_throws(string targetId) =>
        Assert.Throws<ArgumentException>(() => FfmpegArguments.For(targetId, Input, Output));
}
