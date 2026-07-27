using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ClipJournal;

public sealed class BrandLogo : Control
{
    public BrandLogo()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        DoubleBuffered = true;
        Size = new Size(224, 52);
        BackColor = Color.Transparent;
        AccessibleName = Localization.AppName;
        AccessibleDescription = Localization.BrandSubtitle;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using (var background = new SolidBrush(Parent?.BackColor ?? Theme.SidebarBackground))
        {
            graphics.FillRectangle(background, ClientRectangle);
        }

        var iconSize = Theme.Scale(this, 42);
        var iconRect = new Rectangle(0, (Height - iconSize) / 2, iconSize, iconSize);
        using (var gradient = new LinearGradientBrush(
                   iconRect,
                   Color.FromArgb(116, 121, 224),
                   Theme.Primary,
                   LinearGradientMode.ForwardDiagonal))
        {
            graphics.FillRoundedRectangle(gradient, iconRect, Theme.Scale(this, 13));
        }

        var glyphRect = Rectangle.Inflate(iconRect, -Theme.Scale(this, 10), -Theme.Scale(this, 10));
        UiIconPainter.Draw(
            graphics,
            UiIcon.Clipboard,
            glyphRect,
            Color.White,
            Math.Max(1.5f, 1.7f * DeviceDpi / 96f));

        var textLeft = iconRect.Right + Theme.Scale(this, 12);
        var titleRect = new Rectangle(textLeft, Theme.Scale(this, 2), Math.Max(1, Width - textLeft), Theme.Scale(this, 25));
        TextRenderer.DrawText(
            graphics,
            Localization.AppName,
            Theme.FontBrand,
            titleRect,
            Theme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

        var subtitleRect = new Rectangle(textLeft, Theme.Scale(this, 28), Math.Max(1, Width - textLeft), Theme.Scale(this, 19));
        TextRenderer.DrawText(
            graphics,
            Localization.BrandSubtitle,
            Theme.FontSmall,
            subtitleRect,
            Theme.TextSecondary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}
