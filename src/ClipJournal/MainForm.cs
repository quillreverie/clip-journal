using System.Diagnostics;

namespace ClipJournal;

public sealed class MainForm : Form
{
    private const int MaxUiItems = 500;
    private const int MaxLineChars = 256 * 1024;

    private readonly AppSettings _settings;
    private readonly ClipStore _store;
    private readonly ClipboardListener _listener = new();
    private readonly NotifyIcon _notifyIcon = new();
    private readonly ModernTrayMenu _trayMenu = new();
    private readonly Icon _appIcon = AppIconFactory.Create();
    private ModernTrayMenuItem? _trayPauseItem;

    private readonly Panel _sidebarPanel = new();
    private readonly BrandLogo _brandLogo = new();
    private readonly Label _captureLabel = new();
    private readonly ModernStatusBadge _statusBadge = new();
    private readonly ModernButton _pauseButton = new();
    private readonly SettingsDrawer _settingsPanel = new();
    private readonly Label _privacyHint = new();

    private readonly Panel _contentPanel = new();
    private readonly Panel _contentHeader = new();
    private readonly Label _recordsTitle = new();
    private readonly Label _recordsSubtitle = new();
    private readonly Label _recordCount = new();
    private readonly Label _previewScope = new();
    private readonly ModernSearchBar _searchBar = new();
    private readonly ModernButton _clearButton = new();
    private readonly ModernCardList _cardList = new();
    private readonly ModernToastOverlay _toastOverlay = new();

    private bool _listening = true;
    private bool _exiting;
    private bool _handlingClipboard;
    private bool _trayHintShown;
    private string? _lastLine;
    private int _totalCount;
    private int _nextIndex = 1;
    private string? _suppressedClipboardText;
    private DateTime _suppressedClipboardUntilUtc;

