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

    /// <summary>A baseline distance of more than this many times the block's median line pitch starts a new paragraph (needs 3+ lines).</summary>
    public const double PitchJumpRatio = 1.5;

    /// <summary>Whatever the line pitch, a baseline distance of more than this many font sizes is a paragraph break (double spacing is about 2.2).</summary>
    public const double AbsolutePitchRatio = 2.5;

    /// <summary>Two heading paragraphs from neighbouring blocks are one wrapped heading when their baselines are at most this many font sizes apart.</summary>
    public const double HeadingJoinRatio = 1.5;

    /// <summary>True when the baseline distance <paramref name="pitch"/> to the previous line starts a new paragraph.</summary>
    public static bool StartsNewParagraph(double pitch, double medianPitch, int pitchCount, double fontSize) =>
        pitch > AbsolutePitchRatio * fontSize || (pitchCount >= 2 && pitch > PitchJumpRatio * medianPitch);

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
        if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            // Supplementary private-use planes (U+F0000 and up) are surrogate pairs in UTF-16.
            return CharUnicodeInfo.GetUnicodeCategory(char.ConvertToUtf32(c, text[index + 1])) == UnicodeCategory.PrivateUse;
        }

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
