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

    /// <summary>
    /// Builds a numbered line for the txt file, e.g. "3. hello".
    /// </summary>
    public static string FormatNumberedLine(int index, string content)
        => $"{index}. {content}";

    /// <summary>
    /// Strips a leading "N. " prefix if present so dedupe compares raw content.
    /// Requires a space after the dot (our format is "3. hello").
    /// </summary>
    public static string StripNumberPrefix(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return line;
        }

        var i = 0;
        while (i < line.Length && char.IsDigit(line[i]))
        {
            i++;
        }

        // Must be digits + ". "
        if (i == 0 || i + 1 >= line.Length || line[i] != '.' || line[i + 1] != ' ')
        {
            return line;
        }

        return line[(i + 2)..];
    }
}
