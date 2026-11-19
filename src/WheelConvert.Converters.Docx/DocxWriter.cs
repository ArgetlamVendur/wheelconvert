using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace WheelConvert.Converters.Docx;

/// <summary>Writes the extracted pages as a minimal .docx with Word's built-in style IDs, so headings show in the navigation pane.</summary>
internal static class DocxWriter
{
    public static void Write(string path, IReadOnlyList<PageText> pages)
    {
        using var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        var stylesPart = main.AddNewPart<StyleDefinitionsPart>();
        stylesPart.Styles = BuildStyles();

        var body = new Body();
        for (var i = 0; i < pages.Count; i++)
        {
            if (i > 0)
            {
                body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
            }

            foreach (var block in pages[i].Blocks)
            {
                var paragraph = new Paragraph();
                if (block.Style != BlockStyle.Normal)
                {
                    paragraph.Append(new ParagraphProperties(new ParagraphStyleId
                    {
                        Val = block.Style == BlockStyle.Heading1 ? "Heading1" : "Heading2",
                    }));
                }

                paragraph.Append(new Run(new Text(block.Text) { Space = SpaceProcessingModeValues.Preserve }));
                body.Append(paragraph);
            }
        }

        // Some readers want at least one paragraph in the body.
        if (!body.Elements<Paragraph>().Any())
        {
            body.Append(new Paragraph());
        }

        main.Document = new Document(body);
        main.Document.Save();
    }

    private static Styles BuildStyles() => new(
        new Style(new StyleName { Val = "Normal" }, new PrimaryStyle())
        {
            Type = StyleValues.Paragraph,
            StyleId = "Normal",
            Default = true,
        },
        Heading("Heading1", "heading 1", "32", 0),
        Heading("Heading2", "heading 2", "26", 1));

    private static Style Heading(string id, string name, string halfPoints, int outline) => new(
        new StyleName { Val = name },
        new BasedOn { Val = "Normal" },
        new NextParagraphStyle { Val = "Normal" },
        new UIPriority { Val = 9 },
        new PrimaryStyle(),
        new StyleParagraphProperties(
            new KeepNext(),
            new KeepLines(),
            new SpacingBetweenLines { Before = "240", After = "80" },
            new OutlineLevel { Val = outline }),
        new StyleRunProperties(new Bold(), new FontSize { Val = halfPoints }))
    {
        Type = StyleValues.Paragraph,
        StyleId = id,
    };
}