    public MainForm(AppSettings settings)
    {
        _settings = settings;
        _store = new ClipStore(_settings.ClipsFilePath);

        Text = Localization.AppName;
        Icon = _appIcon;
        ClientSize = new Size(1100, 700);
        MinimumSize = new Size(900, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        Font = Theme.FontMain;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        DoubleBuffered = true;
        KeyPreview = true;

        EnsureClipsFileExistsSafe();
        BuildUi();
        BuildTray();
        try
        {
            LoadHistory();
        }
        catch (Exception)
        {
            // Defensive: a history read failure (locked file, permissions) during
            // construction must not crash the app before the message loop starts.
            // Degrade to an empty journal; live capture will keep working.
            _totalCount = 0;
            _nextIndex = 1;
            _lastLine = null;
            _cardList.SetItems(Array.Empty<ClipCardItem>());
        }
        UpdateStatusUI();

        _listener.ClipboardUpdate += OnClipboardUpdate;
        try
        {
            _listener.Start();
        }
        catch
        {
            ModernDialog.ShowError(this, Localization.ClipboardListenFailed);
        }

        FormClosing += OnFormClosing;
        Shown += (_, _) =>
        {
            UpdateStatusUI();
            _toastOverlay.UpdatePosition();
            _cardList.Focus();
        };
        KeyDown += OnMainKeyDown;
    }

    private void BuildUi()
    {
        BuildSidebar();
        BuildContent();

        Controls.Add(_contentPanel);
        Controls.Add(_sidebarPanel);

        _sidebarPanel.Dock = DockStyle.Left;
        _contentPanel.Dock = DockStyle.Fill;

        Resize += (_, _) => LayoutShell();
        LayoutShell();
    }

    private void BuildSidebar()
    {
        _sidebarPanel.BackColor = Theme.SidebarBackground;
        _sidebarPanel.Paint += (_, e) =>
        {
            using var divider = new Pen(Theme.Divider, Math.Max(1f, DeviceDpi / 96f));
            e.Graphics.DrawLine(divider, _sidebarPanel.Width - 1, 0, _sidebarPanel.Width - 1, _sidebarPanel.Height);
            e.Graphics.DrawLine(
                divider,
                Theme.Scale(this, 20),
                Theme.Scale(this, 92),
                _sidebarPanel.Width - Theme.Scale(this, 20),
                Theme.Scale(this, 92));
        };

        ConfigureLabel(_captureLabel, Localization.CaptureSectionLabel, Theme.FontSmallMedium, Theme.TextSecondary);
        _captureLabel.Text = _captureLabel.Text.ToUpperInvariant();

        _statusBadge.Click += (_, _) => TogglePause();

        _pauseButton.Text = Localization.PauseAction;
        _pauseButton.Icon = UiIcon.Pause;
        _pauseButton.Style = ModernButtonStyle.Primary;
        _pauseButton.AutoFitWidth = false;
        _pauseButton.AccessibleDescription = Localization.CaptureSectionLabel;
        _pauseButton.Click += (_, _) => TogglePause();

        _settingsPanel.FilePath = _store.FilePath;
        _settingsPanel.BlankLineEvery = _settings.BlankLineEvery;
        _settingsPanel.OpenFileClicked += (_, _) => OpenClipsFile();
        _settingsPanel.OpenFolderClicked += (_, _) => OpenClipsFolder();
        _settingsPanel.ChangeFileClicked += (_, _) => ChangeClipsFile();
        _settingsPanel.BlankEveryChanged += (_, value) =>
        {
            _settings.BlankLineEvery = value;
            TrySaveSettings();
            ShowToast(value == 0
                ? Localization.BlankLineDisabled
                : Localization.BlankLineEnabled(value));
        };

        ConfigureLabel(_privacyHint, Localization.PrivacyHint, Theme.FontSmall, Theme.TextSecondary);
        _privacyHint.AutoSize = false;

        _sidebarPanel.Controls.Add(_brandLogo);
        _sidebarPanel.Controls.Add(_captureLabel);
        _sidebarPanel.Controls.Add(_statusBadge);
        _sidebarPanel.Controls.Add(_pauseButton);
        _sidebarPanel.Controls.Add(_settingsPanel);
        _sidebarPanel.Controls.Add(_privacyHint);
    }

    private void BuildContent()
    {
        _contentPanel.BackColor = Theme.Background;

        _contentHeader.Dock = DockStyle.Top;
        _contentHeader.BackColor = Theme.Background;
        _contentHeader.Paint += (_, e) =>
        {
            using var divider = new Pen(Theme.Divider, Math.Max(1f, DeviceDpi / 96f));
            e.Graphics.DrawLine(
                divider,
                Theme.Scale(this, 24),
                _contentHeader.Height - 1,
                _contentHeader.Width - Theme.Scale(this, 24),
                _contentHeader.Height - 1);
        };
        _contentHeader.Resize += (_, _) => LayoutContentHeader();

        ConfigureLabel(_recordsTitle, Localization.RecordsTitle, Theme.FontDisplay, Theme.TextPrimary);
        ConfigureLabel(_recordsSubtitle, Localization.RecordsSubtitle, Theme.FontMain, Theme.TextSecondary);
        ConfigureLabel(_recordCount, string.Empty, Theme.FontMainMedium, Theme.TextPrimary);
        ConfigureLabel(_previewScope, Localization.PreviewScope, Theme.FontSmall, Theme.TextSecondary);

        _searchBar.SearchTextChanged += (_, _) =>
        {
            _cardList.SetFilter(_searchBar.SearchText);
            UpdateRecordSummary();
        };

        _clearButton.Text = Localization.ClearRecords;
        _clearButton.Icon = UiIcon.Trash;
        _clearButton.Style = ModernButtonStyle.Danger;
        _clearButton.AutoFitWidth = false;
        _clearButton.Click += (_, _) => ClearAll();

        _cardList.Dock = DockStyle.Fill;
        _cardList.ItemCopyClicked += OnItemCopyClicked;
        _cardList.FilterResultChanged += (_, _) => UpdateRecordSummary();

        _contentHeader.Controls.Add(_recordsTitle);
        _contentHeader.Controls.Add(_recordsSubtitle);
        _contentHeader.Controls.Add(_recordCount);
        _contentHeader.Controls.Add(_previewScope);
        _contentHeader.Controls.Add(_searchBar);
        _contentHeader.Controls.Add(_clearButton);

        _contentPanel.Controls.Add(_cardList);
        _contentPanel.Controls.Add(_contentHeader);
        _contentPanel.Controls.Add(_toastOverlay);
        _toastOverlay.BringToFront();
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

    private void LayoutShell()
    {
        var sidebarWidth = Theme.Scale(this, 276);
        _sidebarPanel.Width = sidebarWidth;

        var padding = Theme.Scale(this, 20);
        var innerWidth = Math.Max(1, sidebarWidth - padding * 2);

        _brandLogo.SetBounds(padding, Theme.Scale(this, 20), innerWidth, Theme.Scale(this, 54));
        _captureLabel.Location = new Point(padding, Theme.Scale(this, 107));
        _statusBadge.SetBounds(padding, Theme.Scale(this, 132), innerWidth, Theme.Scale(this, 76));
        _pauseButton.SetBounds(padding, Theme.Scale(this, 220), innerWidth, Theme.Scale(this, 42));
        _settingsPanel.SetBounds(padding, Theme.Scale(this, 280), innerWidth, Theme.Scale(this, 344));

        var privacyTop = Math.Max(_settingsPanel.Bottom + Theme.Scale(this, 14), _sidebarPanel.ClientSize.Height - Theme.Scale(this, 64));
        _privacyHint.SetBounds(padding + Theme.Scale(this, 2), privacyTop, innerWidth - Theme.Scale(this, 4), Theme.Scale(this, 52));

        _contentHeader.Height = Theme.Scale(this, 146);
        LayoutContentHeader();
        _toastOverlay.UpdatePosition();
        _sidebarPanel.Invalidate();
    }

    private void LayoutContentHeader()
    {
        if (_contentHeader.Width <= 0)
        {
            return;
        }

        var left = Theme.Scale(this, 24);
        var right = _contentHeader.Width - Theme.Scale(this, 24);
        _recordsTitle.Location = new Point(left, Theme.Scale(this, 20));
        _recordsSubtitle.Location = new Point(left, Theme.Scale(this, 57));

        _recordCount.Location = new Point(Math.Max(left, right - _recordCount.Width), Theme.Scale(this, 25));
        _previewScope.Location = new Point(Math.Max(left, right - _previewScope.Width), Theme.Scale(this, 54));

        var searchTop = Theme.Scale(this, 91);
        var clearWidth = Theme.Scale(this, 122);
        var gap = Theme.Scale(this, 12);
        _clearButton.SetBounds(right - clearWidth, searchTop, clearWidth, Theme.Scale(this, 40));
        _searchBar.SetBounds(left, searchTop, Math.Max(Theme.Scale(this, 220), _clearButton.Left - gap - left), Theme.Scale(this, 40));
        _contentHeader.Invalidate();
    }

    private void BuildTray()
    {
        _trayMenu.AddAction(
            Localization.ShowWindow,
            UiIcon.Clipboard,
            (_, _) => ShowMainWindow(),
            emphasized: true);
        _trayMenu.AddDivider();

        _trayPauseItem = _trayMenu.AddAction(
            Localization.PauseAction,
            UiIcon.Pause,
            (_, _) => TogglePause());
        _trayMenu.AddAction(Localization.OpenFileShort, UiIcon.File, (_, _) => OpenClipsFile());
        _trayMenu.AddAction(Localization.OpenFolderShort, UiIcon.Folder, (_, _) => OpenClipsFolder());
        _trayMenu.AddAction(Localization.ChangeFileShort, UiIcon.Swap, (_, _) => ChangeClipsFile());
        _trayMenu.AddDivider();

        _trayMenu.AddAction(
            Localization.ClearRecords,
            UiIcon.Trash,
            (_, _) => ClearAll(),
            danger: true);
        _trayMenu.AddAction(Localization.Exit, UiIcon.Close, (_, _) => ExitApp());

        _notifyIcon.Text = Localization.AppName;
        _notifyIcon.Icon = _appIcon;
        _notifyIcon.ContextMenuStrip = _trayMenu;
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
        _notifyIcon.Visible = true;
    }

    private void LoadHistory()
    {
        _totalCount = _store.CountNonEmptyLines();
        _nextIndex = _totalCount + 1;

        var lines = _store.ReadTailLines(MaxUiItems);
        var startIndex = Math.Max(1, _totalCount - lines.Count + 1);
        var cards = lines
            .Select((content, offset) => new ClipCardItem(startIndex + offset, string.Empty, content))
            .ToList();

        _cardList.SetItems(cards);
        _lastLine = lines.Count > 0 ? lines[^1] : null;
        UpdateStatusUI();
    }

    private void OnClipboardUpdate()
    {
        if (!_listening || _exiting || _handlingClipboard || IsDisposed)
        {
            return;
        }

        _handlingClipboard = true;
        try
        {
            HandleClipboardUpdate();
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                ShowToast(ex.Message, isError: true);
            }
        }
        finally
        {
            _handlingClipboard = false;
        }
    }

    private void HandleClipboardUpdate()
    {
        if (_exiting || IsDisposed)
        {
            return;
        }

        if (!ClipboardReader.TryReadUnicodeText(out var text, out _))
        {
            ShowToast(Localization.ClipboardReadFailed, isError: true);
            return;
        }

        if (text is null || TextNormalizer.IsIgnorable(text))
        {
            return;
        }

        var (line, wasTruncated) = TextNormalizer.ToSingleLine(text, MaxLineChars);
        if (TextNormalizer.IsIgnorable(line))
        {
            return;
        }

        if (_suppressedClipboardText is not null)
        {
            var shouldSuppress = DateTime.UtcNow <= _suppressedClipboardUntilUtc &&
                                 string.Equals(line, _suppressedClipboardText, StringComparison.Ordinal);
            _suppressedClipboardText = null;
            if (shouldSuppress)
            {
                return;
            }
        }

        if (string.Equals(line, _lastLine, StringComparison.Ordinal))
        {
            ShowToast(Localization.SkippedDuplicate);
            return;
        }

        var index = _nextIndex;
        var every = _settings.BlankLineEvery;
        var insertBlank = every > 0 && index % every == 0;

        if (_exiting || IsDisposed)
        {
            return;
        }

        try
        {
            _store.AppendLine(line, insertBlank);
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                ShowToast(Localization.WriteFailed + ex.Message, isError: true);
            }

            return;
        }

        _lastLine = line;
        _totalCount++;
        _nextIndex++;

        if (IsDisposed)
        {
            return;
        }

        _cardList.AddItem(index, DateTime.Now.ToString("HH:mm"), line);
        UpdateStatusUI();

        if (wasTruncated)
        {
            ShowToast(Localization.Truncated, isError: true);
        }
        else if (insertBlank)
        {
            ShowToast(Localization.BlankInserted(index));
        }
    }

