using Xunit;

namespace WheelConvert.Converters.Media.Tests;

public sealed class FfmpegProgressParserTests
{
    [Fact]
    public void Reports_fraction_from_duration_and_out_time()
    {
        var parser = new FfmpegProgressParser();

        Assert.Null(parser.Feed("Input #0, wav, from 'C:\\a\\b.wav':"));
        Assert.Null(parser.Feed("  Duration: 00:00:10.00, bitrate: 1411 kb/s"));
        Assert.Equal(10_000_000, parser.DurationUs);
        Assert.Null(parser.Feed("frame=12"));
        Assert.Equal(0.25, parser.Feed("out_time_us=2500000"));
        Assert.Equal(1.0, parser.Feed("out_time_us=12000000")); // clamped
    }

    [Fact]
    public void Uses_first_duration_only()
    {
        var parser = new FfmpegProgressParser();
        parser.Feed("  Duration: 01:00:00.50, start: 0.000000, bitrate: 128 kb/s");
        parser.Feed("  Duration: 00:00:01.00, start: 0.000000");

        Assert.Equal(3_600_500_000, parser.DurationUs);
    }

    [Theory]
    [InlineData("  Duration: N/A, start: 0.000000, bitrate: N/A")]
    [InlineData("  Duration: 00:00:00.00, start: 0.000000")]
    [InlineData("  Duration: garbage")]
    [InlineData("  Duration:")]
    public void No_usable_duration_means_no_progress(string durationLine)
    {
        var parser = new FfmpegProgressParser();

        Assert.Null(parser.Feed(durationLine));
        Assert.Null(parser.DurationUs);
        Assert.Null(parser.Feed("out_time_us=500000"));
    }

    [Theory]
    [InlineData("out_time_us=N/A")]
    [InlineData("out_time_us=-23219")]
    [InlineData("out_time_us=")]
    [InlineData("out_time_us=99999999999999999999999")]
    [InlineData(null)]
    [InlineData("")]
    public void Odd_progress_lines_are_ignored(string? line)
    {
        var parser = new FfmpegProgressParser();
        parser.Feed("  Duration: 00:00:10.00, start: 0.000000");

        Assert.Null(parser.Feed(line));
    }

    [Fact]
    public void Hours_past_23_parse()
    {
        var parser = new FfmpegProgressParser();
        parser.Feed("  Duration: 123:04:05.50, start: 0.000000");

        Assert.Equal(((123L * 3600) + (4 * 60) + 5) * 1_000_000 + 500_000, parser.DurationUs);
    }

    [Theory]
    [InlineData("out_time_us=1", true)]
    [InlineData("progress=end", true)]
    [InlineData("frame=12", true)]
    [InlineData("[aac @ 000001] Invalid data found", false)]
    [InlineData("Stream mapping:", false)]
    [InlineData("  encoder         : Lavf60.16.100", false)]
    [InlineData("=x", false)]
    [InlineData("", false)]
    public void Recognises_progress_lines(string line, bool expected) =>
        Assert.Equal(expected, FfmpegProgressParser.IsProgressLine(line));
}
