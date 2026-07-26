namespace ClipJournal;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var mutex = SingleInstance.TryAcquire(@"Local\ClipJournal.SingleInstance");
        if (mutex is null)
        {
            MessageBox.Show(
                Localization.AlreadyRunning,
                Localization.AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var settings = AppSettings.Load();
        if (!EnsurePrivacyAccepted(settings))
        {
            return;
        }

        Application.Run(new MainForm(settings));
    }

    private static bool EnsurePrivacyAccepted(AppSettings settings)
    {
        if (settings.PrivacyAccepted)
        {
            return true;
        }

        var result = MessageBox.Show(
            Localization.PrivacyMessage(settings.ClipsFilePath),
            Localization.PrivacyTitle,
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);

        if (result != DialogResult.OK)
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
