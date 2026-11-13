using System.Globalization;

namespace WheelConvert.Converters.Media;

/// <summary>
/// Turns FFmpeg stderr lines (with <c>-progress pipe:2</c>) into a 0..1 fraction: the first <c>Duration: hh:mm:ss.xx</c>
/// line gives the length, each <c>out_time_us=&lt;n&gt;</c> line the position. One instance per conversion; FFmpeg's
/// stderr events arrive one at a time, so no locking. Never throws: progress is a nicety, never a reason to fail.
/// </summary>
public sealed class FfmpegProgressParser
{
    private const string DurationKey = "Duration:";
    private const string OutTimeKey = "out_time_us=";

    /// <summary>Input length in microseconds, once a usable <c>Duration:</c> line was seen.</summary>
    public long? DurationUs { get; private set; }

    /// <summary>Feeds one stderr line. Returns the new fraction (0..1), or null when the line carries no progress.</summary>
    public double? Feed(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }

        var trimmed = line.AsSpan().Trim();
        if (trimmed.StartsWith(OutTimeKey, StringComparison.Ordinal))
        {
            // "N/A" and negative values (before the first frame) carry no position.
            if (DurationUs is not { } duration
                || !long.TryParse(trimmed[OutTimeKey.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var position)
                || position < 0)
            {
                return null;
            }

            return Math.Clamp((double)position / duration, 0.0, 1.0);
        }

        if (DurationUs is null && trimmed.StartsWith(DurationKey, StringComparison.Ordinal))
        {
            DurationUs = ParseDurationUs(trimmed[DurationKey.Length..]);
        }

        return null;
    }

    /// <summary>True for <c>key=value</c> progress lines (<c>frame=</c>, <c>out_time_us=</c>, <c>progress=end</c>…).</summary>
    public static bool IsProgressLine(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        var eq = line.IndexOf('=', StringComparison.Ordinal);
        if (eq <= 0)
        {
            return false;
        }

        for (var i = 0; i < eq; i++)
        {
            var c = line[i];
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Parses " 00:01:02.50, start: …" → microseconds; null for "N/A", zero or anything unexpected.</summary>
    private static long? ParseDurationUs(ReadOnlySpan<char> rest)
    {
        rest = rest.TrimStart();
        var comma = rest.IndexOf(',');
        if (comma >= 0)
        {
            rest = rest[..comma];
        }

        rest = rest.TrimEnd();
        if (!TimeSpan.TryParseExact(rest, @"hh\:mm\:ss\.FFFFFFF", CultureInfo.InvariantCulture, out var length)
            && !TimeSpan.TryParseExact(rest, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out length))
        {
            // FFmpeg prints hours unpadded past 99 (e.g. "123:00:00.00"); hand-parse that rare case.
            if (!TryParseLongHours(rest, out length))
            {
                return null;
            }
        }

        var us = length.Ticks / (TimeSpan.TicksPerMillisecond / 1000);
        return us > 0 ? us : null;
    }

    private static bool TryParseLongHours(ReadOnlySpan<char> text, out TimeSpan length)
    {
        length = default;
        var first = text.IndexOf(':');
        if (first <= 0
            || !int.TryParse(text[..first], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !TimeSpan.TryParseExact(text[(first + 1)..], @"mm\:ss\.FFFFFFF", CultureInfo.InvariantCulture, out var rest))
        {
            return false;
        }

        length = TimeSpan.FromHours(hours) + rest;
        return true;
    }
}
