using System.Text.Json;

namespace ClipJournal;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>Bytes written/read with the same encoder so hand-edited files
    /// (e.g. notepad saving with BOM) don't drift across saves.</summary>
    private static readonly System.Text.Encoding Utf8NoBom = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

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

    public static AppSettings Load() => Load(out _);

    /// <summary>
    /// Loads settings. When the file exists but cannot be parsed (corrupt JSON),
    /// <paramref name="warning"/> receives a localized message the caller can show
    /// once so the user knows their settings were reset to defaults instead of
    /// silently disappearing.
    /// </summary>
    public static AppSettings Load(out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath, Utf8NoBom);
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
        catch (Exception ex)
        {
            // Preserve the broken file so the user can recover it, then fall back
            // to defaults. Without a backup the original settings are lost forever.
            try
            {
                if (File.Exists(SettingsPath))
                {
                    File.Move(SettingsPath, SettingsPath + ".broken", overwrite: true);
                }
            }
            catch
            {
                // Best-effort backup; if it fails we still reset to defaults.
            }

            warning = Localization.SettingsCorruptWarning(ex.Message);
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
        try
        {
            File.WriteAllText(temp, json, Utf8NoBom);
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
