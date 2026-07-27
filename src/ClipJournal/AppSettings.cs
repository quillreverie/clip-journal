using System.Text.Json;

namespace ClipJournal;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public string ClipsFilePath { get; set; } = DefaultClipsPath;

    public bool PrivacyAccepted { get; set; }

    /// <summary>
    /// After every N content lines, append one blank line to the txt. 0 = disabled.
    /// </summary>
    public int BlankLineEvery { get; set; }

    /// <summary>Upper bound enforced on load to mirror the UI stepper, so a
    /// hand-edited settings.json cannot inject a pathologically large value.</summary>
    private const int BlankLineMax = 999;

    public static string DefaultClipsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ClipJournal",
            "clips.txt");

    public static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipJournal",
            "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded is null)
            {
                return new AppSettings();
            }

            if (string.IsNullOrWhiteSpace(loaded.ClipsFilePath))
            {
                loaded.ClipsFilePath = DefaultClipsPath;
            }
            else
            {
                // Normalize and reject empty/invalid paths defensively.
                try
                {
                    loaded.ClipsFilePath = Path.GetFullPath(loaded.ClipsFilePath);
                }
                catch
                {
                    loaded.ClipsFilePath = DefaultClipsPath;
                }
            }

            if (loaded.BlankLineEvery < 0)
            {
                loaded.BlankLineEvery = 0;
            }
            else if (loaded.BlankLineEvery > BlankLineMax)
            {
                loaded.BlankLineEvery = BlankLineMax;
            }

            return loaded;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(this, JsonOptions);
        // Atomic-ish write to reduce risk of truncated settings on crash.
        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, json);
        try
        {
            File.Move(temp, SettingsPath, overwrite: true);
        }
        catch
        {
            // If the final move failed (e.g. settings.json held open by another
            // process), remove the orphaned temp so it cannot accumulate.
            try
            {
                File.Delete(temp);
            }
            catch
            {
                // Best-effort cleanup; nothing actionable if the temp is also locked.
            }

            throw;
        }
    }
}
