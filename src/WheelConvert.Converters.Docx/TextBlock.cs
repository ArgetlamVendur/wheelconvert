namespace WheelConvert.Converters.Docx;

/// <summary>Heading level a block is written with. Maps to Word's built-in styles Heading1, Heading2 and Normal.</summary>
internal enum BlockStyle
{
    Normal,
    Heading1,
    Heading2,
}

/// <summary>One paragraph of extracted text and the style to write it in.</summary>
internal sealed record TextBlock(string Text, BlockStyle Style);

/// <summary>The paragraphs of one PDF page, in reading order, plus character counts for scan/junk detection.</summary>
internal sealed record PageText(IReadOnlyList<TextBlock> Blocks, int LetterCount, int CharCount, int JunkCount);
