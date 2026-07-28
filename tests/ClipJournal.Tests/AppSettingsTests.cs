using ClipJournal;

namespace ClipJournal.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void LoadFromPath_quarantines_malformed_json()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-settings-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "{ invalid");

            var settings = AppSettings.LoadFromPath(path, out var warning);

            Assert.NotNull(settings);
            Assert.NotNull(warning);
            Assert.False(File.Exists(path));
            Assert.True(File.Exists(path + ".broken"));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".broken");
        }
    }

    [Fact]
    public void LoadFromPath_does_not_quarantine_transient_io_failure()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-settings-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, """{"PrivacyAccepted":true}""");
            using var heldOpen = new FileStream(
                path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);

            Assert.Throws<IOException>(() => AppSettings.LoadFromPath(path, out _));
            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".broken"));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".broken");
        }
    }

    [Fact]
    public void LoadFromPath_quarantines_oversized_settings_before_full_read()
    {
        var path = Path.Combine(Path.GetTempPath(), "cj-settings-" + Guid.NewGuid() + ".json");
        try
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                stream.SetLength(AppSettings.MaxSettingsBytes + 1);
            }

            _ = AppSettings.LoadFromPath(path, out var warning);

            Assert.NotNull(warning);
            Assert.False(File.Exists(path));
            Assert.True(File.Exists(path + ".broken"));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".broken");
        }
    }
}
