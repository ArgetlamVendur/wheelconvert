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

        var paragraphs = new List<Para>();
        foreach (var block in ordered)
        {
            AddParagraphs(paragraphs, block, bodySize);
        }

        // The block segmenter can put the lines of a wrapped heading into separate blocks: join neighbouring paragraphs of
        // the same heading class that sit one line apart (within 1.5 font sizes), and nothing else.
        var merged = new List<Para>();
        foreach (var paragraph in paragraphs)
        {
            if (merged.Count > 0 && merged[^1] is var previous && paragraph.Style != BlockStyle.Normal && previous.Style == paragraph.Style
                && previous.LastBaseline - paragraph.FirstBaseline is > 0 and var distance && distance <= TextRules.HeadingJoinRatio * paragraph.Size)
            {
                merged[^1] = previous with { Text = previous.Text + " " + paragraph.Text, LastBaseline = paragraph.LastBaseline };
            }
            else
            {
                merged.Add(paragraph);
            }
        }

        var result = merged.Select(p => new TextBlock(p.Text, p.Style)).ToList();
        return new PageText(result, letterCount, charCount, junk);
    }

    private sealed record Para(string Text, BlockStyle Style, double FirstBaseline, double LastBaseline, double Size);

    private sealed record Line(string Text, double Baseline, List<double> Sizes, BlockStyle Style);

    /// <summary>
    /// A text block can hold a heading and the paragraphs under it (the layout analysis groups by distance only), so its lines
    /// are regrouped. A new paragraph starts when the heading class changes, or when the baseline distance is much larger than
    /// the block's own line pitch (see <see cref="TextRules.StartsNewParagraph"/>), so 1.5 and double line spacing stay together.
    /// Each paragraph is classified by the median size of all its letters (req. 5).
    /// </summary>
    private static void AddParagraphs(List<Para> result, UglyToad.PdfPig.DocumentLayoutAnalysis.TextBlock block, double bodySize)
    {
        var lines = new List<Line>();
        foreach (var line in block.TextLines)
        {
            var letters = line.Words.SelectMany(w => w.Letters).ToList();
            if (letters.Count == 0)
            {
                continue;
            }

            var sizes = letters.Select(l => l.PointSize).ToList();
            lines.Add(new Line(
                string.Join(' ', line.Words.Select(w => w.Text)),
                letters[0].StartBaseLine.Y,
                sizes,
                TextRules.StyleFor(TextRules.Median([.. sizes]), bodySize)));
        }

        var start = 0;
        while (start < lines.Count)
        {
            var end = start + 1;
            while (end < lines.Count && lines[end].Style == lines[start].Style)
            {
                end++;
            }

            var run = lines.GetRange(start, end - start);
            var pitches = new List<double>();
            for (var i = 1; i < run.Count; i++)
            {
                pitches.Add(Math.Abs(run[i - 1].Baseline - run[i].Baseline));
            }

            var runMedian = TextRules.Median([.. pitches]);
            var runSize = TextRules.Median(run.SelectMany(l => l.Sizes).ToList());
            var from = 0;
            for (var i = 1; i <= run.Count; i++)
            {
                if (i == run.Count || TextRules.StartsNewParagraph(pitches[i - 1], runMedian, pitches.Count, runSize))
                {
                    Flush(result, run.GetRange(from, i - from), bodySize);
                    from = i;
                }
            }

            start = end;
        }
    }

    private static void Flush(List<Para> result, List<Line> paragraph, double bodySize)
    {
        var text = TextRules.XmlSafe(TextRules.JoinLines(paragraph.Select(l => l.Text)));
        if (text.Length > 0)
        {
            var size = TextRules.Median(paragraph.SelectMany(l => l.Sizes).ToList());
            result.Add(new Para(text, TextRules.StyleFor(size, bodySize), paragraph[0].Baseline, paragraph[^1].Baseline, size));
        }
    }
}
