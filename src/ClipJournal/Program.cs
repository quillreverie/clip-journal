namespace ClipJournal;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var mutex = SingleInstance.TryAcquire(@"Local\ClipJournal.SingleInstance");
        if (mutex is null)
        {
            // A second instance: nudge the running window. If the broadcast fails
            // (UAC/session boundary), tell the user instead of exiting silently,
            // otherwise double-clicking the exe looks like it does nothing.
            if (!SingleInstance.SignalShowWindow())
            {
                ModernDialog.ShowInfo(Localization.AppName, Localization.AlreadyRunning);
            }

            return;
        }

        AppSettings settings;
        string? settingsWarning;
        try
        {
            settings = AppSettings.Load(out settingsWarning);
        }
        catch (Exception)
        {
            // Do not continue with defaults when a valid settings file is merely
            // locked or inaccessible: doing so can split future clips into a
            // different journal and later overwrite the original configuration.
            ModernDialog.ShowError(owner: null, Localization.SettingsReadFailed);
            return;
        }

        if (!TryCreateStore(settings, out var store, out var pathChanged))
        {
            ModernDialog.ShowError(owner: null, Localization.StorageUnavailable);
            return;
        }

        if (pathChanged)
        {
            settingsWarning = CombineWarnings(
                settingsWarning,
                Localization.StoragePathAdjusted(settings.ClipsFilePath));
        }

        var privacyWasAccepted = settings.PrivacyAccepted;
        if (!EnsurePrivacyAccepted(settings))
        {
            return;
        }

        // EnsurePrivacyAccepted already saves a newly accepted configuration. When
        // privacy was accepted on an earlier launch, persist any path fallback now so
        // the next launch cannot silently jump back to the unusable configured path.
        if (privacyWasAccepted && pathChanged)
        {
            try
            {
                settings.Save();
            }
            catch
            {
                settingsWarning = CombineWarnings(settingsWarning, Localization.SaveSettingsFailedHint);
            }
        }

        using var mainForm = new MainForm(settings, store!, settingsWarning);
        Application.Run(mainForm);
    }

    private static bool TryCreateStore(
        AppSettings settings,
        out ClipStore? store,
        out bool pathChanged)
    {
        store = null;
        pathChanged = false;
        var configuredPath = settings.ClipsFilePath;
        var candidates = new[]
        {
            (Path: configuredPath, MustBeWritable: false),
            (Path: AppSettings.DefaultClipsPath, MustBeWritable: true),
            (Path: Path.Combine(Path.GetTempPath(), "ClipJournal", "clips.txt"), MustBeWritable: true),
        };
        var attempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (!attempted.Add(candidate.Path))
            {
                continue;
            }

            try
            {
                store = new ClipStore(candidate.Path);
                if (candidate.MustBeWritable)
                {
                    // Fallbacks must be proven usable. The configured path is kept
                    // when syntactically valid even if it is temporarily locked;
                    // MainForm then starts paused and lets Resume retry, avoiding a
                    // permanent and surprising migration after a momentary lock.
                    store.EnsureWritable();
                }

                settings.ClipsFilePath = store.FilePath;
                pathChanged = !string.Equals(
                    configuredPath,
                    settings.ClipsFilePath,
                    StringComparison.OrdinalIgnoreCase);
                return true;
            }
            catch
            {
                // Try the next local fallback. No clipboard content is written before
                // privacy confirmation, and the final resolved path is what the dialog
                // displays.
            }
        }

        return false;
    }

    private static string CombineWarnings(string? first, string second)
        => string.IsNullOrWhiteSpace(first) ? second : first + Environment.NewLine + second;

    private static bool EnsurePrivacyAccepted(AppSettings settings)
    {
        if (settings.PrivacyAccepted)
        {
            return true;
        }

        if (!ModernDialog.ConfirmPrivacy(settings.ClipsFilePath))
        {
            return false;
        }

        settings.PrivacyAccepted = true;
        try
        {
            settings.Save();
        }
        catch
        {
            // Still allow running if settings cannot be persisted.
        }

        return true;
    }
}
