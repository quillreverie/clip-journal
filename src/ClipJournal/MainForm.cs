using System.Diagnostics;

namespace ClipJournal;

public sealed class MainForm : Form
{
    private const int MaxUiItems = 500;
    private const int MaxLineChars = ClipStore.MaxStoredLineChars;

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
    private readonly System.Windows.Forms.Timer _searchDebounce = new();

    private bool _listening = true;
    private bool _exiting;
    private bool _handlingClipboard;
    private bool _historyReady = true;
    private bool _storageWritable;
    private bool _trayHintShown;
    private string? _lastLine;
    private int _totalCount;
    private int _nextIndex = 1;
    private readonly string? _startupWarning;

    public MainForm(AppSettings settings, ClipStore store, string? startupWarning = null)
    {
        _settings = settings;
        _store = store;
        _startupWarning = startupWarning;

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

        _storageWritable = EnsureClipsFileWritableSafe();
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
            _historyReady = false;
            _listening = false;
        }

        if (!_storageWritable)
        {
            _listening = false;
        }

        UpdateStatusUI();

        _listener.ClipboardUpdate += OnClipboardUpdate;
        try
        {
            _listener.Start();
        }
        catch
        {
            // Listening could not start (another app is locking AddClipboardFormatListener,
            // or handle creation failed). Without this the status badge would still pulse
            // "capturing" green while no clipboard events ever arrive — a silent failure
            // the user cannot distinguish from normal operation. Flip to the paused state
            // so the UI truthfully reports that nothing is being captured.
            _listening = false;
            ModernDialog.ShowError(this, Localization.ClipboardListenFailed);
        }

