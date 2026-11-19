using System.Globalization;

namespace WheelConvert.Core.Jobs;

/// <summary>
/// Orders paths the way people expect ("scan 2" before "scan 10", case-insensitive) by file name, ties broken by
/// full path (ADR-011 section 3). Pure managed code: StrCmpLogicalW is Windows-only and Core is cross-platform.
/// Digit runs are compared as numbers; runs that are numerically equal ("02" and "2") compare equal here and fall
/// through to the full-path tie-break.
/// </summary>
public sealed class NaturalFileNameComparer : IComparer<string>
{
    public static NaturalFileNameComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var byName = CompareNatural(Path.GetFileName(x), Path.GetFileName(y));
        return byName != 0 ? byName : string.CompareOrdinal(x, y);
    }

    private static int CompareNatural(string a, string b)
    {
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (IsDigit(a[i]) && IsDigit(b[j]))
            {
                var startA = i;
                var startB = j;
                while (i < a.Length && IsDigit(a[i]))
                {
                    i++;
                }

                while (j < b.Length && IsDigit(b[j]))
                {
                    j++;
                }

                var numA = a.AsSpan(startA, i - startA).TrimStart('0');
                var numB = b.AsSpan(startB, j - startB).TrimStart('0');
                if (numA.Length != numB.Length)
                {
                    return numA.Length < numB.Length ? -1 : 1;
                }

                var cmp = numA.CompareTo(numB, StringComparison.Ordinal);
                if (cmp != 0)
                {
                    return cmp;
                }
            }
            else
            {
                var cmp = char.ToUpper(a[i], CultureInfo.InvariantCulture).CompareTo(char.ToUpper(b[j], CultureInfo.InvariantCulture));
                if (cmp != 0)
                {
                    return cmp;
                }

                i++;
                j++;
            }
        }

        return (a.Length - i).CompareTo(b.Length - j);
    }

    // ASCII digits only: char.IsDigit also accepts other scripts' digits, which would break the numeric compare.
    private static bool IsDigit(char c) => c is >= '0' and <= '9';
}
