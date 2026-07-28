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
        var (r, truncated) = TextNormalizer.ToSingleLine("a\r\n\nb\t c");
        Assert.False(truncated);
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
        var (r, _) = TextNormalizer.ToSingleLine("  hello  \n");
        Assert.Equal("hello", r);
    }

    [Fact]
    public void ToSingleLine_preserves_numbered_content()
    {
        // Content that looks like an old numbered line must stay intact.
        var (r, _) = TextNormalizer.ToSingleLine("12. hello");
        Assert.Equal("12. hello", r);
    }

    [Fact]
    public void ToSingleLine_respects_maxChars_without_full_scan_allocation()
    {
        var huge = new string('a', 1000) + "\n" + new string('b', 1000);
        var (r, truncated) = TextNormalizer.ToSingleLine(huge, maxChars: 10);
        Assert.True(truncated);
        Assert.Equal(10, r.Length);
        Assert.Equal(new string('a', 10), r);
    }

    [Fact]
    public void Truncate_does_not_split_surrogate_pair()
    {
        // 😀 is one rune, two UTF-16 chars.
        var s = "ab" + "\U0001F600" + "cd";
        var (t, truncated) = TextNormalizer.Truncate(s, 3);
        Assert.True(truncated);
        Assert.Equal("ab", t);
    }

    [Theory]
    [InlineData("alpha\u000Bbeta", "alpha beta")]
    [InlineData("alpha\u000Cbeta", "alpha beta")]
    [InlineData("alpha\u0085beta", "alpha beta")]
    [InlineData("alpha\u00A0beta", "alpha beta")]
    [InlineData("alpha\u2028beta", "alpha beta")]
    [InlineData("alpha\u2029beta", "alpha beta")]
    public void ToSingleLine_flattens_unicode_whitespace(string input, string expected)
    {
        var (result, truncated) = TextNormalizer.ToSingleLine(input);

        Assert.False(truncated);
        Assert.Equal(expected, result);
    }
}
