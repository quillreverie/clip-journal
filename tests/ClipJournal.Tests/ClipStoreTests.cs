using ClipJournal;
using System.Text;

namespace ClipJournal.Tests;

public class ClipStoreTests
{
    [Fact]
    public void Append_and_ReadTail_roundtrip()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-" + Guid.NewGuid() + ".txt");
        try
        {
            var store = new ClipStore(path);
            store.AppendLine("one");
            store.AppendLine("two");
            store.AppendLine("three");
            var tail = store.ReadTailLines(2);
            Assert.Equal(new[] { "two", "three" }, tail);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Clear_empties_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-" + Guid.NewGuid() + ".txt");
        try
        {
            var store = new ClipStore(path);
            store.AppendLine("x");
            store.Clear();
            Assert.Empty(store.ReadTailLines(10));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SetFilePath_creates_directory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cj-dir-" + Guid.NewGuid());
        var path = Path.Combine(dir, "nested", "clips.txt");
        try
        {
            var store = new ClipStore(path);
            store.AppendLine("hello");
            Assert.True(File.Exists(path));
            Assert.Equal("hello", File.ReadAllText(path).Trim());
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void CountNonEmptyLines_counts_appended_rows()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-" + Guid.NewGuid() + ".txt");
        try
        {
            var store = new ClipStore(path);
            Assert.Equal(0, store.CountNonEmptyLines());
            store.AppendLine("a");
            store.AppendLine("b");
            Assert.Equal(2, store.CountNonEmptyLines());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void AppendBlankLine_inserts_empty_separator_without_counting()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-" + Guid.NewGuid() + ".txt");
        try
        {
            var store = new ClipStore(path);
            store.AppendLine("a");
            store.AppendLine("b");
            store.AppendBlankLine();
            store.AppendLine("c");

            var normalized = File.ReadAllText(path).Replace("\r\n", "\n");
            Assert.Contains("b\n\nc", normalized);
            Assert.Equal(3, store.CountNonEmptyLines());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void AppendLine_with_blank_writes_content_and_separator_together()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-" + Guid.NewGuid() + ".txt");
        try
        {
            var store = new ClipStore(path);
            store.AppendLine("a", alsoBlankLine: false);
            store.AppendLine("b", alsoBlankLine: true);
            store.AppendLine("c", alsoBlankLine: false);

            var normalized = File.ReadAllText(path).Replace("\r\n", "\n");
            Assert.Equal("a\nb\n\nc\n", normalized);
            Assert.Equal(3, store.CountNonEmptyLines());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ReadTailLines_keeps_content_that_looks_numbered()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-" + Guid.NewGuid() + ".txt");
        try
        {
            var store = new ClipStore(path);
            store.AppendLine("12. hello");
            var tail = store.ReadTailLines(1);
            Assert.Equal(new[] { "12. hello" }, tail);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void AppendLine_adds_boundary_when_existing_file_has_no_final_newline()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-" + Guid.NewGuid() + ".txt");
        try
        {
            File.WriteAllText(path, "old");
            var store = new ClipStore(path);

            store.AppendLine("new");

            var normalized = File.ReadAllText(path).Replace("\r\n", "\n");
            Assert.Equal("old\nnew\n", normalized);
            Assert.Equal(2, store.CountNonEmptyLines());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ReadTailLines_returns_all_requested_large_lines()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-" + Guid.NewGuid() + ".txt");
        try
        {
            var store = new ClipStore(path);
            var lines = Enumerable.Range(1, 4)
                .Select(index =>
                    index + ":" +
                    new string(
                        (char)('a' + index),
                        ClipStore.MaxStoredLineChars - 2))
                .ToArray();
            foreach (var line in lines)
            {
                store.AppendLine(line);
            }

            var tail = store.ReadTailLines(4);

            Assert.Equal(lines, tail);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Constructor_rejects_non_txt_path()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-" + Guid.NewGuid() + ".json");
        Assert.Throws<ArgumentException>(() => new ClipStore(path));
    }

    [Fact]
    public void EnsureWritable_rejects_readonly_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-" + Guid.NewGuid() + ".txt");
        try
        {
            File.WriteAllText(path, "existing");
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            var store = new ClipStore(path);

            Assert.Throws<UnauthorizedAccessException>(() => store.EnsureWritable());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
    }

    public static IEnumerable<object[]> SupportedEncodings()
    {
        yield return new object[] { new UTF8Encoding(encoderShouldEmitUTF8Identifier: false) };
        yield return new object[] { new UTF8Encoding(encoderShouldEmitUTF8Identifier: true) };
        yield return new object[] { Encoding.Unicode };
        yield return new object[] { Encoding.BigEndianUnicode };
        yield return new object[] { Encoding.UTF32 };
        yield return new object[] { new UTF32Encoding(bigEndian: true, byteOrderMark: true) };
    }

    [Theory]
    [MemberData(nameof(SupportedEncodings))]
    public void AppendLine_preserves_existing_bom_encoding_and_line_boundary(Encoding encoding)
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-" + Guid.NewGuid() + ".txt");
        try
        {
            File.WriteAllText(path, "旧", encoding);
            var originalPreamble = encoding.GetPreamble();
            var store = new ClipStore(path);

            store.AppendLine("新");

            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.AsSpan().StartsWith(originalPreamble));
            Assert.Equal(
                "旧" + Environment.NewLine + "新" + Environment.NewLine,
                File.ReadAllText(path, encoding));
            var snapshot = store.ReadSnapshot(10);
            Assert.Equal(2, snapshot.TotalCount);
            Assert.Equal(new[] { "旧", "新" }, snapshot.TailLines);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ReadSnapshot_rejects_external_line_over_capture_limit()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-" + Guid.NewGuid() + ".txt");
        try
        {
            File.WriteAllText(path, new string('x', ClipStore.MaxStoredLineChars + 1));
            var store = new ClipStore(path);

            Assert.Throws<InvalidDataException>(() => store.ReadSnapshot(500));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ReadSnapshot_rejects_invalid_bomless_utf8_before_append()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-" + Guid.NewGuid() + ".txt");
        try
        {
            File.WriteAllBytes(path, new byte[] { 0x63, 0x61, 0x66, 0xE9 });
            var store = new ClipStore(path);

            Assert.Throws<DecoderFallbackException>(() => store.ReadSnapshot(500));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