    private void OnItemCopyClicked(object? sender, ItemActionEventArgs e)
    {
        try
        {
            _suppressedClipboardText = e.Item.Content;
            _suppressedClipboardUntilUtc = DateTime.UtcNow.AddSeconds(2);
            Clipboard.SetText(e.Item.Content);
            ShowToast(Localization.CopySuccess);
        }
        catch (Exception ex)
        {
            _suppressedClipboardText = null;
            ShowToast(ex.Message, isError: true);
        }
    }

    private void TogglePause()
    {
        _listening = !_listening;
        UpdateStatusUI();
        ShowToast(_listening ? Localization.Resumed : Localization.Paused);
    }

    private void UpdateStatusUI()
    {
        _statusBadge.IsListening = _listening;
        _statusBadge.Count = _totalCount;
        _pauseButton.Text = _listening ? Localization.PauseAction : Localization.ResumeAction;
        _pauseButton.Icon = _listening ? UiIcon.Pause : UiIcon.Play;
        if (_trayPauseItem is not null)
        {
            _trayPauseItem.Text = _listening ? Localization.PauseAction : Localization.ResumeAction;
            _trayPauseItem.Icon = _listening ? UiIcon.Pause : UiIcon.Play;
            _trayPauseItem.AccessibleName = _trayPauseItem.Text;
        }
        _notifyIcon.Text = _listening ? Localization.TrayListening : Localization.TrayPaused;
        UpdateRecordSummary();
    }

