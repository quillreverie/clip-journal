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

    private bool _listening = true;
    private bool _exiting;
    private string? _lastLine;
    private int _totalCount;
    private int _nextIndex = 1;

    public MainForm()
    {
        _settings = AppSettings.Load();

        Text = "ClipJournal";
        Width = 860;
        Height = 560;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(640, 400);
        Font = new Font("Segoe UI", 9F);

        EnsurePrivacyAccepted();

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

    private void EnsurePrivacyAccepted()
    {
        if (_settings.PrivacyAccepted)
        {
            return;
        }

        var result = MessageBox.Show(
            this,
            "ClipJournal 会监听剪贴板中的文字，并把每次复制压成一行后写入本地 txt 文件。\n\n" +
            "文件默认位置：\n" + _settings.ClipsFilePath + "\n\n" +
            "请勿在记录密码等敏感信息时使用；可随时暂停监听或清空文件。\n\n是否继续？",
            "ClipJournal 隐私说明",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);

        if (result != DialogResult.OK)
        {
            Environment.Exit(0);
            return;
        }

        _settings.PrivacyAccepted = true;
        _settings.Save();
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

        ConfigureButton(_btnPause, "暂停");
        ConfigureButton(_btnOpenFile, "打开 txt");
        ConfigureButton(_btnOpenFolder, "打开文件夹");
        ConfigureButton(_btnChangeFile, "更换文件");
        ConfigureButton(_btnClear, "清空…");
        ConfigureButton(_btnExit, "退出");

        _btnPause.Click += (_, _) => TogglePause();
        _btnOpenFile.Click += (_, _) => OpenClipsFile();
        _btnOpenFolder.Click += (_, _) => OpenClipsFolder();
        _btnChangeFile.Click += (_, _) => ChangeClipsFile();
        _btnClear.Click += (_, _) => ClearAll();
        _btnExit.Click += (_, _) => ExitApp();

        panelButtons.Controls.AddRange(new Control[]
        {
            _btnPause, _btnOpenFile, _btnOpenFolder, _btnChangeFile, _btnClear, _btnExit,
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
        menu.Items.Add("显示窗口", null, (_, _) => ShowMainWindow());
        menu.Items.Add("暂停/继续", null, (_, _) => TogglePause());
        menu.Items.Add("打开 txt", null, (_, _) => OpenClipsFile());
        menu.Items.Add("打开文件夹", null, (_, _) => OpenClipsFolder());
        menu.Items.Add("更换文件", null, (_, _) => ChangeClipsFile());
        menu.Items.Add("清空…", null, (_, _) => ClearAll());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApp());

        _notifyIcon.Text = "ClipJournal";
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
                var content = TextNormalizer.StripNumberPrefix(lines[i]);
                var index = startIndex + i;
                _listBox.Items.Add(FormatItem(index, "历史", content));
            }

            if (_listBox.Items.Count > 0)
            {
                _listBox.TopIndex = _listBox.Items.Count - 1;
                _lastLine = TextNormalizer.StripNumberPrefix(lines[^1]);
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
        if (!_listening || _exiting)
        {
            return;
        }

        if (!ClipboardReader.TryReadUnicodeText(out var text, out _))
        {
            ShowTempMessage("读取剪贴板失败");
            return;
        }

        // No Unicode text (e.g. image-only clipboard): ignore silently.
        if (text is null || TextNormalizer.IsIgnorable(text))
        {
            return;
        }

        var line = TextNormalizer.ToSingleLine(text);
        if (TextNormalizer.IsIgnorable(line))
        {
            return;
        }

        var (truncated, wasTruncated) = TextNormalizer.Truncate(line, MaxLineChars);
        line = truncated;

        if (string.Equals(line, _lastLine, StringComparison.Ordinal))
        {
            ShowTempMessage("与上一条相同，已跳过");
            return;
        }

        var index = _nextIndex;
        var numbered = TextNormalizer.FormatNumberedLine(index, line);

        try
        {
            _store.AppendLine(numbered);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "写入文件失败：\n" + ex.Message,
                "ClipJournal",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
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
            ShowTempMessage("内容过长，已截断到 256KB");
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
        _btnPause.Text = _listening ? "暂停" : "继续";
        UpdateStatus();
        ShowTempMessage(_listening ? "已继续监听" : "已暂停监听");
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
            Process.Start(new ProcessStartInfo
            {
                FileName = _store.FilePath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ClipJournal", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenClipsFolder()
    {
        try
        {
            EnsureClipsFileExists();
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{_store.FilePath}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ClipJournal", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ChangeClipsFile()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "选择保存的 txt 文件",
            Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
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
            _store.SetFilePath(dialog.FileName);
            if (!File.Exists(_store.FilePath))
            {
                _store.Clear();
            }

            _settings.ClipsFilePath = _store.FilePath;
            _settings.Save();
            LoadHistory();
            UpdateStatus();
            ShowTempMessage("已切换保存文件");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ClipJournal", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearAll()
    {
        var result = MessageBox.Show(
            this,
            "将清空列表，并清空当前 txt 文件中的全部内容。确定吗？",
            "ClipJournal",
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
            ShowTempMessage("已清空");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "ClipJournal", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void EnsureClipsFileExists()
    {
        if (!File.Exists(_store.FilePath))
        {
            _store.Clear();
        }
    }

    private void UpdateStatus()
    {
        _statusListen.Text = _listening ? "监听中" : "已暂停";
        _statusCount.Text = $"共 {_totalCount} 条";
        _statusPath.Text = _store.FilePath;
        _notifyIcon.Text = _listening ? "ClipJournal - 监听中" : "ClipJournal - 已暂停";
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
            ShowTempMessage("已隐藏到托盘，仍在监听");
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
        _notifyIcon.Dispose();
        _messageTimer.Dispose();
        Application.Exit();
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
