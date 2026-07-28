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
        var full = PrepareFilePath(path);

        lock (_lock)
        {
            _filePath = full;
        }
    }

    /// <summary>
    /// Validates and prepares a journal path without creating or modifying the file.
    /// Storage is deliberately restricted to ordinary .txt paths: settings.json is
    /// user-editable, so this boundary prevents an invalid setting from appending to
    /// or clearing an unrelated file type.
    /// </summary>
    public static string PrepareFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        var full = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(full), ".txt", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The journal path must use the .txt extension.", nameof(path));
        }

        if (Directory.Exists(full))
        {
            throw new IOException("The journal path points to a directory.");
        }

        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return full;
    }

    /// <summary>
    /// Verifies that the current target can be opened for writing without changing
    /// its contents. A missing file is created, matching the next append's behavior.
    /// </summary>
    public void EnsureWritable()
    {
        lock (_lock)
        {
            using var stream = new FileStream(
                _filePath,
                FileMode.OpenOrCreate,
                FileAccess.Write,
                FileShare.Read);
        }
    }

    public void AppendLine(string singleLine)
    {
        ArgumentNullException.ThrowIfNull(singleLine);
        AppendCore(singleLine, trailingBlankLine: false);
    }

    /// <summary>
    /// Appends an empty line (group separator). Does not count as content for numbering.
    /// </summary>
    public void AppendBlankLine()
    {
        AppendCore(contentLine: null, trailingBlankLine: true);
    }

    /// <summary>
    /// Appends a content line and optionally one blank separator in a single open/flush.
    /// </summary>
    public void AppendLine(string singleLine, bool alsoBlankLine)
    {
        ArgumentNullException.ThrowIfNull(singleLine);
        AppendCore(singleLine, alsoBlankLine);
    }

    private void AppendCore(string? contentLine, bool trailingBlankLine)
    {
        lock (_lock)
        {
            using var stream = new FileStream(
                _filePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read);

            var needsLineBoundary = false;
            if (stream.Length > 0)
            {
                stream.Position = stream.Length - 1;
                var finalByte = stream.ReadByte();
                needsLineBoundary = finalByte is not ('\r' or '\n');
            }

            stream.Position = stream.Length;
            using var writer = new StreamWriter(stream, Utf8NoBom);
            if (needsLineBoundary)
            {
                // A user may select or externally edit a txt file whose final line has
                // no EOL. Add the missing boundary before appending so two clips cannot
                // be permanently merged into one record.
                writer.WriteLine();
            }

            if (contentLine is not null)
            {
                writer.WriteLine(contentLine);
            }

            if (trailingBlankLine)
            {
                writer.WriteLine();
            }

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

            // Stream the whole file while retaining only the requested tail. A fixed
            // byte window loses valid entries when a few legal 256K clips exceed it;
            // startup already scans the full file for CountNonEmptyLines, so this keeps
            // bounded memory without introducing a new asymptotic I/O cost.
            var lines = new Queue<string>(Math.Min(n, 1024));
            using (var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: true))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (lines.Count == n)
                    {
                        lines.Dequeue();
                    }

                    lines.Enqueue(line);
                }
            }

            return lines.ToList();
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

}
