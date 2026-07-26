using System.Diagnostics;

namespace ClipJournal;

public sealed class MainForm : Form
{
    private const int MaxUiItems = 500;
    private const int MaxLineChars = 256 * 1024;
    private const int PreviewChars = 80;

    private readonly AppSettings _settings;
    private readonly ClipStore _store;
    private readonly ClipboardListener _listener = new();
    private readonly ListBox _listBox = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusListen = new();
    private readonly ToolStripStatusLabel _statusCount = new();
    private readonly ToolStripStatusLabel _statusPath = new();
    private readonly ToolStripStatusLabel _statusMessage = new();
    private readonly NotifyIcon _notifyIcon = new();
    private readonly System.Windows.Forms.Timer _messageTimer = new();
    private readonly Button _btnPause = new();
    private readonly Button _btnOpenFile = new();
    private readonly Button _btnOpenFolder = new();
    private readonly Button _btnChangeFile = new();
    private readonly Button _btnClear = new();
    private readonly Button _btnExit = new();
    private readonly Label _lblBlankEvery = new();
    private readonly NumericUpDown _numBlankEvery = new();
    private readonly Label _lblBlankEverySuffix = new();

    private bool _listening = true;
    private bool _exiting;
    private bool _handlingClipboard;
    private string? _lastLine;
    private int _totalCount;
    private int _nextIndex = 1;

    public MainForm(AppSettings settings)
    {
        _settings = settings;

        Text = Localization.AppName;
        Width = 860;
        Height = 560;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(640, 400);
        Font = new Font("Segoe UI", 9F);

        _store = new ClipStore(_settings.ClipsFilePath);
        EnsureClipsFileExists();

        BuildUi();
        BuildTray();
        LoadHistory();
        UpdateStatus();

        _listener.ClipboardUpdate += OnClipboardUpdate;
        _listener.Start();

        FormClosing += OnFormClosing;
        Shown += (_, _) => UpdateStatus();
    }

    private void BuildUi()
    {
        var panelButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = true,
        };

        ConfigureButton(_btnPause, Localization.Pause);
        ConfigureButton(_btnOpenFile, Localization.OpenTxt);
        ConfigureButton(_btnOpenFolder, Localization.OpenFolder);
        ConfigureButton(_btnChangeFile, Localization.ChangeFile);
        ConfigureButton(_btnClear, Localization.Clear);
        ConfigureButton(_btnExit, Localization.Exit);

        _lblBlankEvery.Text = Localization.BlankEveryPrefix;
        _lblBlankEvery.AutoSize = true;
        _lblBlankEvery.Margin = new Padding(12, 8, 0, 4);
        _lblBlankEvery.TextAlign = ContentAlignment.MiddleLeft;

        _numBlankEvery.Minimum = 0;
        _numBlankEvery.Maximum = 999;
        _numBlankEvery.Value = Math.Clamp(_settings.BlankLineEvery, 0, 999);
        _numBlankEvery.Width = 56;
        _numBlankEvery.Margin = new Padding(4, 6, 0, 4);
        _numBlankEvery.ValueChanged += (_, _) =>
        {
            _settings.BlankLineEvery = (int)_numBlankEvery.Value;
            TrySaveSettings();
            ShowTempMessage(_settings.BlankLineEvery == 0
                ? Localization.BlankLineDisabled
                : Localization.BlankLineEnabled(_settings.BlankLineEvery));
        };

        _lblBlankEverySuffix.Text = Localization.BlankEverySuffix;
        _lblBlankEverySuffix.AutoSize = true;
        _lblBlankEverySuffix.Margin = new Padding(4, 8, 8, 4);
        _lblBlankEverySuffix.TextAlign = ContentAlignment.MiddleLeft;

