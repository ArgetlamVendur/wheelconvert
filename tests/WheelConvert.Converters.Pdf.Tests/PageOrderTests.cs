using Xunit;

namespace WheelConvert.Converters.Pdf.Tests;

/// <summary>CONTRACTS C6 parsing rules for <c>OptionKeys.PageOrder</c>.</summary>
public sealed class PageOrderTests
{
    [Theory]
    [InlineData("3,1,2", new[] { 2, 0, 1 })]
    [InlineData(" 2 , 3 ", new[] { 1, 2 })]
    [InlineData("1,2,3", new[] { 0, 1, 2 })]
    [InlineData("3", new[] { 2 })]
    public void Valid(string text, int[] expected)
    {
        Assert.True(PageOrder.TryParse(text, 3, out var indices));
        Assert.Equal(expected, indices);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("1,1")]
    [InlineData("0")]
    [InlineData("4")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1,,2")]
    [InlineData("1;2")]
    [InlineData("a")]
    [InlineData("1.0")]
    [InlineData("99999999999")]
    public void Invalid(string? text)
    {
        Assert.False(PageOrder.TryParse(text, 3, out var indices));
        Assert.Empty(indices);
    }
}
