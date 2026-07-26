namespace ClipJournal;

public sealed class ClipStore
{
    private static readonly System.Text.Encoding Utf8NoBom = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly object _lock = new();
    private string _filePath;

    public ClipStore(string filePath)
    {
        _filePath = string.Empty;
        SetFilePath(filePath);
    }

    public string FilePath
    {
        get
        {
            lock (_lock)
            {
                return _filePath;
            }
        }
    }

    public void SetFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        lock (_lock)
        {
            _filePath = full;
        }
    }

    public void AppendLine(string singleLine)
    {
        ArgumentNullException.ThrowIfNull(singleLine);

        lock (_lock)
        {
            using var stream = new FileStream(
                _filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read);
            using var writer = new StreamWriter(stream, Utf8NoBom);
            writer.WriteLine(singleLine);
            writer.Flush();
            stream.Flush(true);
        }
    }

    public IReadOnlyList<string> ReadTailLines(int n)
    {
        if (n <= 0)
        {
            return Array.Empty<string>();
        }

        lock (_lock)
        {
            if (!File.Exists(_filePath))
            {
                return Array.Empty<string>();
            }

            var info = new FileInfo(_filePath);
            if (info.Length == 0)
            {
                return Array.Empty<string>();
            }

            // MVP: small files expected. For large files, only read the last 1MB.
            const long maxRead = 1 * 1024 * 1024;
            List<string> lines;
            if (info.Length <= maxRead)
            {
                lines = File.ReadAllLines(_filePath, Utf8NoBom)
                    .Where(static l => !string.IsNullOrWhiteSpace(l))
                    .ToList();
            }
            else
            {
                lines = ReadLastChunkLines(_filePath, maxRead);
            }

            if (lines.Count <= n)
            {
                return lines;
            }

            return lines.Skip(lines.Count - n).ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_filePath, string.Empty, Utf8NoBom);
        }
    }

    /// <summary>
    /// Counts non-empty lines in the current file (for continuing sequence numbers).
    /// </summary>
    public int CountNonEmptyLines()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
            {
                return 0;
            }

            var count = 0;
            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: true);
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    count++;
                }
            }

            return count;
        }
    }

    private static List<string> ReadLastChunkLines(string path, long maxRead)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var length = stream.Length;
        var start = Math.Max(0, length - maxRead);
        stream.Position = start;

        using var reader = new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: true);
        // If we started mid-file, drop the partial first line.
        if (start > 0)
        {
            _ = reader.ReadLine();
        }

        var lines = new List<string>();
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        return lines;
    }
}
