using System.Globalization;

namespace WheelConvert.Core.Output;

/// <summary>Standard labels for output names (docs/CONTRACTS.md C2) so every module names files the same way.</summary>
public static class OutputLabels
{
    public const string Rotated = "rotated";
    public const string Reversed = "reversed";
    public const string Reordered = "reordered";
    public const string OddPages = "odd pages";
    public const string EvenPages = "even pages";
    public const string Audio = "audio";

    /// <summary>"page 3", zero-padded to the width of <paramref name="total"/> ("page 03" of 40) so files sort correctly.</summary>
    public static string Page(int pageNumber, int total) => "page " + Pad(pageNumber, total);

    /// <summary>"part 1" / "part 2".</summary>
    public static string Part(int partNumber) => "part " + partNumber.ToString(CultureInfo.InvariantCulture);

    private static string Pad(int number, int total)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        ArgumentOutOfRangeException.ThrowIfLessThan(total, number);
        var width = total.ToString(CultureInfo.InvariantCulture).Length;
        return number.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
    }
}
