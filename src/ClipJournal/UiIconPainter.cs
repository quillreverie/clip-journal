using System.Drawing.Drawing2D;

namespace ClipJournal;

public enum UiIcon
{
    None,
    Pause,
    Play,
    Search,
    Close,
    Copy,
    Trash,
    File,
    Folder,
    Swap,
    ChevronRight,
    Clipboard,
    Minus,
    Plus,
}

public static class UiIconPainter
{
    public static void Draw(Graphics graphics, UiIcon icon, Rectangle bounds, Color color, float stroke = 1.7f)
    {
        if (icon == UiIcon.None || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, stroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        using var brush = new SolidBrush(color);

        var cx = bounds.Left + bounds.Width / 2f;
        var cy = bounds.Top + bounds.Height / 2f;
        var left = bounds.Left + bounds.Width * 0.18f;
        var right = bounds.Right - bounds.Width * 0.18f;
        var top = bounds.Top + bounds.Height * 0.18f;
        var bottom = bounds.Bottom - bounds.Height * 0.18f;

        switch (icon)
        {
            case UiIcon.Pause:
                graphics.FillRoundedRectangle(
                    brush,
                    Rectangle.Round(new RectangleF(bounds.Left + bounds.Width * .28f, top, bounds.Width * .16f, bottom - top)),
                    1);
                graphics.FillRoundedRectangle(
                    brush,
                    Rectangle.Round(new RectangleF(bounds.Left + bounds.Width * .56f, top, bounds.Width * .16f, bottom - top)),
                    1);
                break;

            case UiIcon.Play:
                PointF[] play =
                [
                    new(bounds.Left + bounds.Width * .34f, top),
                    new(bounds.Left + bounds.Width * .74f, cy),
                    new(bounds.Left + bounds.Width * .34f, bottom),
                ];
                graphics.FillPolygon(brush, play);
                break;

            case UiIcon.Search:
                var diameter = bounds.Width * .48f;
                graphics.DrawEllipse(pen, left, top, diameter, diameter);
                graphics.DrawLine(
                    pen,
                    left + diameter * .78f,
                    top + diameter * .78f,
                    right,
                    bottom);
                break;

            case UiIcon.Close:
                graphics.DrawLine(pen, left, top, right, bottom);
                graphics.DrawLine(pen, right, top, left, bottom);
                break;

            case UiIcon.Copy:
                var back = Rectangle.Round(new RectangleF(
                    bounds.Left + bounds.Width * .18f,
                    bounds.Top + bounds.Height * .15f,
                    bounds.Width * .52f,
                    bounds.Height * .58f));
                var front = Rectangle.Round(new RectangleF(
                    bounds.Left + bounds.Width * .33f,
                    bounds.Top + bounds.Height * .30f,
                    bounds.Width * .52f,
                    bounds.Height * .58f));
                graphics.DrawRoundedRectangle(pen, back, Math.Max(2, bounds.Width / 8));
                // Reuse the shared brush (temporarily retinted) instead of allocating
                // a fresh SolidBrush per frame on the copy-button hover hot path.
                var savedColor = brush.Color;
                brush.Color = Color.FromArgb(230, Theme.Surface);
                graphics.FillRoundedRectangle(brush, front, Math.Max(2, bounds.Width / 8));
                brush.Color = savedColor;
                graphics.DrawRoundedRectangle(pen, front, Math.Max(2, bounds.Width / 8));
                break;

            case UiIcon.Trash:
                graphics.DrawLine(pen, bounds.Left + bounds.Width * .28f, bounds.Top + bounds.Height * .30f, bounds.Left + bounds.Width * .72f, bounds.Top + bounds.Height * .30f);
                graphics.DrawLine(pen, bounds.Left + bounds.Width * .40f, bounds.Top + bounds.Height * .20f, bounds.Left + bounds.Width * .60f, bounds.Top + bounds.Height * .20f);
                graphics.DrawRoundedRectangle(
                    pen,
                    Rectangle.Round(new RectangleF(bounds.Left + bounds.Width * .32f, bounds.Top + bounds.Height * .36f, bounds.Width * .36f, bounds.Height * .46f)),
                    2);
                break;

            case UiIcon.File:
                var fileRect = Rectangle.Round(new RectangleF(
                    bounds.Left + bounds.Width * .25f,
                    bounds.Top + bounds.Height * .12f,
                    bounds.Width * .50f,
                    bounds.Height * .76f));
                graphics.DrawRoundedRectangle(pen, fileRect, Math.Max(2, bounds.Width / 10));
                graphics.DrawLine(pen, fileRect.Left + bounds.Width * .13f, fileRect.Top + bounds.Height * .38f, fileRect.Right - bounds.Width * .10f, fileRect.Top + bounds.Height * .38f);
                graphics.DrawLine(pen, fileRect.Left + bounds.Width * .13f, fileRect.Top + bounds.Height * .54f, fileRect.Right - bounds.Width * .18f, fileRect.Top + bounds.Height * .54f);
                break;

            case UiIcon.Folder:
                PointF[] folder =
                [
                    new(bounds.Left + bounds.Width * .12f, bounds.Top + bounds.Height * .31f),
                    new(bounds.Left + bounds.Width * .40f, bounds.Top + bounds.Height * .31f),
                    new(bounds.Left + bounds.Width * .48f, bounds.Top + bounds.Height * .22f),
                    new(bounds.Left + bounds.Width * .86f, bounds.Top + bounds.Height * .22f),
                    new(bounds.Left + bounds.Width * .88f, bounds.Top + bounds.Height * .75f),
                    new(bounds.Left + bounds.Width * .12f, bounds.Top + bounds.Height * .75f),
                ];
                graphics.DrawPolygon(pen, folder);
                break;

            case UiIcon.Swap:
                graphics.DrawLine(pen, left, bounds.Top + bounds.Height * .38f, right, bounds.Top + bounds.Height * .38f);
                graphics.DrawLine(pen, right, bounds.Top + bounds.Height * .38f, right - bounds.Width * .15f, bounds.Top + bounds.Height * .25f);
                graphics.DrawLine(pen, right, bounds.Top + bounds.Height * .38f, right - bounds.Width * .15f, bounds.Top + bounds.Height * .51f);
                graphics.DrawLine(pen, right, bounds.Top + bounds.Height * .65f, left, bounds.Top + bounds.Height * .65f);
                graphics.DrawLine(pen, left, bounds.Top + bounds.Height * .65f, left + bounds.Width * .15f, bounds.Top + bounds.Height * .52f);
                graphics.DrawLine(pen, left, bounds.Top + bounds.Height * .65f, left + bounds.Width * .15f, bounds.Top + bounds.Height * .78f);
                break;

            case UiIcon.ChevronRight:
                graphics.DrawLine(pen, bounds.Left + bounds.Width * .40f, top, bounds.Left + bounds.Width * .65f, cy);
                graphics.DrawLine(pen, bounds.Left + bounds.Width * .65f, cy, bounds.Left + bounds.Width * .40f, bottom);
                break;

            case UiIcon.Clipboard:
                var board = Rectangle.Round(new RectangleF(
                    bounds.Left + bounds.Width * .20f,
                    bounds.Top + bounds.Height * .20f,
                    bounds.Width * .60f,
                    bounds.Height * .68f));
                graphics.DrawRoundedRectangle(pen, board, Math.Max(2, bounds.Width / 9));
                var clip = Rectangle.Round(new RectangleF(
                    bounds.Left + bounds.Width * .37f,
                    bounds.Top + bounds.Height * .12f,
                    bounds.Width * .26f,
                    bounds.Height * .18f));
                graphics.FillRoundedRectangle(brush, clip, Math.Max(1, bounds.Width / 12));
                graphics.DrawLine(pen, board.Left + bounds.Width * .15f, board.Top + bounds.Height * .28f, board.Right - bounds.Width * .14f, board.Top + bounds.Height * .28f);
                graphics.DrawLine(pen, board.Left + bounds.Width * .15f, board.Top + bounds.Height * .44f, board.Right - bounds.Width * .24f, board.Top + bounds.Height * .44f);
                break;

            case UiIcon.Minus:
                graphics.DrawLine(pen, left, cy, right, cy);
                break;

            case UiIcon.Plus:
                graphics.DrawLine(pen, left, cy, right, cy);
                graphics.DrawLine(pen, cx, top, cx, bottom);
                break;
        }
    }
}
