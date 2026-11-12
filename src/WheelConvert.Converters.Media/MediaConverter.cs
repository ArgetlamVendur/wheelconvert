using System.ComponentModel;
using System.Text.RegularExpressions;
using WheelConvert.Core.Contracts;
using WheelConvert.Core.Formats;
using WheelConvert.Core.Infrastructure;
using WheelConvert.Core.Output;

namespace WheelConvert.Converters.Media;

/// <summary>Seam over <see cref="ProcessRunner.RunAsync"/> so tests can run the converter without FFmpeg installed.</summary>
internal delegate Task<ProcessResult> ToolRunner(
    string executable,
    IReadOnlyList<string> arguments,
    TimeSpan timeout,
    CancellationToken cancellationToken,
    Action<string>? onStdErrLine);

/// <summary>
/// Audio/video conversion through the user's own FFmpeg (ADR-004, docs/modules/WP-07-media-converter.md).
/// FFmpeg is found, never bundled, and only ever started through <see cref="ProcessRunner"/> with an argument list.
/// </summary>
public sealed partial class MediaConverter : IConverter
{
    internal const string InstallHint = "Install FFmpeg: winget install Gyan.FFmpeg";

    private static readonly TimeSpan VideoTimeout = TimeSpan.FromHours(2);
    private static readonly TimeSpan AudioTimeout = TimeSpan.FromMinutes(30);

    private readonly Func<string?> _ffmpegPathProvider;
    private readonly Func<string?, string?> _locate;
    private readonly ToolRunner _run;

    // Written by CheckHealth (worker thread), read by concurrent ConvertAsync calls. Reference/bool writes are atomic.
    private volatile string? _ffmpegPath;
    private volatile bool _checked;

    /// <param name="ffmpegPathProvider">Returns the configured FFmpeg path (AppSettings.FfmpegPath) or null to auto-detect.</param>
    public MediaConverter(Func<string?> ffmpegPathProvider)
        : this(
            ffmpegPathProvider,
            configured => ToolLocator.Find("ffmpeg", configured),
            (exe, args, timeout, ct, onLine) => ProcessRunner.RunAsync(exe, args, timeout, ct, onLine))
    {
    }

    /// <summary>Test constructor: <paramref name="locate"/> replaces the PATH search, <paramref name="run"/> replaces ProcessRunner.</summary>
    internal MediaConverter(Func<string?> ffmpegPathProvider, Func<string?, string?> locate, ToolRunner run)
    {
        _ffmpegPathProvider = ffmpegPathProvider ?? throw new ArgumentNullException(nameof(ffmpegPathProvider));
        _locate = locate ?? throw new ArgumentNullException(nameof(locate));
        _run = run ?? throw new ArgumentNullException(nameof(run));
    }

    public string Id => "media";

    public IReadOnlyCollection<string> SupportedTargetIds { get; } =
    [
        TargetCatalog.AudioMp3.Id, TargetCatalog.AudioM4a.Id, TargetCatalog.AudioFlac.Id,
        TargetCatalog.AudioWav.Id, TargetCatalog.AudioOgg.Id, TargetCatalog.AudioOpus.Id,
        TargetCatalog.VideoMp4.Id, TargetCatalog.VideoWebm.Id, TargetCatalog.VideoGif.Id,
        TargetCatalog.VideoExtractMp3.Id,
    ];

    // Same-format targets (WAV → WAV) are hidden by the registry via ConversionTarget.IsRedundantFor.
    // GIF from a long video can't be refused without probing (no ffprobe, req. 8): the 2 h timeout bounds it.
    public bool CanConvert(string sourceExtension, string targetId)
    {
        var kind = FileClassifier.Classify(sourceExtension);
        var target = TargetCatalog.Find(targetId);
        return target is not null
            && SupportedTargetIds.Contains(targetId)
            && (kind == FileKind.Audio || kind == FileKind.Video)
            && kind == target.InputKind;
    }

    /// <summary>Looks FFmpeg up (file-system checks only; FFmpeg is never started here) and caches the path until the next call.</summary>
    public ConverterHealth CheckHealth()
    {
        string? path;
        try
        {
            path = _locate(_ffmpegPathProvider());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A broken settings provider or an unreadable PATH entry means "not found", never a crash.
            path = null;
        }

        _ffmpegPath = path;
        _checked = true;
        return path is null ? ConverterHealth.Missing(InstallHint) : ConverterHealth.Ready;
    }

