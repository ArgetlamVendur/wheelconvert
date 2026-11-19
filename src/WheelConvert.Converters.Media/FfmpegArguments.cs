using WheelConvert.Core.Formats;

namespace WheelConvert.Converters.Media;

/// <summary>
/// Builds FFmpeg argument lists (docs/modules/WP-07-media-converter.md req. 2). Pure: no I/O, no process.
/// Every value is its own list element; ProcessRunner hands them to ArgumentList, so no string is ever re-parsed
/// by a shell, and file names (untrusted) can't inject options.
/// </summary>
public static class FfmpegArguments
{
    /// <summary>GIF filter graph. One element: the single quotes are FFmpeg filter syntax (they protect the comma), not shell quoting.</summary>
    public const string GifFilter = "fps=12,scale='min(720,iw)':-2:flags=lanczos,split[a][b];[a]palettegen[p];[b][p]paletteuse";

    /// <summary>
    /// "Smaller" video filter (WP-16): short edge to at most 720, never upscaled, both sides even (yuv420p/H.264 need even sides).
    /// Landscape (iw &gt; ih): height 720, width -2 (keeps the ratio, rounds to even). Portrait or square: width 720, height -2.
    /// At or below 720 the size is kept, rounded down to even (trunc(x/2)*2). One element: the single quotes are FFmpeg filter
    /// syntax (they protect the commas), not shell quoting. Proven against real FFmpeg in RealFfmpegTests.
    /// </summary>
    public const string SmallerFilter =
        "scale=w='if(gt(min(iw,ih),720),if(gt(iw,ih),-2,720),trunc(iw/2)*2)':h='if(gt(min(iw,ih),720),if(gt(iw,ih),720,-2),trunc(ih/2)*2)':flags=lanczos";

    /// <summary>
    /// Input demuxers FFmpeg may use (<c>-format_whitelist</c>), by FFmpeg's own names from <c>ffmpeg -demuxers</c> (9.0.2).
    /// Only real media demuxers: never hls, concat, ffconcat, image2, tee or any other playlist/meta demuxer (WP-21 req. 2).
    /// mov,mp4,m4a,3gp,3g2,mj2 is one demuxer whose name is a list; FFmpeg matches each part.
    /// .mp4 .mov .m4v .m4a use mov; .mkv .webm use matroska (and webm); .avi use avi; .mp3 use mp3; .wav use wav; .flac use flac;
    /// .ogg .opus use ogg; .aac use aac; .wma .wmv use asf; .aiff .aif use aiff.
    /// </summary>
    public const string InputFormatWhitelist = "mov,mp4,m4a,3gp,3g2,mj2,matroska,webm,avi,mp3,wav,flac,ogg,aac,asf,aiff";

    private static readonly string[] Mp3 = ["-vn", "-map", "0:a:0", "-c:a", "libmp3lame", "-q:a", "2"];

    private static readonly Dictionary<string, string[]> CodecArguments = new(StringComparer.Ordinal)
    {
        [TargetCatalog.AudioMp3.Id] = Mp3,
        [TargetCatalog.VideoExtractMp3.Id] = Mp3,
        [TargetCatalog.AudioM4a.Id] = ["-vn", "-map", "0:a:0", "-c:a", "aac", "-b:a", "192k"],
        [TargetCatalog.AudioFlac.Id] = ["-vn", "-map", "0:a:0", "-c:a", "flac"],
        [TargetCatalog.AudioWav.Id] = ["-vn", "-map", "0:a:0", "-c:a", "pcm_s16le"],
        [TargetCatalog.AudioOgg.Id] = ["-vn", "-map", "0:a:0", "-c:a", "libvorbis", "-q:a", "5"],
        [TargetCatalog.AudioOpus.Id] = ["-vn", "-map", "0:a:0", "-c:a", "libopus", "-b:a", "128k"],
        [TargetCatalog.VideoMp4.Id] =
            ["-c:v", "libx264", "-preset", "medium", "-crf", "23", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart"],
        [TargetCatalog.VideoSmaller.Id] =
            ["-vf", SmallerFilter, "-c:v", "libx264", "-preset", "medium", "-crf", "28", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "96k", "-movflags", "+faststart"],
        [TargetCatalog.VideoWebm.Id] = ["-c:v", "libvpx-vp9", "-crf", "32", "-b:v", "0", "-c:a", "libopus", "-b:a", "128k"],
        [TargetCatalog.VideoGif.Id] = ["-vf", GifFilter, "-loop", "0"],
    };

    /// <summary>Target IDs this class can build arguments for.</summary>
    public static IReadOnlyCollection<string> TargetIds => CodecArguments.Keys;

    /// <summary>
    /// Full argument list: <c>-hide_banner -nostdin -loglevel info -nostats -progress pipe:2 -n -protocol_whitelist file -format_whitelist &lt;list&gt; -i file:&lt;input&gt; … &lt;output&gt;</c>.
    /// Both paths are made absolute, so a file called "-x.wav" can never be read as an option (CONTRACTS C9).
    /// </summary>
    /// <exception cref="ArgumentException">Unknown target, or an empty path.</exception>
    public static IReadOnlyList<string> For(string targetId, string inputPath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (!CodecArguments.TryGetValue(targetId, out var codec))
        {
            throw new ArgumentException($"No FFmpeg arguments for target '{targetId}'.", nameof(targetId));
        }

        var args = new List<string>(20 + codec.Length)
        {
            "-hide_banner", "-nostdin", "-loglevel", "info", "-nostats", "-progress", "pipe:2", "-n",
            // The whitelists go before -i (input options). "file:" makes a name that looks like a protocol read as a plain file.
            "-protocol_whitelist", "file", "-format_whitelist", InputFormatWhitelist,
            "-i", "file:" + Path.GetFullPath(inputPath),
        };
        args.AddRange(codec);
        args.Add(Path.GetFullPath(outputPath));
        return args;
    }
}