    private void UpdateRecordSummary()
    {
        _recordCount.Text = string.IsNullOrWhiteSpace(_searchBar.SearchText)
            ? Localization.CountSummary(_totalCount)
            : Localization.FilteredPreviewSummary(_cardList.FilteredCount);
        _previewScope.Text = Localization.PreviewScope;
        LayoutContentHeader();
    }

    private void OnMainKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.F)
        {
            _searchBar.FocusInput();
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.Shift && e.KeyCode == Keys.P)
        {
            TogglePause();
            e.SuppressKeyPress = true;
        }
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void OpenClipsFile()
    {
        try
        {
            EnsureClipsFileExists();
            var path = _store.FilePath;
            if (!IsTxtPath(path))
            {
                ShowToast(Localization.OpenOnlyTxt, isError: true);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ModernDialog.ShowError(this, ex.Message);
        }
    }

    private static bool IsTxtPath(string path)
        => path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    private void OpenClipsFolder()
    {
        try
        {
            EnsureClipsFileExists();
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select,\"" + _store.FilePath + "\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ModernDialog.ShowError(this, ex.Message);
        }
    }

    private void ChangeClipsFile()
    {
        using var dialog = new SaveFileDialog
        {
            Title = Localization.ChooseFileTitle,
            Filter = Localization.FilterTxt,
            DefaultExt = "txt",
            AddExtension = true,
            FileName = Path.GetFileName(_store.FilePath),
            InitialDirectory = Path.GetDirectoryName(_store.FilePath),
            OverwritePrompt = false,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var chosen = dialog.FileName;
            if (!IsTxtPath(chosen))
            {
                chosen += ".txt";
            }

            _store.SetFilePath(chosen);
            if (!File.Exists(_store.FilePath))
            {
                _store.Clear();
            }

            _settings.ClipsFilePath = _store.FilePath;
            _settingsPanel.FilePath = _store.FilePath;
            TrySaveSettings();
            LoadHistory();
            ShowToast(Localization.FileSwitched);
        }
        catch (Exception ex)
        {
            ModernDialog.ShowError(this, ex.Message);
        }
    }

    private void ClearAll()
    {
        if (!ModernDialog.ConfirmClear(this, _store.FilePath))
        {
            return;
        }

        try
        {
            _store.Clear();
            _cardList.Clear();
            _searchBar.SearchText = string.Empty;
            _lastLine = null;
            _totalCount = 0;
            _nextIndex = 1;
            UpdateStatusUI();
            ShowToast(Localization.Cleared);
        }
        catch (Exception ex)
        {
            ModernDialog.ShowError(this, ex.Message);
        }
    }

    private void EnsureClipsFileExists()
    {
        if (!File.Exists(_store.FilePath))
        {
            _store.Clear();
        }
    }

    // Constructor-safe variant: a missing clips file is created by Clear(), which
    // opens with FileShare.None; if the path is temporarily locked/readonly we must
    // not throw before the message loop is running.
    private void EnsureClipsFileExistsSafe()
    {
        try
        {
            EnsureClipsFileExists();
        }
        catch (Exception)
        {
            // Live capture will retry; ignore during construction.
        }
    }

    private void TrySaveSettings()
    {
        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            ModernDialog.ShowError(this, ex.Message);
        }
    }

    private void ShowToast(string message, bool isError = false)
        => _toastOverlay.ShowToast(message, isError);

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_exiting)
        {
            return;
        }

        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                _notifyIcon.BalloonTipTitle = Localization.TrayHintTitle;
                _notifyIcon.BalloonTipText = Localization.TrayHintBody;
                _notifyIcon.ShowBalloonTip(1800);
            }
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == SingleInstance.ShowWindowMessage)
        {
            ShowMainWindow();
            return;
        }

        base.WndProc(ref message);
    }

    private void ExitApp()
    {
        _exiting = true;
        try
        {
            _listener.Stop();
        }
        catch
        {
            // Ignore shutdown races from the native clipboard window.
        }

        _notifyIcon.Visible = false;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _listener.Dispose();
            _notifyIcon.Dispose();
            _trayMenu.Dispose();
            _appIcon.Dispose();
        }

        base.Dispose(disposing);
    }

}