        _btnPause.Click += (_, _) => TogglePause();
        _btnOpenFile.Click += (_, _) => OpenClipsFile();
        _btnOpenFolder.Click += (_, _) => OpenClipsFolder();
        _btnChangeFile.Click += (_, _) => ChangeClipsFile();
        _btnClear.Click += (_, _) => ClearAll();
        _btnExit.Click += (_, _) => ExitApp();

        panelButtons.Controls.AddRange(new Control[]
        {
            _btnPause, _btnOpenFile, _btnOpenFolder, _btnChangeFile, _btnClear, _btnExit,
            _lblBlankEvery, _numBlankEvery, _lblBlankEverySuffix,
        });

        _listBox.Dock = DockStyle.Fill;
        _listBox.IntegralHeight = false;
        _listBox.HorizontalScrollbar = true;
        _listBox.Font = new Font("Consolas", 9.5F);

        _statusListen.Spring = false;
        _statusCount.Spring = false;
        _statusPath.Spring = true;
        _statusPath.TextAlign = ContentAlignment.MiddleLeft;
        _statusMessage.Spring = false;

        _statusStrip.Items.AddRange(new ToolStripItem[]
        {
            _statusListen,
            new ToolStripSeparator(),
            _statusCount,
            new ToolStripSeparator(),
            _statusPath,
            new ToolStripSeparator(),
            _statusMessage,
        });

        _messageTimer.Interval = 2000;
        _messageTimer.Tick += (_, _) =>
        {
            _messageTimer.Stop();
            _statusMessage.Text = string.Empty;
        };

