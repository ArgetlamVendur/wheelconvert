using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace WheelConvert.Converters.Docx;

/// <summary>Turns one PDF page into paragraphs: words, then text blocks, then reading order (req. 4), then heading detection (req. 5).</summary>
internal static class PdfTextExtractor
{
    public static PageText ExtractPage(Page page)
    {
        var letters = page.Letters;
        var letterCount = 0;
        var charCount = 0;
        var junk = 0;
        var sizes = new List<double>(letters.Count);
        foreach (var letter in letters)
        {
            var value = letter.Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            sizes.Add(letter.PointSize);
            for (var i = 0; i < value.Length; i++)
            {
                charCount++;
                if (char.IsLetter(value[i]))
                {
                    letterCount++;
                }
                else if (TextRules.IsJunk(value, i))
                {
                    junk++;
                }
            }
        }

        if (letterCount == 0)
        {
            return new PageText([], letterCount, charCount, junk);
        }

        var bodySize = TextRules.Median(sizes);
        var words = NearestNeighbourWordExtractor.Instance.GetWords(letters);
        var blocks = DocstrumBoundingBoxes.Instance.GetBlocks(words);
        var ordered = UnsupervisedReadingOrderDetector.Instance.Get(blocks);

        var result = new List<TextBlock>();
        foreach (var block in ordered)
        {
            // A text block can hold a heading and the paragraphs under it (the layout analysis groups by distance only),
            // so lines are regrouped: a new paragraph starts when the heading class changes or there is a paragraph-sized gap.
            var lines = new List<string>();
            var style = BlockStyle.Normal;
            TextLine? previous = null;
            foreach (var line in block.TextLines)
            {
                var lineSizes = line.Words.SelectMany(w => w.Letters).Select(l => l.PointSize).ToList();
                var lineStyle = TextRules.StyleFor(TextRules.Median(lineSizes), bodySize);
                var gap = previous is null ? 0 : previous.BoundingBox.Bottom - line.BoundingBox.Top;
                if (previous is not null && (lineStyle != style || gap > TextRules.ParagraphGapRatio * bodySize))
                {
                    Flush(result, lines, style);
                }

                style = lineStyle;
                lines.Add(string.Join(' ', line.Words.Select(w => w.Text)));
                previous = line;
            }

            Flush(result, lines, style);
        }

        return new PageText(result, letterCount, charCount, junk);
    }

    private static void Flush(List<TextBlock> result, List<string> lines, BlockStyle style)
    {
        var text = TextRules.XmlSafe(TextRules.JoinLines(lines));
        lines.Clear();
        if (text.Length > 0)
        {
            result.Add(new TextBlock(text, style));
        }
    }
}
