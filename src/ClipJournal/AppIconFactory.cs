using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ClipJournal;

internal static class AppIconFactory
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            var backgroundRect = new Rectangle(1, 1, 30, 30);
            using (var gradient = new LinearGradientBrush(
                       backgroundRect,
                       Color.FromArgb(119, 124, 226),
                       Theme.Primary,
                       LinearGradientMode.ForwardDiagonal))
            {
                graphics.FillRoundedRectangle(gradient, backgroundRect, 9);
            }

            UiIconPainter.Draw(graphics, UiIcon.Clipboard, new Rectangle(8, 7, 16, 18), Color.White, 1.7f);
        }

        var handle = bitmap.GetHicon();
        try
        {
            // Icon.FromHandle wraps the HICON in a managed Icon that owns a separate
            // GDI+ resource; dispose it after cloning so it is not left for the GC
            // finalizer. DestroyIcon frees the underlying HICON regardless.
            using var source = Icon.FromHandle(handle);
            return (Icon)source.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
