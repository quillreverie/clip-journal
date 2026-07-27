using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ClipJournal;

public sealed class ModernNumberStepper : Control
{
    private int _value;
    private int _hoveredPart;

    public event EventHandler<int>? ValueChanged;

    [DefaultValue(0)]
    public int Value
    {
        get => _value;
        set
        {
            var next = Math.Clamp(value, Minimum, Maximum);
            if (_value == next)
            {
                return;
            }

            _value = next;
            AccessibleDescription = Localization.BlankLineValue(_value);
            Invalidate();
            ValueChanged?.Invoke(this, _value);
        }
    }

    [DefaultValue(0)]
    public int Minimum { get; set; }

    [DefaultValue(999)]
    public int Maximum { get; set; } = 999;

    public ModernNumberStepper()
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
        Size = new Size(124, 38);
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleName = Localization.BlankLineLabel;
        AccessibleDescription = Localization.BlankLineValue(_value);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var next = e.X < Height ? 1 : e.X >= Width - Height ? 2 : 0;
        if (_hoveredPart != next)
        {
            _hoveredPart = next;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredPart = 0;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (e.X < Height)
        {
            Value--;
        }
        else if (e.X >= Width - Height)
        {
            Value++;
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        // A mouse wheel over the stepper while another control has focus would
        // silently change the spacing setting (and persist it) without the user
        // ever clicking in. Require focus so a stray scroll does not rewrite config.
        if (!Focused)
        {
            Focus();
            return;
        }

        base.OnMouseWheel(e);
        Value += Math.Sign(e.Delta);
    }

    protected override bool IsInputKey(Keys keyData)
        => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Right or Keys.Up)
        {
            Value++;
            e.Handled = true;
        }
        else if (e.KeyCode is Keys.Left or Keys.Down)
        {
            Value--;
            e.Handled = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using (var parentBrush = new SolidBrush(Parent?.BackColor ?? Theme.Surface))
        {
            graphics.FillRectangle(parentBrush, ClientRectangle);
        }

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        var radius = Theme.Scale(this, Theme.CornerRadius);
        using (var fill = new SolidBrush(Theme.SurfaceSoft))
        {
            graphics.FillRoundedRectangle(fill, rect, radius);
        }

        var partWidth = Height;
        if (_hoveredPart != 0)
        {
            var hoverRect = _hoveredPart == 1
                ? new Rectangle(1, 1, partWidth - 2, Height - 3)
                : new Rectangle(Width - partWidth + 1, 1, partWidth - 2, Height - 3);
            using var hover = new SolidBrush(Theme.PrimarySoft);
            graphics.FillRoundedRectangle(hover, hoverRect, Math.Max(2, radius - 1));
        }

        using (var border = new Pen(Focused ? Theme.Primary : Theme.Border, Math.Max(1f, DeviceDpi / 96f)))
        {
            graphics.DrawRoundedRectangle(border, rect, radius);
        }

        var iconSize = Theme.Scale(this, 14);
        var minusRect = new Rectangle((partWidth - iconSize) / 2, (Height - iconSize) / 2, iconSize, iconSize);
        var plusRect = new Rectangle(Width - partWidth + (partWidth - iconSize) / 2, (Height - iconSize) / 2, iconSize, iconSize);
        UiIconPainter.Draw(graphics, UiIcon.Minus, minusRect, _value <= Minimum ? Theme.TextMuted : Theme.TextSecondary);
        UiIconPainter.Draw(graphics, UiIcon.Plus, plusRect, _value >= Maximum ? Theme.TextMuted : Theme.TextSecondary);

        var valueRect = new Rectangle(partWidth, 0, Math.Max(1, Width - partWidth * 2), Height);
        TextRenderer.DrawText(
            graphics,
            _value.ToString(),
            Theme.FontMainMedium,
            valueRect,
            Theme.TextPrimary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}
