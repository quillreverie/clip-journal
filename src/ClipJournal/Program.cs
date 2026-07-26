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
                "ClipJournal 已在运行。",
                "ClipJournal",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.Run(new MainForm());
    }
}
