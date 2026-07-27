using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ClipJournal;

public sealed class ModernStatusBadge : Control
{
    private readonly System.Windows.Forms.Timer _pulseTimer = new();
    private bool _isListening = true;
    private bool _hovered;
    private float _pulseProgress;
    private int _count;

    [Category("Behavior")]
    [DefaultValue(true)]
    public bool IsListening
    {
        get => _isListening;
        set
        {
            if (_isListening == value)
            {
                return;
            }

            _isListening = value;
            UpdateTimerState();
            UpdateAccessibility();
            Invalidate();
        }
    }

    [Category("Data")]
    [DefaultValue(0)]
    public int Count
    {
        get => _count;
        set
        {
            _count = Math.Max(0, value);
            UpdateAccessibility();
            Invalidate();
        }
    }

    public ModernStatusBadge()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.SupportsTransparentBackColor,
            true);

        DoubleBuffered = true;
        Size = new Size(224, 76);
        Cursor = Cursors.Hand;
        TabStop = true;

        _pulseTimer.Interval = 32;
        _pulseTimer.Tick += (_, _) =>
        {
            _pulseProgress += .035f;
            if (_pulseProgress >= 1f)
            {
                _pulseProgress -= 1f;
            }

            if (_isListening && Visible)
            {
                Invalidate();
            }
        };

        VisibleChanged += (_, _) => UpdateTimerState();
        UpdateAccessibility();
    }

    public void RecalculateSize()
    {
        Height = Theme.Scale(this, 76);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void UpdateTimerState()
    {
        if (_isListening && Visible)
        {
            _pulseTimer.Start();
        }
        else
        {
            _pulseTimer.Stop();
            _pulseProgress = 0f;
        }
    }

    private void UpdateAccessibility()
    {
        AccessibleName = _isListening ? Localization.ListeningHeadline : Localization.PausedHeadline;
        AccessibleDescription = Localization.LocalCountDescription(_count);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using (var parentBrush = new SolidBrush(Parent?.BackColor ?? Theme.SidebarBackground))
        {
            graphics.FillRectangle(parentBrush, ClientRectangle);
        }

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        var accent = _isListening ? Theme.Success : Theme.Warning;
        var background = _isListening ? Theme.SuccessSoft : Theme.WarningSoft;
        if (_hovered)
        {
            background = Theme.Interpolate(background, Theme.Surface, .26f);
        }

        var radius = Theme.Scale(this, Theme.LargeCornerRadius);
        using (var fill = new SolidBrush(background))
        {
            graphics.FillRoundedRectangle(fill, rect, radius);
        }

        using (var border = new Pen(Color.FromArgb(58, accent), Math.Max(1f, DeviceDpi / 96f)))
        {
            graphics.DrawRoundedRectangle(border, rect, radius);
        }

        var dotSize = Theme.Scale(this, 9);
        var dotCenter = new PointF(Theme.Scale(this, 23), Theme.Scale(this, 25));
        if (_isListening)
        {
            var eased = (float)(.5 - Math.Cos(_pulseProgress * Math.PI * 2) / 2);
            var auraSize = dotSize + Theme.Scale(this, 8) * eased;
            var auraAlpha = (int)(60 * (1f - eased));
            using var aura = new SolidBrush(Color.FromArgb(auraAlpha, accent));
            graphics.FillEllipse(
                aura,
                dotCenter.X - auraSize / 2f,
                dotCenter.Y - auraSize / 2f,
                auraSize,
                auraSize);
        }

        using (var dot = new SolidBrush(accent))
        {
            graphics.FillEllipse(dot, dotCenter.X - dotSize / 2f, dotCenter.Y - dotSize / 2f, dotSize, dotSize);
        }

        var textLeft = Theme.Scale(this, 40);
        var titleRect = new Rectangle(textLeft, Theme.Scale(this, 11), Width - textLeft - Theme.Scale(this, 16), Theme.Scale(this, 27));
        TextRenderer.DrawText(
            graphics,
            _isListening ? Localization.ListeningHeadline : Localization.PausedHeadline,
            Theme.FontMainMedium,
            titleRect,
            accent,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        var detailRect = new Rectangle(textLeft, Theme.Scale(this, 38), Width - textLeft - Theme.Scale(this, 16), Theme.Scale(this, 25));
        TextRenderer.DrawText(
            graphics,
            Localization.LocalCountDescription(_count),
            Theme.FontSmall,
            detailRect,
            Theme.TextSecondary,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        var chevronSize = Theme.Scale(this, 14);
        var chevronRect = new Rectangle(Width - Theme.Scale(this, 25), (Height - chevronSize) / 2, chevronSize, chevronSize);
        UiIconPainter.Draw(graphics, UiIcon.ChevronRight, chevronRect, Color.FromArgb(155, accent), Math.Max(1.4f, 1.6f * DeviceDpi / 96f));

        if (Focused && ShowFocusCues)
        {
            var focusRect = Rectangle.Inflate(rect, -Theme.Scale(this, 4), -Theme.Scale(this, 4));
            using var focusPen = new Pen(Color.FromArgb(170, accent)) { DashStyle = DashStyle.Dot };
            graphics.DrawRoundedRectangle(focusPen, focusRect, Math.Max(2, radius - Theme.Scale(this, 4)));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pulseTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