    public async Task<ConversionResult> ConvertAsync(
        ConversionRequest request,
        IOutputWriter output,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(output);
        cancellationToken.ThrowIfCancellationRequested();

        var target = request.Target;
        if (!SupportedTargetIds.Contains(target.Id) || target.OutputExtension is not { } ext)
        {
            return ConversionResult.Fail(ConversionError.Unsupported, "This conversion isn't supported for this file.");
        }

        if (!File.Exists(request.SourcePath))
        {
            return ConversionResult.Fail(ConversionError.SourceMissing, "The original file is no longer there.");
        }

        if (!_checked)
        {
            CheckHealth();
        }

        if (_ffmpegPath is not { } ffmpeg)
        {
            return ConversionResult.Fail(ConversionError.ToolMissing, "FFmpeg wasn't found. " + InstallHint);
        }

        var sourceFullPath = Path.GetFullPath(request.SourcePath);
        var label = target.Id == TargetCatalog.VideoExtractMp3.Id ? OutputLabels.Audio : request.OutputLabel;
        var timeout = target.InputKind == FileKind.Video ? VideoTimeout : AudioTimeout;

        string? temp = null;
        var committed = false;
        try
        {
            temp = output.CreateTempPath(sourceFullPath, ext);
            var args = FfmpegArguments.For(target.Id, sourceFullPath, temp);

            var parser = new FfmpegProgressParser();
            void OnLine(string line)
            {
                if (parser.Feed(line) is { } fraction)
                {
                    progress?.Report(fraction);
                }
            }

            var result = await _run(ffmpeg, args, timeout, cancellationToken, OnLine).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (result.TimedOut)
            {
                return ConversionResult.Fail(ConversionError.ToolFailed, "FFmpeg took too long and was stopped.");
            }

            if (result.ExitCode != 0)
            {
                // Not logged: the frozen constructor has no ILog (WP-07 req. 5 can't be met as written; see the PR).
                return MapFailure(result.StdErr, sourceFullPath, RequestedEncoders(args));
            }

            var final = output.Commit(temp, sourceFullPath, ext, label);
            committed = true;
            return ConversionResult.Ok(final);
        }
        catch (OperationCanceledException)
        {
            throw; // The queue reports Cancelled; ProcessRunner already killed FFmpeg.
        }
        catch (Win32Exception)
        {
            // The cached FFmpeg was removed or can't be started since the last health check.
            return ConversionResult.Fail(ConversionError.ToolMissing, "FFmpeg couldn't be started. " + InstallHint);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Includes FileNotFoundException from Commit (FFmpeg exited 0 without writing): CONTRACTS C1.
            return ConversionResult.Fail(ConversionError.OutputFailed, "Couldn't save the converted file here.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ConversionResult.Fail(ConversionError.Unknown, "Something went wrong while converting this file.");
        }
        finally
        {
            if (temp is not null && !committed)
            {
                output.Discard(temp);
            }
        }
    }

    // ProcessRunner's buffer prefixes stderr with this line when it dropped the start; the line after it is cut mid-line.
    private const string TruncationMarker = "[…truncated…]";

    // The only -map value FfmpegArguments passes (audio targets); video-only targets pass none, so they can never match.
    private const string AudioMap = "0:a:0";

    private const string OldInvalidDataSuffix = ": Invalid data found when processing input";

    /// <summary>The encoder names this conversion asked FFmpeg for (the <c>-c:a</c> / <c>-c:v</c> values).</summary>
    internal static IReadOnlyCollection<string> RequestedEncoders(IReadOnlyList<string> arguments)
    {
        var encoders = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i + 1 < arguments.Count; i++)
        {
            if (arguments[i] is "-c:a" or "-c:v")
            {
                encoders.Add(arguments[i + 1]);
            }
        }

        return encoders;
    }

