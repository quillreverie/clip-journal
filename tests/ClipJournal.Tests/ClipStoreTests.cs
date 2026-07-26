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
}
