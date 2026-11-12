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
                return MapFailure(result.StdErr, sourceFullPath, temp);
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

    // Encoders this converter itself passes to FFmpeg (FfmpegArguments). Only these names may appear in a user message.
    private static readonly HashSet<string> KnownEncoders = new(StringComparer.Ordinal)
    {
        "libmp3lame", "aac", "flac", "pcm_s16le", "libvorbis", "libopus", "libx264", "libvpx-vp9",
    };

    // FFmpeg prints its own diagnostics last. Only this many trailing lines are searched, so the input dump
    // (file metadata, paths) printed earlier can't steer the result.
    private const int DiagnosticLineCount = 6;

    /// <summary>
    /// Maps a non-zero exit to an error code from FFmpeg's own diagnostic lines. Untrusted text (title tags, file and
    /// folder names) also reaches stderr, so: the source path, temp output path and their folder are removed first,
    /// progress lines and indented lines (FFmpeg indents metadata and stream details) are skipped, only the last few
    /// remaining lines are searched, and stderr text is never copied into the message except an encoder name from
    /// <see cref="KnownEncoders"/>.
    /// </summary>
    internal static ConversionResult MapFailure(string? stdErr, string sourceFullPath, string? tempFullPath = null)
    {
        var text = stdErr ?? string.Empty;
        var folder = Path.GetDirectoryName(sourceFullPath);
        foreach (var path in new[] { sourceFullPath, tempFullPath, folder }.OfType<string>().Where(p => p.Length > 0).OrderByDescending(p => p.Length))
        {
            text = text
                .Replace(path, string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(path.Replace('\\', '/'), string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        text = string.Join(
            '\n',
            text.Split('\n')
                .Where(l => l.Length > 0 && !char.IsWhiteSpace(l[0]))
                .Where(l => !FfmpegProgressParser.IsProgressLine(l.Trim()))
                .TakeLast(DiagnosticLineCount));

        var encoder = UnknownEncoderPattern().Match(text);
        if (encoder.Success && KnownEncoders.Contains(encoder.Groups[1].Value))
        {
            return ConversionResult.Fail(ConversionError.ToolMissing, $"Your FFmpeg build lacks the {encoder.Groups[1].Value} encoder.");
        }

        if (text.Contains("Unknown encoder", StringComparison.Ordinal))
        {
            return ConversionResult.Fail(ConversionError.ToolMissing, "Your FFmpeg build lacks a needed encoder.");
        }

        if (text.Contains("Invalid data found", StringComparison.Ordinal))
        {
            return ConversionResult.Fail(ConversionError.CorruptInput, "This file couldn't be read. It may be damaged.");
        }

        if (text.Contains("matches no streams", StringComparison.Ordinal))
        {
            // "-map 0:a:0" on a file without audio, e.g. a silent screen recording → Audio/MP3.
            return ConversionResult.Fail(ConversionError.Unsupported, "This file has no audio track.");
        }

        return ConversionResult.Fail(ConversionError.ToolFailed, "FFmpeg could not convert this file.");
    }

    // Only a short identifier is admitted, and then only if it is in KnownEncoders; stderr text never reaches the user.
    [GeneratedRegex(@"Unknown encoder '([A-Za-z0-9_\-]{1,32})'", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UnknownEncoderPattern();
}
