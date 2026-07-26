using ClipJournal;

namespace ClipJournal.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void IsIgnorable_whitespace_true(string? s)
        => Assert.True(TextNormalizer.IsIgnorable(s));

    [Fact]
    public void ToSingleLine_replaces_newlines_and_compresses_spaces()
    {
        var r = TextNormalizer.ToSingleLine("a\r\n\nb\t c");
        Assert.Equal("a b c", r);
    }

    [Fact]
    public void Truncate_over_limit()
    {
        var (t, truncated) = TextNormalizer.Truncate(new string('x', 10), 5);
        Assert.True(truncated);
        Assert.Equal(5, t.Length);
    }

    [Fact]
    public void ToSingleLine_trims_edges()
    {
        var r = TextNormalizer.ToSingleLine("  hello  \n");
        Assert.Equal("hello", r);
    }
}