        Controls.Add(_listBox);
        Controls.Add(panelButtons);
        Controls.Add(_statusStrip);
    }

    private static void ConfigureButton(Button button, string text)
    {
        button.Text = text;
        button.AutoSize = true;
        button.Margin = new Padding(0, 0, 8, 4);
        button.Padding = new Padding(10, 4, 10, 4);
    }

    private void BuildTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Localization.ShowWindow, null, (_, _) => ShowMainWindow());
        menu.Items.Add(Localization.PauseResume, null, (_, _) => TogglePause());
        menu.Items.Add(Localization.OpenTxt, null, (_, _) => OpenClipsFile());
        menu.Items.Add(Localization.OpenFolder, null, (_, _) => OpenClipsFolder());
        menu.Items.Add(Localization.ChangeFile, null, (_, _) => ChangeClipsFile());
        menu.Items.Add(Localization.Clear, null, (_, _) => ClearAll());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Localization.Exit, null, (_, _) => ExitApp());

        _notifyIcon.Text = Localization.AppName;
        _notifyIcon.Icon = SystemIcons.Application;
        _notifyIcon.Visible = true;
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void LoadHistory()
    {
        _totalCount = _store.CountNonEmptyLines();
        _nextIndex = _totalCount + 1;

        var lines = _store.ReadTailLines(MaxUiItems);
        var startIndex = Math.Max(1, _totalCount - lines.Count + 1);

        _listBox.BeginUpdate();
        try
        {
            _listBox.Items.Clear();
            for (var i = 0; i < lines.Count; i++)
            {
                // txt stores plain content only; do not strip leading "N. " which may be real content.
                var content = lines[i];
                var index = startIndex + i;
                _listBox.Items.Add(FormatItem(index, Localization.HistoryLabel, content));
            }

            if (_listBox.Items.Count > 0)
            {
                _listBox.TopIndex = _listBox.Items.Count - 1;
                _lastLine = lines[^1];
            }
            else
            {
                _lastLine = null;
            }
        }
        finally
        {
            _listBox.EndUpdate();
        }
    }

    private void OnClipboardUpdate()
    {
        // Never re-enter from modal loops or nested clipboard messages.
        if (!_listening || _exiting || _handlingClipboard)
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
            // Keep the listener alive; avoid modal dialogs inside WndProc.
            ShowTempMessage(ex.Message);
        }
        finally
        {
            _handlingClipboard = false;
        }
    }

    private void HandleClipboardUpdate()
    {
        if (!ClipboardReader.TryReadUnicodeText(out var text, out _))
        {
            ShowTempMessage(Localization.ClipboardReadFailed);
            return;
        }

        // No Unicode text (e.g. image-only clipboard): ignore silently.
        if (text is null || TextNormalizer.IsIgnorable(text))
        {
            return;
        }

        // Flatten + cap length in one pass so huge clipboard payloads do not allocate 2×.
        var (line, wasTruncated) = TextNormalizer.ToSingleLine(text, MaxLineChars);
        if (TextNormalizer.IsIgnorable(line))
        {
            return;
        }

        if (string.Equals(line, _lastLine, StringComparison.Ordinal))
        {
            ShowTempMessage(Localization.SkippedDuplicate);
            return;
        }

        var index = _nextIndex;
        var every = _settings.BlankLineEvery;
        var insertBlank = every > 0 && index % every == 0;

        try
        {
            // txt product: content only (no leading index). UI still shows sequence numbers.
            // Content + optional blank separator are written in one open/flush.
            _store.AppendLine(line, insertBlank);
        }
        catch (Exception ex)
        {
            // Status bar only — MessageBox inside WM_CLIPBOARDUPDATE can re-enter this path.
            ShowTempMessage(Localization.WriteFailed + ex.Message);
            return;
        }

        _lastLine = line;
        _totalCount++;
        _nextIndex++;

        var stamp = DateTime.Now.ToString("HH:mm:ss");
        _listBox.Items.Add(FormatItem(index, stamp, line));
        while (_listBox.Items.Count > MaxUiItems)
        {
            _listBox.Items.RemoveAt(0);
        }

        _listBox.TopIndex = _listBox.Items.Count - 1;
        UpdateStatus();

        if (wasTruncated)
        {
            ShowTempMessage(Localization.Truncated);
        }
        else if (insertBlank)
        {
            ShowTempMessage(Localization.BlankInserted(index));
        }
    }

    private static string FormatItem(int index, string timeLabel, string line)
    {
        var preview = line.Length <= PreviewChars ? line : line[..PreviewChars] + "…";
        return $"{index}.  {timeLabel}  {preview}";
    }

    private void TogglePause()
    {
        _listening = !_listening;
        _btnPause.Text = _listening ? Localization.Pause : Localization.Resume;
        UpdateStatus();
        ShowTempMessage(_listening ? Localization.Resumed : Localization.Paused);
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
                ShowTempMessage(Localization.OpenOnlyTxt);
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
            MessageBox.Show(this, ex.Message, Localization.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            MessageBox.Show(this, ex.Message, Localization.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            TrySaveSettings();
            LoadHistory();
            UpdateStatus();
            ShowTempMessage(Localization.FileSwitched);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Localization.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearAll()
    {
        var result = MessageBox.Show(
            this,
            Localization.ClearConfirm(_store.FilePath),
            Localization.AppName,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
        {
            return;
        }

        try
        {
            _store.Clear();
            _listBox.Items.Clear();
            _lastLine = null;
            _totalCount = 0;
            _nextIndex = 1;
            UpdateStatus();
            ShowTempMessage(Localization.Cleared);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Localization.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EnsureClipsFileExists()
    {
        if (!File.Exists(_store.FilePath))
        {
            _store.Clear();
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
            MessageBox.Show(this, ex.Message, Localization.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateStatus()
    {
        _statusListen.Text = _listening ? Localization.Listening : Localization.StatusPaused;
        _statusCount.Text = Localization.TotalCount(_totalCount);
        _statusPath.Text = _store.FilePath;
        _notifyIcon.Text = _listening ? Localization.TrayListening : Localization.TrayPaused;
    }

    private void ShowTempMessage(string message)
    {
        _statusMessage.Text = message;
        _messageTimer.Stop();
        _messageTimer.Start();
    }

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
            ShowTempMessage(Localization.HiddenToTray);
        }
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
            // ignore
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
            _messageTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
