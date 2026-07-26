using ClipJournal;

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
}
