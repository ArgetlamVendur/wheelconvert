using System.Globalization;
using System.Text;

namespace WheelConvert.Converters.Docx;

/// <summary>Pure text rules of WP-18 (line joining, headings, junk detection). No PDF or Word types, so they test without files.</summary>
internal static class TextRules
{
    /// <summary>A block whose median font size is at least this many times the page's body size is a heading (req. 5).</summary>
    public const double Heading2Ratio = 1.3;
    public const double Heading1Ratio = 1.6;

    /// <summary>Fewer letters per page than this on average means a scan (req. 7).</summary>
    public const double MinLettersPerPage = 20;

    /// <summary>More than this share of junk characters means the PDF has no usable text map (gotchas).</summary>
    public const double MaxJunkShare = 0.30;

    /// <summary>A gap between lines of more than this many body-text heights starts a new paragraph.</summary>
    public const double ParagraphGapRatio = 0.6;

    /// <summary>What to do with the text counts of a whole document: <c>null</c> = fine, otherwise the failure message.</summary>
    public static string? RejectReason(long letters, long chars, long junk, int pages)
    {
        if (chars > 0 && junk > MaxJunkShare * chars)
        {
            return "Couldn't read the text in this PDF.";
        }

        return letters < MinLettersPerPage * pages ? "This PDF looks like a scan: there's no text to copy." : null;
    }

    public static BlockStyle StyleFor(double blockSize, double bodySize)
    {
        if (bodySize <= 0 || blockSize <= 0)
        {
            return BlockStyle.Normal;
        }

        var ratio = blockSize / bodySize;
        return ratio >= Heading1Ratio ? BlockStyle.Heading1 : ratio >= Heading2Ratio ? BlockStyle.Heading2 : BlockStyle.Normal;
    }

    /// <summary>
    /// Joins the lines of one block into a paragraph. A line ending in "-" is merged with the next line without a space
    /// only when the next line starts lower-case ("exam-" + "ple" gives "example"); otherwise the hyphen stays ("well-" + "Known").
    /// </summary>
    public static string JoinLines(IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (sb.Length == 0)
            {
                sb.Append(line);
            }
            else if (sb[^1] == '-' && sb.Length >= 2 && char.IsLetter(sb[^2]) && char.IsLower(line[0]))
            {
                sb.Length--;
                sb.Append(line);
            }
            else
            {
                sb.Append(' ').Append(line);
            }
        }

        return sb.ToString();
    }

    public static double Median(List<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        values.Sort();
        var mid = values.Count / 2;
        return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2;
    }

    /// <summary>True for U+FFFD and private-use characters: what a PDF without a ToUnicode map tends to produce.</summary>
    public static bool IsJunk(string text, int index)
    {
        var c = text[index];
        return c == '�' || char.GetUnicodeCategory(c) == UnicodeCategory.PrivateUse;
    }

    /// <summary>Removes characters that are not allowed in an XML document (control characters, lone surrogates).</summary>
    public static string XmlSafe(string text)
    {
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                sb.Append(c).Append(text[i + 1]);
                i++;
            }
            else if (c is '\t' or '\n' or '\r' || (c >= ' ' && !char.IsSurrogate(c) && c != '￾' && c != '￿'))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
