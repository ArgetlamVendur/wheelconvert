namespace WheelConvert.Core.Contracts;

/// <summary>Broad category of an input file, decided by extension only (never by content sniffing).</summary>
public enum FileKind
{
    Unsupported = 0,
    Image,
    Audio,
    Video,
    Pdf,
}
