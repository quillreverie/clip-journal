using System.Drawing.Drawing2D;

namespace ClipJournal;

internal sealed class ModernDialog : Form
{
    private readonly DialogGlyph _glyph = new();
    private readonly Label _titleLabel = new();
    private readonly Label _messageLabel = new();
    private readonly Panel _footer = new();
    private readonly ModernButton _primaryButton = new();
    private readonly ModernButton? _secondaryButton;

    private ModernDialog(
        string title,
        string message,
        string primaryText,
        string? secondaryText,
        UiIcon icon,
        bool isDanger)
    {
        Text = title;
        ClientSize = new Size(560, 330);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Surface;
        Font = Theme.FontMain;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);

        _glyph.Icon = icon;
        _glyph.Accent = isDanger ? Theme.Danger : Theme.Primary;
        _glyph.SoftBackground = isDanger ? Theme.DangerSoft : Theme.PrimarySoft;

        _titleLabel.Text = title;
        _titleLabel.Font = Theme.FontTitle;
        _titleLabel.ForeColor = Theme.TextPrimary;
        _titleLabel.BackColor = Color.Transparent;
        _titleLabel.AutoSize = true;
        _titleLabel.UseMnemonic = false;

        _messageLabel.Text = message;
        _messageLabel.Font = Theme.FontMain;
        _messageLabel.ForeColor = Theme.TextSecondary;
        _messageLabel.BackColor = Color.Transparent;
        _messageLabel.AutoSize = false;
        _messageLabel.UseMnemonic = false;

        _footer.Dock = DockStyle.Bottom;
        _footer.Height = 72;
        _footer.BackColor = Theme.SurfaceSoft;
        _footer.Paint += (_, e) =>
        {
            using var divider = new Pen(Theme.Divider, Math.Max(1f, DeviceDpi / 96f));
            e.Graphics.DrawLine(divider, 0, 0, _footer.Width, 0);
        };
        _footer.Resize += (_, _) => LayoutFooter();

        _primaryButton.Text = primaryText;
        _primaryButton.Style = isDanger ? ModernButtonStyle.Danger : ModernButtonStyle.Primary;
        _primaryButton.Icon = isDanger ? UiIcon.Trash : UiIcon.ChevronRight;
        _primaryButton.AutoFitWidth = false;
        _primaryButton.DialogResult = DialogResult.OK;

        if (!string.IsNullOrWhiteSpace(secondaryText))
        {
            _secondaryButton = new ModernButton
            {
                Text = secondaryText,
                Style = ModernButtonStyle.Secondary,
                AutoFitWidth = false,
                DialogResult = DialogResult.Cancel,
            };
            _footer.Controls.Add(_secondaryButton);
            CancelButton = _secondaryButton;
        }
        else
        {
            // No secondary button (error/info dialogs): the only way out is the primary
            // "Got it". Bind Esc to it too so the dialog is keyboard-dismissable without
            // forcing a mouse click — AcceptButton only handles Enter.
            CancelButton = _primaryButton;
        }

        AcceptButton = _primaryButton;
        _footer.Controls.Add(_primaryButton);
        Controls.Add(_glyph);
        Controls.Add(_titleLabel);
        Controls.Add(_messageLabel);
        Controls.Add(_footer);

        Resize += (_, _) => LayoutContent();
        Shown += (_, _) =>
        {
            _footer.Height = Theme.Scale(this, 72);
            LayoutContent();
            LayoutFooter();
            if (isDanger && _secondaryButton is not null)
            {
                _secondaryButton.Focus();
            }
        };

        LayoutContent();
        LayoutFooter();
    }

    public static bool ConfirmPrivacy(string path)
    {
        using var dialog = new ModernDialog(
            Localization.PrivacyTitle,
            Localization.PrivacyMessage(path),
            Localization.ContinueAction,
            Localization.Exit,
            UiIcon.Clipboard,
            isDanger: false);
        dialog.StartPosition = FormStartPosition.CenterScreen;
        return dialog.ShowDialog() == DialogResult.OK;
    }

    public static bool ConfirmClear(IWin32Window owner, string path)
    {
        using var dialog = new ModernDialog(
            Localization.ClearDialogTitle,
            Localization.ClearConfirm(path),
            Localization.ClearRecords,
            Localization.CancelAction,
            UiIcon.Trash,
            isDanger: true);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    public static void ShowError(IWin32Window? owner, string message)
    {
        using var dialog = new ModernDialog(
            Localization.ErrorTitle,
            message,
            Localization.GotItAction,
            null,
            UiIcon.Close,
            isDanger: true);
        if (owner is null)
        {
            dialog.StartPosition = FormStartPosition.CenterScreen;
            dialog.ShowDialog();
        }
        else
        {
            dialog.ShowDialog(owner);
        }
    }

    public static void ShowInfo(string title, string message)
    {
        using var dialog = new ModernDialog(
            title,
            message,
            Localization.GotItAction,
            null,
            UiIcon.Clipboard,
            isDanger: false);
        dialog.StartPosition = FormStartPosition.CenterScreen;
        dialog.ShowDialog();
    }

    private void LayoutContent()
    {
        var left = Theme.Scale(this, 28);
        _glyph.SetBounds(left, Theme.Scale(this, 27), Theme.Scale(this, 48), Theme.Scale(this, 48));
        var textLeft = _glyph.Right + Theme.Scale(this, 18);
        _titleLabel.Location = new Point(textLeft, Theme.Scale(this, 29));
        _messageLabel.SetBounds(
            textLeft,
            Theme.Scale(this, 72),
            Math.Max(1, ClientSize.Width - textLeft - Theme.Scale(this, 30)),
            Math.Max(1, _footer.Top - Theme.Scale(this, 88)));
    }

    private void LayoutFooter()
    {
        var right = _footer.ClientSize.Width - Theme.Scale(this, 20);
        var top = Theme.Scale(this, 16);
        var height = Theme.Scale(this, 40);
        var primaryWidth = Theme.Scale(this, 132);
        _primaryButton.SetBounds(right - primaryWidth, top, primaryWidth, height);

        if (_secondaryButton is not null)
        {
            var secondaryWidth = Theme.Scale(this, 104);
            _secondaryButton.SetBounds(
                _primaryButton.Left - Theme.Scale(this, 10) - secondaryWidth,
                top,
                secondaryWidth,
                height);
        }
    }

    private sealed class DialogGlyph : Control
    {
        public UiIcon Icon { get; set; }
        public Color Accent { get; set; } = Theme.Primary;
        public Color SoftBackground { get; set; } = Theme.PrimarySoft;

        public DialogGlyph()
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
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var background = new SolidBrush(SoftBackground);
            e.Graphics.FillEllipse(background, ClientRectangle);
            var iconRect = Rectangle.Inflate(ClientRectangle, -Theme.Scale(this, 13), -Theme.Scale(this, 13));
            UiIconPainter.Draw(e.Graphics, Icon, iconRect, Accent, Math.Max(1.5f, 1.7f * DeviceDpi / 96f));
        }
    }
}