    /// <summary>
    /// Maps a non-zero exit to an error code. Untrusted text (tags, file and folder names, concat entries) reaches
    /// stderr, so the code is decided only by FFmpeg's closing error lines, which nothing from the file can follow:
    /// <list type="bullet">
    /// <item>the last line must be one of FFmpeg's fixed error lines, compared as a whole line (no substring search);</item>
    /// <item>for "missing encoder" and "no audio track" the lines before it must be FFmpeg's whole closing sequence at
    /// fixed positions (4 and 5 lines from the end), each compared whole-line. Text from the file can reach column 0 of
    /// stderr, so it can fake any one such line, but FFmpeg's own error lines always come after it and fill the last
    /// positions, so a file cannot supply the whole sequence;</item>
    /// <item>only blank and progress lines at the very end are ignored; lines in the middle are never removed, because an
    /// attacker-shaped line must not be able to move into a checked position;</item>
    /// <item>a partial first line left by ProcessRunner's truncation is discarded;</item>
    /// <item>an "Unknown encoder" result is reported only for an encoder this conversion requested (<paramref name="requestedEncoders"/>);</item>
    /// <item>anything else is the generic <see cref="ConversionError.ToolFailed"/>; no stderr text is ever copied into a message
    /// (the encoder name shown is the requested one, taken from our own arguments).</item>
    /// </list>
    /// </summary>
    internal static ConversionResult MapFailure(string? stdErr, string sourceFullPath, IReadOnlyCollection<string> requestedEncoders)
    {
        var lines = (stdErr ?? string.Empty).Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        // The line after the marker is a fragment of unknown origin (e.g. the tail of a metadata line that starts at column 0).
        if (lines.Count > 0 && lines[0] == TruncationMarker)
        {
            lines.RemoveRange(0, Math.Min(2, lines.Count));
        }

        // Trailing only: dropping progress-looking lines elsewhere would let a forged line (ending "z=") shift text into a checked position.
        while (lines.Count > 0 && (string.IsNullOrWhiteSpace(lines[^1]) || FfmpegProgressParser.IsProgressLine(lines[^1].Trim())))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            return GenericFailure();
        }

        var last = lines[^1];

        if (last == "Error opening input files: Invalid data found when processing input"
            || IsOldShapeInvalidData(last, sourceFullPath))
        {
            return ConversionResult.Fail(ConversionError.CorruptInput, "This file couldn't be read. It may be damaged.");
        }

        if (last == "Error opening output files: Encoder not found")
        {
            // Four closing lines: "[aost#0:0 @ ptr] Unknown encoder 'x'", "[same prefix] Error selecting an encoder",
            // "Error opening output file <path>.", and the line matched above.
            if (lines.Count >= 4
                && UnknownEncoderPattern().Match(lines[^4]) is { Success: true } unknown
                && SelectingEncoderPattern().Match(lines[^3]) is { Success: true } selecting
                && unknown.Groups[1].Value == selecting.Groups[1].Value
                && lines[^2].StartsWith("Error opening output file ", StringComparison.Ordinal)
                && requestedEncoders.Contains(unknown.Groups[2].Value))
            {
                return ConversionResult.Fail(ConversionError.ToolMissing, $"Your FFmpeg build lacks the {unknown.Groups[2].Value} encoder.");
            }

            return GenericFailure();
        }

        if (last == "Error opening output files: Invalid argument"
            && lines.Count >= 5
            && NoStreamsPattern().IsMatch(lines[^5])
            && lines[^4] == "To ignore this, add a trailing '?' to the map."
            && lines[^3] == "Failed to set value '" + AudioMap + "' for option 'map': Invalid argument"
            && lines[^2].StartsWith("Error parsing options for output file ", StringComparison.Ordinal))
        {
            // "-map 0:a:0" on a file without audio, e.g. a silent screen recording → Audio/MP3.
            return ConversionResult.Fail(ConversionError.Unsupported, "This file has no audio track.");
        }

        return GenericFailure();
    }

    private static ConversionResult GenericFailure() =>
        ConversionResult.Fail(ConversionError.ToolFailed, "FFmpeg could not convert this file.");

    // Older FFmpeg ends with "<input path>: Invalid data found ...". Compared as an exact string against the path we passed
    // (a Windows file name can't contain ':'), never by cutting the path out of the text.
    private static bool IsOldShapeInvalidData(string line, string sourceFullPath) =>
        sourceFullPath.Length > 0
        && (string.Equals(line, sourceFullPath + OldInvalidDataSuffix, StringComparison.OrdinalIgnoreCase)
            || string.Equals(line, sourceFullPath.Replace('\\', '/') + OldInvalidDataSuffix, StringComparison.OrdinalIgnoreCase));

    // Whole-line shapes copied from real FFmpeg output (pointer is "0000024..." on Windows, "0x55..." elsewhere). [0-9] rather than \d: \d also matches non-ASCII digits.
    [GeneratedRegex(@"^\[([a-z]ost#[0-9]+:[0-9]+ @ (?:0x)?[0-9a-fA-F]+)\] Unknown encoder '([A-Za-z0-9_\-]{1,32})'\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UnknownEncoderPattern();

    [GeneratedRegex(@"^\[([a-z]ost#[0-9]+:[0-9]+ @ (?:0x)?[0-9a-fA-F]+)\] Error selecting an encoder\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SelectingEncoderPattern();

    [GeneratedRegex(@"^Stream map '[0-9a-z:]{0,16}' matches no streams\.\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NoStreamsPattern();
}
