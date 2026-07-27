using System.Drawing.Drawing2D;

namespace ClipJournal;

public sealed class SettingsDrawer : Panel
{
    private readonly Label _titleLabel = new();
    private readonly Label _pathLabel = new();
    private readonly PathSurface _pathSurface = new();
    private readonly ModernButton _openFileButton = new();
    private readonly ModernButton _openFolderButton = new();
    private readonly ModernButton _changeFileButton = new();
    private readonly Label _blankLineLabel = new();
    private readonly Label _blankLineHelp = new();
    private readonly ModernNumberStepper _blankLineStepper = new();
    private bool _suppressValueChanged;

    public event EventHandler? OpenFileClicked;
    public event EventHandler? OpenFolderClicked;
    public event EventHandler? ChangeFileClicked;
    public event EventHandler<int>? BlankEveryChanged;

    public string FilePath
    {
        get => _pathSurface.PathText;
        set => _pathSurface.PathText = value;
    }

    public int BlankLineEvery
    {
        get => _blankLineStepper.Value;
        set
        {
            _suppressValueChanged = true;
            _blankLineStepper.Value = Math.Clamp(value, 0, 999);
            _suppressValueChanged = false;
        }
    }

    public SettingsDrawer()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        DoubleBuffered = true;
        Height = 344;
        BackColor = Theme.SidebarBackground;

        ConfigureLabel(_titleLabel, Localization.StorageTitle, Theme.FontTitle, Theme.TextPrimary);
        ConfigureLabel(_pathLabel, Localization.StoragePathLabel, Theme.FontSmallMedium, Theme.TextSecondary);
        ConfigureLabel(_blankLineLabel, Localization.BlankLineLabel, Theme.FontMainMedium, Theme.TextPrimary);
        ConfigureLabel(_blankLineHelp, Localization.BlankLineDescription, Theme.FontSmall, Theme.TextSecondary);
        _blankLineHelp.AutoSize = false;

        ConfigureButton(_openFileButton, Localization.OpenFileShort, UiIcon.File);
        ConfigureButton(_openFolderButton, Localization.OpenFolderShort, UiIcon.Folder);
        ConfigureButton(_changeFileButton, Localization.ChangeFileShort, UiIcon.Swap);

        _openFileButton.Click += (_, _) => OpenFileClicked?.Invoke(this, EventArgs.Empty);
        _openFolderButton.Click += (_, _) => OpenFolderClicked?.Invoke(this, EventArgs.Empty);
        _changeFileButton.Click += (_, _) => ChangeFileClicked?.Invoke(this, EventArgs.Empty);
        _blankLineStepper.ValueChanged += (_, value) =>
        {
            if (!_suppressValueChanged)
            {
                BlankEveryChanged?.Invoke(this, value);
            }
        };

        Controls.Add(_titleLabel);
        Controls.Add(_pathLabel);
        Controls.Add(_pathSurface);
        Controls.Add(_openFileButton);
        Controls.Add(_openFolderButton);
        Controls.Add(_changeFileButton);
        Controls.Add(_blankLineLabel);
        Controls.Add(_blankLineHelp);
        Controls.Add(_blankLineStepper);

        Resize += (_, _) => LayoutChildren();
        LayoutChildren();
    }

    private static void ConfigureLabel(Label label, string text, Font font, Color color)
    {
        label.Text = text;
        label.Font = font;
        label.ForeColor = color;
        label.BackColor = Color.Transparent;
        label.AutoSize = true;
        label.UseMnemonic = false;
    }

    private static void ConfigureButton(ModernButton button, string text, UiIcon icon)
    {
        button.Text = text;
        button.Icon = icon;
        button.Style = ModernButtonStyle.Soft;
        button.AutoFitWidth = false;
        button.Font = Theme.FontSmallMedium;
    }

    private void LayoutChildren()
    {
        var padding = Theme.Scale(this, 16);
        var innerWidth = Math.Max(1, Width - padding * 2);

        _titleLabel.Location = new Point(padding, Theme.Scale(this, 15));
        _pathLabel.Location = new Point(padding, Theme.Scale(this, 51));

        _pathSurface.Location = new Point(padding, Theme.Scale(this, 73));
        _pathSurface.Size = new Size(innerWidth, Theme.Scale(this, 43));

        var buttonTop = Theme.Scale(this, 126);
        var gap = Theme.Scale(this, 10);
        var buttonWidth = Math.Max(Theme.Scale(this, 80), (innerWidth - gap) / 2);
        var buttonHeight = Theme.Scale(this, 36);
        _openFileButton.SetBounds(padding, buttonTop, buttonWidth, buttonHeight);
        _openFolderButton.SetBounds(_openFileButton.Right + gap, buttonTop, innerWidth - buttonWidth - gap, buttonHeight);
        _changeFileButton.SetBounds(padding, _openFileButton.Bottom + gap, innerWidth, buttonHeight);

        _blankLineLabel.Location = new Point(padding, Theme.Scale(this, 230));
        _blankLineHelp.Location = new Point(padding, Theme.Scale(this, 256));
        _blankLineHelp.Size = new Size(innerWidth, Theme.Scale(this, 34));

        _blankLineStepper.Location = new Point(padding, Theme.Scale(this, 294));
        _blankLineStepper.Size = new Size(Theme.Scale(this, 126), Theme.Scale(this, 36));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (var parentBrush = new SolidBrush(Parent?.BackColor ?? Theme.SidebarBackground))
        {
            graphics.FillRectangle(parentBrush, ClientRectangle);
        }

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        var radius = Theme.Scale(this, Theme.LargeCornerRadius);
        using (var fill = new SolidBrush(Theme.Surface))
        {
            graphics.FillRoundedRectangle(fill, rect, radius);
        }

        using (var border = new Pen(Theme.Border, Math.Max(1f, DeviceDpi / 96f)))
        {
            graphics.DrawRoundedRectangle(border, rect, radius);
        }

        var dividerY = Theme.Scale(this, 218);
        using var divider = new Pen(Theme.Divider, Math.Max(1f, DeviceDpi / 96f));
        graphics.DrawLine(divider, Theme.Scale(this, 16), dividerY, Width - Theme.Scale(this, 16), dividerY);
    }

    private sealed class PathSurface : Control
    {
        private const int IconTextGapLogical = 6;

        private string _pathText = string.Empty;

        public string PathText
        {
            get => _pathText;
            set
            {
                _pathText = value ?? string.Empty;
                AccessibleName = Localization.StoragePathLabel;
                AccessibleDescription = _pathText;
                Invalidate();
            }
        }

        public PathSurface()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var fill = new SolidBrush(Theme.SurfaceSoft))
            {
                graphics.FillRoundedRectangle(fill, rect, Theme.Scale(this, Theme.CornerRadius));
            }

            var iconSize = Theme.Scale(this, 17);
            var iconRect = new Rectangle(Theme.Scale(this, 12), (Height - iconSize) / 2, iconSize, iconSize);
            UiIconPainter.Draw(graphics, UiIcon.Folder, iconRect, Theme.TextSecondary, Math.Max(1.3f, 1.5f * DeviceDpi / 96f));

            var textLeft = iconRect.Right + Theme.Scale(this, IconTextGapLogical);
            var textRect = new Rectangle(textLeft, 0, Math.Max(1, Width - textLeft - Theme.Scale(this, 10)), Height);
            TextRenderer.DrawText(
                graphics,
                _pathText,
                Theme.FontSmall,
                textRect,
                Theme.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis);
        }
    }
}
