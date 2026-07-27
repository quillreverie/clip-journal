using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ClipJournal;

public sealed class ModernSearchBar : Panel
{
    private readonly TextBox _textBox = new();
    private readonly ModernButton _clearButton = new();
    private readonly System.Windows.Forms.Timer _focusTimer = new();
    private bool _focused;
    private float _focusProgress;
    private string _placeholder = Localization.SearchPlaceholder;

    public event EventHandler? SearchTextChanged;

    [Browsable(false)]
    public string SearchText
    {
        get => _textBox.Text;
        set => _textBox.Text = value;
    }

    public string Placeholder
    {
        get => _placeholder;
        set
        {
            _placeholder = value;
            _textBox.PlaceholderText = value;
            Invalidate();
        }
    }

    public ModernSearchBar()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        DoubleBuffered = true;
        Size = new Size(360, 40);
        BackColor = Theme.Surface;
        Padding = Padding.Empty;
        TabStop = true;
        AccessibleName = Localization.SearchPlaceholder;

        _textBox.BorderStyle = BorderStyle.None;
        _textBox.Font = Theme.FontMain;
        _textBox.ForeColor = Theme.TextPrimary;
        _textBox.BackColor = Theme.Surface;
        _textBox.PlaceholderText = _placeholder;
        _textBox.AccessibleName = Localization.SearchPlaceholder;
        _textBox.TabStop = false;
        _textBox.TextChanged += (_, _) =>
        {
            _clearButton.Visible = _textBox.TextLength > 0;
            UpdatePlaceholder();
            SearchTextChanged?.Invoke(this, EventArgs.Empty);
        };
        _textBox.Enter += (_, _) =>
        {
            _focused = true;
            UpdatePlaceholder();
            _focusTimer.Start();
        };
        _textBox.Leave += (_, _) =>
        {
            _focused = false;
            UpdatePlaceholder();
            _focusTimer.Start();
        };

        _clearButton.AutoFitWidth = false;
        _clearButton.Icon = UiIcon.Close;
        _clearButton.Style = ModernButtonStyle.Ghost;
        _clearButton.Size = new Size(28, 28);
        _clearButton.Visible = false;
        _clearButton.TabStop = false;
        _clearButton.AccessibleName = Localization.ClearSearch;
        _clearButton.Click += (_, _) =>
        {
            _textBox.Clear();
            _textBox.Focus();
        };

        _focusTimer.Interval = 15;
        _focusTimer.Tick += (_, _) =>
        {
            var target = _focused ? 1f : 0f;
            _focusProgress += (target - _focusProgress) * .26f;
            if (Math.Abs(target - _focusProgress) < .025f)
            {
                _focusProgress = target;
                _focusTimer.Stop();
            }

            Invalidate();
        };

        Controls.Add(_textBox);
        Controls.Add(_clearButton);
        Resize += (_, _) => LayoutChildren();
        LayoutChildren();
        UpdatePlaceholder();
    }

    public void FocusInput()
    {
        _textBox.Visible = true;
        _textBox.Focus();
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        FocusInput();
    }

    private void LayoutChildren()
    {
        var iconArea = Theme.Scale(this, 40);
        var clearWidth = Theme.Scale(this, 32);
        var innerHeight = _textBox.PreferredHeight;
        _textBox.Location = new Point(iconArea, Math.Max(0, (Height - innerHeight) / 2));
        _textBox.Width = Math.Max(Theme.Scale(this, 60), Width - iconArea - clearWidth - Theme.Scale(this, 8));
        _clearButton.Size = new Size(Theme.Scale(this, 28), Theme.Scale(this, 28));
        _clearButton.Location = new Point(Width - _clearButton.Width - Theme.Scale(this, 6), (Height - _clearButton.Height) / 2);
        _clearButton.BringToFront();
    }

    private void UpdatePlaceholder()
    {
        _textBox.Visible = _focused || _textBox.TextLength > 0;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _textBox.Visible = true;
        _textBox.Focus();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (var parentBrush = new SolidBrush(Parent?.BackColor ?? Theme.Background))
        {
            graphics.FillRectangle(parentBrush, ClientRectangle);
        }

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        var radius = Theme.Scale(this, Theme.CornerRadius);
        using (var fill = new SolidBrush(Theme.Surface))
        {
            graphics.FillRoundedRectangle(fill, rect, radius);
        }

        var borderColor = Theme.Interpolate(Theme.Border, Theme.Primary, _focusProgress);
        using (var border = new Pen(borderColor, Math.Max(1f, (1f + _focusProgress * .45f) * DeviceDpi / 96f)))
        {
            graphics.DrawRoundedRectangle(border, rect, radius);
        }

        var iconSize = Theme.Scale(this, 17);
        var iconRect = new Rectangle(
            Theme.Scale(this, 13),
            (Height - iconSize) / 2,
            iconSize,
            iconSize);
        UiIconPainter.Draw(
            graphics,
            UiIcon.Search,
            iconRect,
            Theme.Interpolate(Theme.TextMuted, Theme.Primary, _focusProgress),
            Math.Max(1.4f, 1.6f * DeviceDpi / 96f));

        if (_textBox.TextLength == 0 && !_focused)
        {
            var placeholderRect = new Rectangle(
                Theme.Scale(this, 40),
                0,
                Math.Max(1, Width - Theme.Scale(this, 80)),
                Height);
            TextRenderer.DrawText(
                graphics,
                _placeholder,
                Theme.FontMain,
                placeholderRect,
                Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _focusTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
