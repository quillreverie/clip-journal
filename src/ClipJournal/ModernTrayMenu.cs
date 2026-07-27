using System.Drawing.Drawing2D;

namespace ClipJournal;

public sealed class ModernTrayMenu : ContextMenuStrip
{
    private const int ItemWidthLogical = 200;
    private const int ItemHeightLogical = 28;
    private const int SeparatorHeightLogical = 7;
    private const int OuterPaddingLogical = 5;

    public ModernTrayMenu()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        AutoSize = true;
        BackColor = Theme.Surface;
        ForeColor = Theme.TextPrimary;
        Font = Theme.FontMain;
        GripStyle = ToolStripGripStyle.Hidden;
        DropShadowEnabled = true;
        ShowCheckMargin = false;
        ShowImageMargin = false;
        Renderer = new ModernTrayMenuRenderer();
        ApplyMetrics(DeviceDpi);
    }

    public ModernTrayMenuItem AddAction(
        string text,
        UiIcon icon,
        EventHandler onClick,
        bool emphasized = false,
        bool danger = false)
    {
        var item = new ModernTrayMenuItem(text, icon)
        {
            AutoSize = false,
            Font = emphasized ? Theme.FontMainMedium : Theme.FontMain,
            Emphasized = emphasized,
            Danger = danger,
            AccessibleName = text,
        };
        item.Click += onClick;
        Items.Add(item);
        ApplyItemMetrics(item, DeviceDpi);
        return item;
    }

    public void AddDivider()
    {
        var separator = new ToolStripSeparator
        {
            AutoSize = false,
        };
        Items.Add(separator);
        ApplyItemMetrics(separator, DeviceDpi);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyMetrics(DeviceDpi);
    }

    protected override void RescaleConstantsForDpi(int oldDpi, int newDpi)
    {
        base.RescaleConstantsForDpi(oldDpi, newDpi);
        ApplyMetrics(newDpi);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        using var path = Theme.CreateRoundedRectangle(
            new Rectangle(0, 0, Width, Height),
            Theme.Scale(this, 10));
        var oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }

    private void ApplyMetrics(int dpi)
    {
        var outerPadding = Scale(OuterPaddingLogical, dpi);
        Padding = new Padding(outerPadding);
        MinimumSize = new Size(
            Scale(ItemWidthLogical + OuterPaddingLogical * 2, dpi),
            0);
        foreach (ToolStripItem item in Items)
        {
            ApplyItemMetrics(item, dpi);
        }

        PerformLayout();
    }

    private static void ApplyItemMetrics(ToolStripItem item, int dpi)
    {
        item.Size = item is ToolStripSeparator
            ? new Size(Scale(ItemWidthLogical, dpi), Scale(SeparatorHeightLogical, dpi))
            : new Size(Scale(ItemWidthLogical, dpi), Scale(ItemHeightLogical, dpi));
    }

    private static int Scale(int logicalPixels, int dpi)
        => (int)Math.Round(logicalPixels * dpi / 96f);
}

public sealed class ModernTrayMenuItem : ToolStripMenuItem
{
    private UiIcon _icon;

    public UiIcon Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            Owner?.Invalidate(Bounds);
        }
    }

    public bool Emphasized { get; init; }

    public bool Danger { get; init; }

    public ModernTrayMenuItem(string text, UiIcon icon)
        : base(text)
    {
        _icon = icon;
        DisplayStyle = ToolStripItemDisplayStyle.Text;
        TextAlign = ContentAlignment.MiddleLeft;
    }
}

internal sealed class ModernTrayMenuRenderer : ToolStripProfessionalRenderer
{
    public ModernTrayMenuRenderer()
        : base(new ModernTrayColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var background = new SolidBrush(Theme.Surface);
        e.Graphics.FillRectangle(background, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        using var border = new Pen(Theme.BorderStrong, Math.Max(1f, e.ToolStrip.DeviceDpi / 96f));
        e.Graphics.DrawRoundedRectangle(border, rect, Theme.Scale(e.ToolStrip, 10));
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || e.Item is not ModernTrayMenuItem item)
        {
            return;
        }

        var toolStrip = e.ToolStrip ?? item.Owner;
        if (toolStrip is null)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var horizontalInset = Theme.Scale(toolStrip, 4);
        var verticalInset = Theme.Scale(toolStrip, 2);
        var rect = new Rectangle(
            horizontalInset,
            verticalInset,
            Math.Max(1, item.Width - horizontalInset * 2),
            Math.Max(1, item.Height - verticalInset * 2));
        using var background = new SolidBrush(item.Danger ? Theme.DangerSoft : Theme.PrimarySoft);
        e.Graphics.FillRoundedRectangle(background, rect, Theme.Scale(toolStrip, 7));
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item is not ModernTrayMenuItem item)
        {
            base.OnRenderItemText(e);
            return;
        }

        var toolStrip = e.ToolStrip ?? item.Owner;
        if (toolStrip is null)
        {
            base.OnRenderItemText(e);
            return;
        }

        var iconSize = Theme.Scale(toolStrip, 18);
        var iconLeft = Theme.Scale(toolStrip, 14);
        var iconRect = new Rectangle(
            iconLeft,
            (item.Height - iconSize) / 2,
            iconSize,
            iconSize);

        var foreground = item.Danger
            ? Theme.Danger
            : item.Selected || item.Emphasized
                ? Theme.PrimaryText
                : Theme.TextSecondary;

        UiIconPainter.Draw(
            e.Graphics,
            item.Icon,
            iconRect,
            foreground,
            Math.Max(1.4f, 1.55f * toolStrip.DeviceDpi / 96f));

        var textLeft = iconRect.Right + Theme.Scale(toolStrip, 6);
        var textRect = new Rectangle(
            textLeft,
            0,
            Math.Max(1, item.Width - textLeft - Theme.Scale(toolStrip, 12)),
            item.Height);
        TextRenderer.DrawText(
            e.Graphics,
            item.Text,
            item.Font,
            textRect,
            foreground,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine |
            TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var toolStrip = e.ToolStrip ?? e.Item.Owner;
        if (toolStrip is null)
        {
            return;
        }

        var y = e.Item.Height / 2;
        using var divider = new Pen(Theme.Divider, Math.Max(1f, toolStrip.DeviceDpi / 96f));
        e.Graphics.DrawLine(
            divider,
            Theme.Scale(toolStrip, 14),
            y,
            e.Item.Width - Theme.Scale(toolStrip, 14),
            y);
    }

    private sealed class ModernTrayColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Surface;
        public override Color ImageMarginGradientBegin => Theme.Surface;
        public override Color ImageMarginGradientMiddle => Theme.Surface;
        public override Color ImageMarginGradientEnd => Theme.Surface;
        public override Color MenuItemSelected => Theme.PrimarySoft;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color SeparatorDark => Theme.Divider;
        public override Color SeparatorLight => Theme.Surface;
    }
}
