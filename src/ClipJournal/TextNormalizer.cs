namespace ClipJournal;

public static class TextNormalizer
{
    public static bool IsIgnorable(string? text)
        => string.IsNullOrWhiteSpace(text);

    /// <summary>
    /// Flattens whitespace/newlines to single spaces. If <paramref name="maxChars"/> &gt; 0,
    /// stops once the result reaches that length (avoids allocating for huge clipboard dumps).
    /// </summary>
    public static (string text, bool truncated) ToSingleLine(string text, int maxChars = 0)
    {
        var capacity = maxChars > 0 ? Math.Min(text.Length, maxChars) : text.Length;
        var sb = new System.Text.StringBuilder(capacity);
        var prevSpace = false;
        var truncated = false;

        foreach (var ch in text)
        {
            if (ch is '\r' or '\n' or '\t' or ' ')
            {
                if (!prevSpace && sb.Length > 0)
                {
                    if (maxChars > 0 && sb.Length >= maxChars)
                    {
                        truncated = true;
                        break;
                    }

                    sb.Append(' ');
                    prevSpace = true;
                }

                continue;
            }

            if (maxChars > 0 && sb.Length >= maxChars)
            {
                truncated = true;
                break;
            }

            sb.Append(ch);
            prevSpace = false;
        }

        var result = sb.ToString().Trim();
        if (maxChars > 0 && result.Length > maxChars)
        {
            result = SafeTruncate(result, maxChars);
            truncated = true;
        }

        return (result, truncated);
    }

    public static (string text, bool truncated) Truncate(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return (text, false);
        }

        return (SafeTruncate(text, maxChars), true);
    }

    private static string SafeTruncate(string text, int maxChars)
    {
        if (maxChars <= 0)
        {
            return string.Empty;
        }

        if (text.Length <= maxChars)
        {
            return text;
        }

        // Avoid splitting a UTF-16 surrogate pair.
        var len = maxChars;
        if (char.IsHighSurrogate(text[len - 1]) && len < text.Length && char.IsLowSurrogate(text[len]))
        {
            len--;
        }

        return text[..len];
    }
}
