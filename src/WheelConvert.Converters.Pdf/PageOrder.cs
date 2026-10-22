using System.Globalization;

namespace WheelConvert.Converters.Pdf;

/// <summary>Parses <c>OptionKeys.PageOrder</c> ("3,1,2", 1-based) per CONTRACTS C6.</summary>
internal static class PageOrder
{
    /// <summary>
    /// True when every entry is a page number in 1..<paramref name="pageCount"/>, none repeats and there is at least one.
    /// A subset is valid (other pages are left out). <paramref name="pageIndices"/> is 0-based, in the new order.
    /// </summary>
    public static bool TryParse(string? text, int pageCount, out int[] pageIndices)
    {
        pageIndices = [];
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split(',');
        var seen = new HashSet<int>();
        var result = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            // NumberStyles.None: digits only, so "+1", "-1", "1e2" and "0x1" are rejected.
            if (!int.TryParse(parts[i].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var page)
                || page < 1 || page > pageCount || !seen.Add(page))
            {
                return false;
            }

            result[i] = page - 1;
        }

        pageIndices = result;
        return true;
    }
}
