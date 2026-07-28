using System.Text;

namespace ClipJournal;

public sealed record ClipStoreSnapshot(int TotalCount, IReadOnlyList<string> TailLines);

public sealed class ClipStore
{
    public const int MaxStoredLineChars = 256 * 1024;

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private readonly object _lock = new();
    private string _filePath;

    private readonly record struct FileEncoding(
        Encoding WriteEncoding,
        Encoding ReadEncoding,
        int PreambleLength,
        int CodeUnitWidth);

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

            var fileEncoding = DetectEncoding(stream);
            var needsLineBoundary = HasContentWithoutFinalLineBreak(stream, fileEncoding);

            stream.Position = stream.Length;
            using (var writer = new StreamWriter(
                       stream,
                       fileEncoding.WriteEncoding,
                       bufferSize: 1024,
                       leaveOpen: true))
            {
                if (needsLineBoundary)
                {
                    // A user may select or externally edit a txt file whose final line
                    // has no EOL. Add the missing boundary in the file's existing
                    // BOM-selected encoding so two clips cannot be merged or corrupted.
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
            }

            stream.Flush(true);
        }
    }

    public IReadOnlyList<string> ReadTailLines(int n)
    {
        if (n <= 0)
        {
            return Array.Empty<string>();
        }

        return ReadSnapshot(n).TailLines;
    }

    /// <summary>
    /// Reads the total count and bounded preview tail in one pass. Lines larger than
    /// the application's capture limit are rejected before they can cause an
    /// unbounded ReadLine allocation.
    /// </summary>
    public ClipStoreSnapshot ReadSnapshot(int tailCount)
    {
        if (tailCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tailCount));
        }

        lock (_lock)
        {
            if (!File.Exists(_filePath))
            {
                return new ClipStoreSnapshot(0, Array.Empty<string>());
            }

            var info = new FileInfo(_filePath);
            if (info.Length == 0)
            {
                return new ClipStoreSnapshot(0, Array.Empty<string>());
            }

            var tail = new Queue<string>(Math.Min(tailCount, 1024));
            var line = new StringBuilder(Math.Min(MaxStoredLineChars, 4096));
            var buffer = new char[8192];
            var totalCount = 0;
            var lineLength = 0;
            var lineHasContent = false;
            var previousWasCarriageReturn = false;

            void FinishLine()
            {
                if (lineHasContent)
                {
                    totalCount = checked(totalCount + 1);
                    if (tailCount > 0)
                    {
                        if (tail.Count == tailCount)
                        {
                            tail.Dequeue();
                        }

                        tail.Enqueue(line.ToString());
                    }
                }

                line.Clear();
                lineLength = 0;
                lineHasContent = false;
            }

            using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var fileEncoding = DetectEncoding(stream);
            stream.Position = fileEncoding.PreambleLength;
            using var reader = new StreamReader(
                stream,
                fileEncoding.ReadEncoding,
                detectEncodingFromByteOrderMarks: false);
            int charsRead;
            while ((charsRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (var index = 0; index < charsRead; index++)
                {
                    var ch = buffer[index];
                    if (ch == '\n')
                    {
                        if (!previousWasCarriageReturn)
                        {
                            FinishLine();
                        }

                        previousWasCarriageReturn = false;
                        continue;
                    }

                    if (ch == '\r')
                    {
                        FinishLine();
                        previousWasCarriageReturn = true;
                        continue;
                    }

                    previousWasCarriageReturn = false;
                    lineLength++;
                    if (lineLength > MaxStoredLineChars)
                    {
                        throw new InvalidDataException(
                            $"The journal contains a line longer than {MaxStoredLineChars} characters.");
                    }

                    line.Append(ch);
                    lineHasContent |= !char.IsWhiteSpace(ch);
                }
            }

            if (lineLength > 0)
            {
                FinishLine();
            }

            return new ClipStoreSnapshot(totalCount, tail.ToList());
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
        => ReadSnapshot(tailCount: 0).TotalCount;

    private static FileEncoding DetectEncoding(FileStream stream)
    {
        if (stream.Length == 0)
        {
            return new FileEncoding(
                Utf8NoBom,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                PreambleLength: 0,
                CodeUnitWidth: 1);
        }

        var originalPosition = stream.Position;
        stream.Position = 0;
        Span<byte> header = stackalloc byte[4];
        var read = stream.Read(header);
        stream.Position = originalPosition;

        if (read >= 4 &&
            header[0] == 0x00 && header[1] == 0x00 &&
            header[2] == 0xFE && header[3] == 0xFF)
        {
            return new FileEncoding(
                new UTF32Encoding(bigEndian: true, byteOrderMark: true),
                new UTF32Encoding(bigEndian: true, byteOrderMark: false, throwOnInvalidCharacters: true),
                PreambleLength: 4,
                CodeUnitWidth: 4);
        }

        if (read >= 4 &&
            header[0] == 0xFF && header[1] == 0xFE &&
            header[2] == 0x00 && header[3] == 0x00)
        {
            return new FileEncoding(
                Encoding.UTF32,
                new UTF32Encoding(bigEndian: false, byteOrderMark: false, throwOnInvalidCharacters: true),
                PreambleLength: 4,
                CodeUnitWidth: 4);
        }

        if (read >= 3 &&
            header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF)
        {
            return new FileEncoding(
                Encoding.UTF8,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                PreambleLength: 3,
                CodeUnitWidth: 1);
        }

        if (read >= 2 && header[0] == 0xFF && header[1] == 0xFE)
        {
            return new FileEncoding(
                Encoding.Unicode,
                new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true),
                PreambleLength: 2,
                CodeUnitWidth: 2);
        }

        if (read >= 2 && header[0] == 0xFE && header[1] == 0xFF)
        {
            return new FileEncoding(
                Encoding.BigEndianUnicode,
                new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true),
                PreambleLength: 2,
                CodeUnitWidth: 2);
        }

        return new FileEncoding(
            Utf8NoBom,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            PreambleLength: 0,
            CodeUnitWidth: 1);
    }

    private static bool HasContentWithoutFinalLineBreak(
        FileStream stream,
        FileEncoding fileEncoding)
    {
        if (stream.Length <= fileEncoding.PreambleLength)
        {
            return false;
        }

        if (stream.Length < fileEncoding.CodeUnitWidth)
        {
            return true;
        }

        var tail = new byte[fileEncoding.CodeUnitWidth];
        stream.Position = stream.Length - tail.Length;
        stream.ReadExactly(tail);
        if (fileEncoding.CodeUnitWidth == 1)
        {
            return tail[0] is not (0x0D or 0x0A);
        }

        var finalText = fileEncoding.ReadEncoding.GetString(tail);
        return finalText.Length == 0 || finalText[^1] is not ('\r' or '\n');
    }

}
