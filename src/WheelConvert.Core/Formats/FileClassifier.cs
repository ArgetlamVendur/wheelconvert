using WheelConvert.Core.Contracts;

namespace WheelConvert.Core.Formats;

/// <summary>Extension → <see cref="FileKind"/>, plus extension normalisation and equivalence (jpg = jpeg).</summary>
public static class FileClassifier
{
    private static readonly Dictionary<string, FileKind> KindByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = FileKind.Image, [".jpeg"] = FileKind.Image, [".png"] = FileKind.Image,
        [".webp"] = FileKind.Image, [".heic"] = FileKind.Image, [".heif"] = FileKind.Image,
        [".bmp"] = FileKind.Image, [".tif"] = FileKind.Image, [".tiff"] = FileKind.Image,
        [".gif"] = FileKind.Image, [".ico"] = FileKind.Image, [".avif"] = FileKind.Image,

        [".mp3"] = FileKind.Audio, [".wav"] = FileKind.Audio, [".flac"] = FileKind.Audio,
        [".ogg"] = FileKind.Audio, [".m4a"] = FileKind.Audio, [".aac"] = FileKind.Audio,
        [".opus"] = FileKind.Audio, [".wma"] = FileKind.Audio, [".aiff"] = FileKind.Audio,
        [".aif"] = FileKind.Audio,

        [".mp4"] = FileKind.Video, [".mov"] = FileKind.Video, [".mkv"] = FileKind.Video,
        [".webm"] = FileKind.Video, [".avi"] = FileKind.Video, [".wmv"] = FileKind.Video,
        [".m4v"] = FileKind.Video,

        [".pdf"] = FileKind.Pdf,
    };

    // Each group is one format with several spellings. First entry is canonical.
    private static readonly string[][] EquivalentGroups =
    [
        [".jpg", ".jpeg"],
        [".tif", ".tiff"],
        [".heic", ".heif"],
        [".aif", ".aiff"],
    ];

    /// <summary>Lower-case extension with leading dot, or "" if none. Accepts a path or a bare extension.</summary>
    public static string NormalizeExtension(string pathOrExtension)
    {
        ArgumentNullException.ThrowIfNull(pathOrExtension);
        var trimmed = pathOrExtension.Trim();
        var ext = trimmed.StartsWith('.') && trimmed.IndexOfAny(['/', '\\']) < 0 && trimmed.LastIndexOf('.') == 0
            ? trimmed
            : Path.GetExtension(trimmed);
        return string.IsNullOrEmpty(ext) || ext == "." ? string.Empty : ext.ToLowerInvariant();
    }

    public static FileKind Classify(string path)
    {
        var ext = NormalizeExtension(path);
        return ext.Length > 0 && KindByExtension.TryGetValue(ext, out var kind) ? kind : FileKind.Unsupported;
    }

    /// <summary>True when both extensions are the same format (".JPG" and ".jpeg" → true).</summary>
    public static bool AreEquivalent(string extensionA, string extensionB)
    {
        var a = NormalizeExtension(extensionA);
        var b = NormalizeExtension(extensionB);
        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        if (a == b)
        {
            return true;
        }

        foreach (var group in EquivalentGroups)
        {
            if (Array.IndexOf(group, a) >= 0 && Array.IndexOf(group, b) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyCollection<string> KnownExtensions => KindByExtension.Keys;
}
