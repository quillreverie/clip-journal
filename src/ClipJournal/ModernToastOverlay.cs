using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ClipJournal;

public sealed class ModernToastOverlay : Control
{
    private readonly Queue<(string Message, bool IsError)> _queue = new();
    private readonly System.Windows.Forms.Timer _animationTimer = new();
    private string _message = string.Empty;
    private bool _isError;
    private float _progress;
    private int _holdTicks;
    private ToastPhase _phase;

    private enum ToastPhase
    {
        Hidden,
        Entering,
        Holding,
        Leaving,
    }

    public ModernToastOverlay()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);

        DoubleBuffered = true;
        Size = new Size(360, 50);
        Visible = false;
        TabStop = false;

        _animationTimer.Interval = 16;
        _animationTimer.Tick += Animate;
        ParentChanged += (_, _) => UpdatePosition();
    }

    public void ShowToast(string message, bool isError = false)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (_phase is ToastPhase.Entering or ToastPhase.Holding)
        {
            // Drop the newest candidate (not the oldest) when the backlog is full,
            // so a burst of similar error toasts does not silently discard the first
            // — usually the most informative — error in the series.
            if (_queue.Count < 3)
            {
                _queue.Enqueue((message, isError));
            }

            return;
        }

        BeginToast(message, isError);
    }

    public void UpdatePosition()
    {
        if (Parent is null)
        {
            return;
        }

        var margin = Theme.Scale(this, 18);
        Width = Math.Min(Theme.Scale(this, 360), Math.Max(Theme.Scale(this, 240), Parent.ClientSize.Width - margin * 2));
        Height = Theme.Scale(this, 50);
        Location = new Point(Math.Max(margin, Parent.ClientSize.Width - Width - margin), margin);
        BringToFront();
    }

    private void BeginToast(string message, bool isError)
    {
        _message = message;
        _isError = isError;
        _progress = 0f;
        _holdTicks = 0;
        _phase = ToastPhase.Entering;
        AccessibleName = message;
        UpdatePosition();
        Visible = true;
        BringToFront();
        _animationTimer.Start();
    }

    private void Animate(object? sender, EventArgs e)
    {
        var needsInvalidate = true;
        switch (_phase)
        {
            case ToastPhase.Entering:
                _progress += (1f - _progress) * .22f;
                if (_progress >= .98f)
                {
                    _progress = 1f;
                    _phase = ToastPhase.Holding;
                }
                break;

            case ToastPhase.Holding:
                _holdTicks++;
                if (_holdTicks >= 145)
                {
                    _phase = ToastPhase.Leaving;
                }
                else
                {
                    // Holding has no visual change each tick; skip the ~145
                    // redundant invalidations (about 2.3s of empty redraws).
                    needsInvalidate = false;
                }
                break;

            case ToastPhase.Leaving:
                _progress += (0f - _progress) * .18f;
                if (_progress <= .03f)
                {
                    _progress = 0f;
                    Visible = false;
                    if (_queue.Count > 0)
                    {
                        var next = _queue.Dequeue();
                        BeginToast(next.Message, next.IsError);
                    }
                    else
                    {
                        _phase = ToastPhase.Hidden;
                        _animationTimer.Stop();
                    }
                }
                break;
        }

        if (needsInvalidate)
        {
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_message))
        {
            return;
        }

        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var offsetX = (int)Math.Round((1f - _progress) * Theme.Scale(this, 20));
        var shadowRect = new Rectangle(offsetX + Theme.Scale(this, 2), Theme.Scale(this, 3), Width - offsetX - Theme.Scale(this, 3), Height - Theme.Scale(this, 4));
        using (var shadow = new SolidBrush(Color.FromArgb(18, 20, 24, 34)))
        {
            graphics.FillRoundedRectangle(shadow, shadowRect, Theme.Scale(this, 12));
        }

        var rect = new Rectangle(offsetX, 0, Width - offsetX - Theme.Scale(this, 3), Height - Theme.Scale(this, 4));
        using (var fill = new SolidBrush(Theme.Surface))
        {
            graphics.FillRoundedRectangle(fill, rect, Theme.Scale(this, 12));
        }

        using (var border = new Pen(_isError ? Color.FromArgb(238, 191, 197) : Theme.Border, Math.Max(1f, DeviceDpi / 96f)))
        {
            graphics.DrawRoundedRectangle(border, rect, Theme.Scale(this, 12));
        }

        var accent = _isError ? Theme.Danger : Theme.Primary;
        var accentRect = new Rectangle(rect.Left + Theme.Scale(this, 7), rect.Top + Theme.Scale(this, 9), Theme.Scale(this, 4), rect.Height - Theme.Scale(this, 18));
        using (var accentBrush = new SolidBrush(accent))
        {
            graphics.FillRoundedRectangle(accentBrush, accentRect, Theme.Scale(this, 2));
        }

        var textRect = new Rectangle(
            rect.Left + Theme.Scale(this, 22),
            rect.Top,
            Math.Max(1, rect.Width - Theme.Scale(this, 34)),
            rect.Height);
        TextRenderer.DrawText(
            graphics,
            _message,
            Theme.FontMainMedium,
            textRect,
            Theme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
