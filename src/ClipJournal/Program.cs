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

        var settings = AppSettings.Load(out var settingsWarning);
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

        Application.Run(new MainForm(settings, store!, settingsWarning));
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
            configuredPath,
            AppSettings.DefaultClipsPath,
            Path.Combine(Path.GetTempPath(), "ClipJournal", "clips.txt"),
        };
        var attempted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (!attempted.Add(candidate))
            {
                continue;
            }

            try
            {
                store = new ClipStore(candidate);
                // A valid path is not necessarily usable (readonly file, ACL, or a
                // long-lived exclusive lock). Probe inside the candidate loop so the
                // fallback actually selects a writable target before privacy consent
                // and before the resolved path is persisted.
                store.EnsureWritable();
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