        FormClosing += OnFormClosing;
        Shown += (_, _) =>
        {
            UpdateStatusUI();
            _toastOverlay.UpdatePosition();
            _cardList.Focus();
            if (_startupWarning is not null && !IsDisposed)
            {
                ShowToast(_startupWarning, isError: true);
            }

            if (!_historyReady && !IsDisposed)
            {
                ShowToast(Localization.HistoryReadFailedHint, isError: true);
            }

            if (!_storageWritable && !IsDisposed)
            {
                ShowToast(Localization.WriteFailedHint, isError: true);
            }
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
            // Debounce the list re-scan: each keystroke would otherwise walk every
            // item (up to 500 x 256KB) with Contains(OrdinalIgnoreCase). Long clips
            // + a fast typist made the header visibly stutter. Restart the timer so
            // only the final (settled) query is applied.
            _searchDebounce.Stop();
            _searchDebounce.Start();
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

        _searchDebounce.Interval = 180;
        _searchDebounce.Tick += (_, _) =>
        {
            _searchDebounce.Stop();
            _cardList.SetFilter(_searchBar.SearchText);
            UpdateRecordSummary();
        };
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
        var snapshot = _store.ReadSnapshot(MaxUiItems);
        _totalCount = snapshot.TotalCount;
        _nextIndex = _totalCount + 1;

        var lines = snapshot.TailLines;
        var startIndex = Math.Max(1, _totalCount - lines.Count + 1);
        var cards = lines
            .Select((content, offset) => new ClipCardItem(startIndex + offset, string.Empty, content))
            .ToList();

        _cardList.SetItems(cards);
        _lastLine = lines.Count > 0 ? lines[^1] : null;
        _historyReady = true;
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
        catch (Exception)
        {
            // Avoid surfacing raw ex.Message here: it can leak local file paths
            // and system-language text. Use a stable localized message instead.
            if (!IsDisposed)
            {
                ShowToast(Localization.UnexpectedError, isError: true);
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

        if (!ClipboardReader.TryReadUnicodeText(
                MaxLineChars,
                out var text,
                out var internalCopy,
                out var readerTruncated))
        {
            ShowToast(Localization.ClipboardReadFailed, isError: true);
            return;
        }

        if (internalCopy)
        {
            return;
        }

        if (text is null || TextNormalizer.IsIgnorable(text))
        {
            return;
        }

        var (line, wasTruncated) = TextNormalizer.ToSingleLine(text, MaxLineChars);
        wasTruncated |= readerTruncated;
        if (TextNormalizer.IsIgnorable(line))
        {
            return;
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
        catch (Exception)
        {
            _storageWritable = false;
            // The append may have reached disk before a flush error was reported.
            // Reload before resuming so numbering and duplicate detection cannot
            // continue from an uncertain in-memory snapshot.
            _historyReady = false;
            _listening = false;
            UpdateStatusUI();
            if (!IsDisposed)
            {
                ShowToast(Localization.WriteFailedHint, isError: true);
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
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, e.Item.Content);
            data.SetData(ClipboardReader.InternalCopyFormat, "1");
            Clipboard.SetDataObject(data, copy: true);
            ShowToast(Localization.CopySuccess);
        }
        catch (Exception)
        {
            ShowToast(Localization.CopyFailedHint, isError: true);
        }
    }

    private void TogglePause()
    {
        if (_listening)
        {
            _listening = false;
            // The journal can be edited through the app's "Open file" action while
            // capture is paused. Resume must reconcile any external changes first.
            _historyReady = false;
            UpdateStatusUI();
            ShowToast(Localization.Paused);
            return;
        }

        try
        {
            _store.EnsureWritable();
            _storageWritable = true;
        }
        catch
        {
            _storageWritable = false;
            UpdateStatusUI();
            ShowToast(Localization.WriteFailedHint, isError: true);
            return;
        }

        if (!_historyReady)
        {
            try
            {
                LoadHistory();
            }
            catch
            {
                _historyReady = false;
                UpdateStatusUI();
                ShowToast(Localization.HistoryReadFailedHint, isError: true);
                return;
            }
        }

        try
        {
            // Start is idempotent after an ordinary pause, but retries native setup
            // after a transient startup failure. Only advertise "capturing" on success.
            _listener.Start();
        }
        catch
        {
            _listening = false;
            UpdateStatusUI();
            ModernDialog.ShowError(this, Localization.ClipboardListenFailed);
            return;
        }

        _listening = true;
        UpdateStatusUI();
        ShowToast(Localization.Resumed);
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
        catch (Exception)
        {
            ModernDialog.ShowError(this, Localization.OpenFileFailedHint);
        }
    }

    private static bool IsTxtPath(string path)
        => path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase);

    private void OpenClipsFolder()
    {
        try
        {
            EnsureClipsFileExists();
            // Build the /select argument without raw concatenation: a path that contains
            // a double-quote (a user can type one in SaveFileDialog) would otherwise
            // break explorer's quoting and point /select at the wrong target. Strip
            // quotes defensively and re-quote the path as a single argument.
            var safePath = _store.FilePath.Replace("\"", string.Empty);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select,\"" + safePath + "\"",
                UseShellExecute = true,
            });
        }
        catch (Exception)
        {
            ModernDialog.ShowError(this, Localization.OpenFileFailedHint);
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

            var previousPath = _store.FilePath;
            _store.SetFilePath(chosen);
            try
            {
                // Validate writability and load a bounded snapshot before committing the
                // selection to settings. This prevents a locked, malformed, or oversized
                // external file from becoming a persistent startup failure.
                _store.EnsureWritable();
                LoadHistory();
                _storageWritable = true;
            }
            catch (Exception)
            {
                try
                {
                    _store.SetFilePath(previousPath);
                    _store.EnsureWritable();
                    LoadHistory();
                    _storageWritable = true;
                }
                catch
                {
                    // The original target changed at the same time. Keep capture paused
                    // instead of treating an unknown history state as an empty journal.
                    _storageWritable = false;
                    _historyReady = false;
                    _listening = false;
                    UpdateStatusUI();
                }

                throw;
            }

            _settings.ClipsFilePath = _store.FilePath;
            _settingsPanel.FilePath = _store.FilePath;
            TrySaveSettings();
            ShowToast(Localization.FileSwitched);
        }
        catch (Exception)
        {
            ModernDialog.ShowError(this, Localization.ChangeFileFailedHint);
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
            _historyReady = true;
            _storageWritable = true;
            UpdateStatusUI();
            ShowToast(Localization.Cleared);
        }
        catch (Exception)
        {
            _storageWritable = false;
            // Clearing can fail after changing the file. Treat the cached history as
            // uncertain and rebuild it on the next successful resume.
            _historyReady = false;
            _listening = false;
            UpdateStatusUI();
            ModernDialog.ShowError(this, Localization.ClearFailedHint);
        }
    }

    private void EnsureClipsFileExists()
    {
        if (!File.Exists(_store.FilePath))
        {
            _store.EnsureWritable();
        }
    }

    // Constructor-safe write probe: if the target is temporarily locked/readonly we
    // must not throw before the message loop is running.
    private bool EnsureClipsFileWritableSafe()
    {
        try
        {
            _store.EnsureWritable();
            return true;
        }
        catch (Exception)
        {
            // Start paused; Resume retries after the user fixes permissions or changes
            // the target instead of repeatedly dropping clips while claiming to listen.
            return false;
        }
    }

    private void TrySaveSettings()
    {
        try
        {
            _settings.Save();
        }
        catch (Exception)
        {
            ModernDialog.ShowError(this, Localization.SaveSettingsFailedHint);
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
            _listener.ClipboardUpdate -= OnClipboardUpdate;
            _listener.Dispose();
            _notifyIcon.Dispose();
            _trayMenu.Dispose();
            _appIcon.Dispose();
            _searchDebounce.Dispose();
        }

        base.Dispose(disposing);
    }

}
