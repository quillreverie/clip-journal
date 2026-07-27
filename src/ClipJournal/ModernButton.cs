using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ClipJournal;

public enum ModernButtonStyle
{
    Primary,
    Secondary,
    Soft,
    Ghost,
    Danger,
}

public sealed class ModernButton : Control, IButtonControl
{
    private const int IconSizeLogical = 16;
    private const int IconTextGapLogical = 6;

    private readonly System.Windows.Forms.Timer _animationTimer = new();
    private float _hoverProgress;
    private bool _hovered;
    private bool _pressed;
    private ModernButtonStyle _style = ModernButtonStyle.Secondary;
    private UiIcon _icon;

    [Category("Appearance")]
    [DefaultValue(ModernButtonStyle.Secondary)]
    public ModernButtonStyle Style
    {
        get => _style;
        set
        {
            _style = value;
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(UiIcon.None)]
    public UiIcon Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            RecalculateWidth();
            Invalidate();
        }
    }

    [Category("Layout")]
    [DefaultValue(true)]
    public bool AutoFitWidth { get; set; } = true;

    [Category("Behavior")]
    [DefaultValue(DialogResult.None)]
    public DialogResult DialogResult { get; set; }

    public ModernButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.SupportsTransparentBackColor,
            true);
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);

        DoubleBuffered = true;
        Size = new Size(96, 38);
        Font = Theme.FontMainMedium;
        Cursor = Cursors.Hand;

        _animationTimer.Interval = 15;
        _animationTimer.Tick += Animate;
    }

    public void RecalculateWidth()
    {
        if (!AutoFitWidth || string.IsNullOrWhiteSpace(Text))
        {
            return;
        }

        var textSize = TextRenderer.MeasureText(
            Text,
            Font,
            Size.Empty,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        var iconSpace = _icon == UiIcon.None
            ? 0
            : Theme.Scale(this, IconSizeLogical + IconTextGapLogical);
        Width = Math.Max(Theme.Scale(this, 76), textSize.Width + iconSpace + Theme.Scale(this, 28));
    }

    public void NotifyDefault(bool value)
    {
    }

    public void PerformClick()
    {
        if (Enabled && Visible)
        {
            OnClick(EventArgs.Empty);
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        RecalculateWidth();
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovered = true;
        _animationTimer.Start();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovered = false;
        _pressed = false;
        _animationTimer.Start();
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        if (mevent.Button == MouseButtons.Left && Enabled)
        {
            Focus();
            Capture = true;
            _pressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        var shouldClick =
            mevent.Button == MouseButtons.Left &&
            _pressed &&
            Enabled &&
            ClientRectangle.Contains(mevent.Location);

        Capture = false;
        _pressed = false;
        base.OnMouseUp(mevent);
        Invalidate();

        if (shouldClick)
        {
            OnClick(EventArgs.Empty);
        }
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture && _pressed)
        {
            _pressed = false;
            Invalidate();
        }
    }

    protected override bool IsInputKey(Keys keyData)
        => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if ((e.KeyCode is Keys.Space or Keys.Enter) && Enabled)
        {
            _pressed = true;
            Invalidate();
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if ((e.KeyCode is Keys.Space or Keys.Enter) && _pressed)
        {
            _pressed = false;
            Invalidate();
            PerformClick();
            e.Handled = true;
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (DialogResult != DialogResult.None && FindForm() is { } form)
        {
            form.DialogResult = DialogResult;
        }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    private void Animate(object? sender, EventArgs e)
    {
        var target = _hovered && Enabled ? 1f : 0f;
        _hoverProgress += (target - _hoverProgress) * .24f;
        if (Math.Abs(target - _hoverProgress) < .025f)
        {
            _hoverProgress = target;
            _animationTimer.Stop();
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using (var parentBrush = new SolidBrush(Parent?.BackColor ?? Theme.Background))
        {
            graphics.FillRectangle(parentBrush, ClientRectangle);
        }

        var inset = Theme.Scale(this, 1);
        var rect = new Rectangle(inset, inset, Width - inset * 2 - 1, Height - inset * 2 - 1);
        if (_pressed && Enabled)
        {
            rect.Offset(0, Theme.Scale(this, 1));
        }

        GetPalette(out var normalBackground, out var hoverBackground, out var normalBorder, out var hoverBorder, out var normalText, out var hoverText);
        var background = Theme.Interpolate(normalBackground, hoverBackground, _hoverProgress);
        var border = Theme.Interpolate(normalBorder, hoverBorder, _hoverProgress);
        var foreground = Theme.Interpolate(normalText, hoverText, _hoverProgress);

        if (!Enabled)
        {
            background = Theme.SurfaceSoft;
            border = Theme.Border;
            foreground = Theme.TextMuted;
        }

        var radius = Theme.Scale(this, Theme.CornerRadius);
        using (var fill = new SolidBrush(background))
        {
            graphics.FillRoundedRectangle(fill, rect, radius);
        }

        if (border.A > 0)
        {
            using var borderPen = new Pen(border, Math.Max(1f, DeviceDpi / 96f));
            graphics.DrawRoundedRectangle(borderPen, rect, radius);
        }

        var iconSize = Theme.Scale(this, IconSizeLogical);
        var gap = Theme.Scale(this, IconTextGapLogical);
        var textSize = string.IsNullOrWhiteSpace(Text)
            ? Size.Empty
            : TextRenderer.MeasureText(
                graphics,
                Text,
                Font,
                Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        var contentWidth = textSize.Width + (_icon == UiIcon.None ? 0 : iconSize + (textSize.Width > 0 ? gap : 0));
        var contentLeft = rect.Left + (rect.Width - contentWidth) / 2;

        if (_icon != UiIcon.None)
        {
            var iconRect = new Rectangle(contentLeft, rect.Top + (rect.Height - iconSize) / 2, iconSize, iconSize);
            UiIconPainter.Draw(graphics, _icon, iconRect, foreground, Math.Max(1.4f, 1.6f * DeviceDpi / 96f));
            contentLeft = iconRect.Right + gap;
        }

        if (!string.IsNullOrWhiteSpace(Text))
        {
            var textRect = new Rectangle(contentLeft, rect.Top, Math.Max(1, rect.Right - contentLeft), rect.Height);
            TextRenderer.DrawText(
                graphics,
                Text,
                Font,
                textRect,
                foreground,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        if (Focused && ShowFocusCues)
        {
            var focusRect = Rectangle.Inflate(rect, -Theme.Scale(this, 3), -Theme.Scale(this, 3));
            using var focusPen = new Pen(Color.FromArgb(165, _style == ModernButtonStyle.Primary ? Color.White : Theme.Primary))
            {
                DashStyle = DashStyle.Dot,
            };
            graphics.DrawRoundedRectangle(focusPen, focusRect, Math.Max(2, radius - Theme.Scale(this, 3)));
        }
    }

    private void GetPalette(
        out Color normalBackground,
        out Color hoverBackground,
        out Color normalBorder,
        out Color hoverBorder,
        out Color normalText,
        out Color hoverText)
    {
        switch (_style)
        {
            case ModernButtonStyle.Primary:
                normalBackground = Theme.Primary;
                hoverBackground = _pressed ? Theme.PrimaryPressed : Theme.PrimaryHover;
                normalBorder = Color.Transparent;
                hoverBorder = Color.Transparent;
                normalText = Theme.TextOnAccent;
                hoverText = Theme.TextOnAccent;
                break;

            case ModernButtonStyle.Danger:
                normalBackground = Theme.DangerSoft;
                hoverBackground = Theme.Danger;
                normalBorder = Color.Transparent;
                hoverBorder = Color.Transparent;
                normalText = Theme.Danger;
                hoverText = Color.White;
                break;

            case ModernButtonStyle.Soft:
                normalBackground = Theme.SurfaceSoft;
                hoverBackground = Theme.PrimarySoft;
                normalBorder = Color.Transparent;
                hoverBorder = Color.Transparent;
                normalText = Theme.TextSecondary;
                hoverText = Theme.PrimaryText;
                break;

            case ModernButtonStyle.Ghost:
                normalBackground = Color.Transparent;
                hoverBackground = Theme.SurfaceSoft;
                normalBorder = Color.Transparent;
                hoverBorder = Color.Transparent;
                normalText = Theme.TextSecondary;
                hoverText = Theme.TextPrimary;
                break;

            default:
                normalBackground = Theme.Surface;
                hoverBackground = Theme.SurfaceHover;
                normalBorder = Theme.Border;
                hoverBorder = Theme.BorderStrong;
                normalText = Theme.TextPrimary;
                hoverText = Theme.PrimaryText;
                break;
        }
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
