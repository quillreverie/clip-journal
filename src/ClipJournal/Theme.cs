using System.Drawing.Drawing2D;

namespace ClipJournal;

public static class Theme
{
    public const int CornerRadius = 10;
    public const int LargeCornerRadius = 16;

    // Quiet, warm-neutral foundations keep long clipboard entries comfortable to scan.
    public static readonly Color Background = Color.FromArgb(246, 247, 249);
    public static readonly Color SidebarBackground = Color.FromArgb(251, 251, 252);
    public static readonly Color Surface = Color.FromArgb(255, 255, 255);
    public static readonly Color SurfaceSoft = Color.FromArgb(242, 244, 247);
    public static readonly Color SurfaceHover = Color.FromArgb(247, 248, 251);
    public static readonly Color SurfaceSelected = Color.FromArgb(241, 242, 255);

    public static readonly Color Border = Color.FromArgb(226, 229, 234);
    public static readonly Color BorderStrong = Color.FromArgb(210, 214, 222);
    public static readonly Color Divider = Color.FromArgb(235, 237, 241);

    // A restrained periwinkle accent feels calm while remaining unmistakably interactive.
    public static readonly Color Primary = Color.FromArgb(96, 101, 210);
    public static readonly Color PrimaryHover = Color.FromArgb(78, 83, 190);
    public static readonly Color PrimaryPressed = Color.FromArgb(69, 73, 171);
    public static readonly Color PrimarySoft = Color.FromArgb(238, 239, 255);
    public static readonly Color PrimaryText = Color.FromArgb(65, 69, 151);

    public static readonly Color Success = Color.FromArgb(25, 116, 83);
    public static readonly Color SuccessSoft = Color.FromArgb(233, 248, 242);
    public static readonly Color Warning = Color.FromArgb(145, 85, 13);
    public static readonly Color WarningSoft = Color.FromArgb(255, 246, 230);
    public static readonly Color Danger = Color.FromArgb(179, 63, 78);
    public static readonly Color DangerHover = Color.FromArgb(156, 49, 64);
    public static readonly Color DangerSoft = Color.FromArgb(255, 240, 242);

    public static readonly Color TextPrimary = Color.FromArgb(31, 35, 43);
    public static readonly Color TextSecondary = Color.FromArgb(94, 102, 116);
    public static readonly Color TextMuted = Color.FromArgb(112, 120, 133);
    public static readonly Color TextOnAccent = Color.White;

    // Fonts are cached for the app lifetime so custom-painted controls do not leak GDI handles.
    public static readonly Font FontDisplay = new("Segoe UI", 18F, FontStyle.Bold);
    public static readonly Font FontBrand = new("Segoe UI", 12.5F, FontStyle.Bold);
    public static readonly Font FontTitle = new("Segoe UI", 12F, FontStyle.Bold);
    public static readonly Font FontMain = new("Segoe UI", 9.5F, FontStyle.Regular);
    public static readonly Font FontMainMedium = new("Segoe UI", 9.5F, FontStyle.Bold);
    public static readonly Font FontSmall = new("Segoe UI", 8.5F, FontStyle.Regular);
    public static readonly Font FontSmallMedium = new("Segoe UI", 8.5F, FontStyle.Bold);
    public static readonly Font FontCode = new("Consolas", 9.5F, FontStyle.Regular);
    public static readonly Font FontCount = new("Segoe UI", 8.5F, FontStyle.Bold);

    public static int Scale(Control control, int logicalPixels)
        => (int)Math.Round(logicalPixels * control.DeviceDpi / 96f);

    public static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int cornerRadius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        var radius = Math.Max(0, Math.Min(cornerRadius, Math.Min(bounds.Width, bounds.Height) / 2));
        if (radius == 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var diameter = radius * 2;
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectangle(bounds, radius);
        graphics.FillPath(brush, path);
    }

    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectangle(bounds, radius);
        graphics.DrawPath(pen, path);
    }

    public static Color Interpolate(Color from, Color to, float progress)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        return Color.FromArgb(
            (byte)(from.A + (to.A - from.A) * progress),
            (byte)(from.R + (to.R - from.R) * progress),
            (byte)(from.G + (to.G - from.G) * progress),
            (byte)(from.B + (to.B - from.B) * progress));
    }
}
