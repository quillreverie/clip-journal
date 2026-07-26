namespace ClipJournal;

public static class TextNormalizer
{
    public static bool IsIgnorable(string? text)
        => string.IsNullOrWhiteSpace(text);

    public static string ToSingleLine(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        var prevSpace = false;
        foreach (var ch in text)
        {
            if (ch is '\r' or '\n' or '\t' or ' ')
            {
                if (!prevSpace && sb.Length > 0)
                {
                    sb.Append(' ');
                    prevSpace = true;
                }

                continue;
            }

            sb.Append(ch);
            prevSpace = false;
        }

        return sb.ToString().Trim();
    }

    public static (string text, bool truncated) Truncate(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return (text, false);
        }

        return (text[..maxChars], true);
    }
}
