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
        if (!EnsurePrivacyAccepted(settings))
        {
            return;
        }

        Application.Run(new MainForm(settings, settingsWarning));
    }

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
