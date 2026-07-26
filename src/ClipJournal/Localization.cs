using System.Globalization;

namespace ClipJournal;

/// <summary>
/// Simple UI string table. Language follows the OS UI culture (Chinese if zh*, otherwise English).
/// </summary>
public static class Localization
{
    public static bool IsChinese { get; }

    static Localization()
    {
        var name = CultureInfo.CurrentUICulture.Name;
        IsChinese = name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    }

    public static string AppName => "ClipJournal";

    public static string AlreadyRunning => IsChinese
        ? "ClipJournal 已在运行。"
        : "ClipJournal is already running.";

    public static string PrivacyTitle => IsChinese
        ? "ClipJournal 隐私说明"
        : "ClipJournal privacy notice";

    public static string PrivacyMessage(string clipsPath) => IsChinese
        ? "ClipJournal 会监听剪贴板中的文字，并把每次复制压成一行后写入本地 txt 文件。\n\n" +
          "文件默认位置：\n" + clipsPath + "\n\n" +
          "请勿在记录密码等敏感信息时使用；可随时暂停监听或清空文件。\n\n是否继续？"
        : "ClipJournal watches the clipboard for text and appends each copy as a single line to a local .txt file.\n\n" +
          "Default file location:\n" + clipsPath + "\n\n" +
          "Do not keep it running while copying passwords or other secrets. You can pause listening or clear the file at any time.\n\nContinue?";

    public static string Pause => IsChinese ? "暂停" : "Pause";
    public static string Resume => IsChinese ? "继续" : "Resume";
    public static string OpenTxt => IsChinese ? "打开 txt" : "Open txt";
    public static string OpenFolder => IsChinese ? "打开文件夹" : "Open folder";
    public static string ChangeFile => IsChinese ? "更换文件" : "Change file";
    public static string Clear => IsChinese ? "清空…" : "Clear…";
    public static string Exit => IsChinese ? "退出" : "Exit";

    public static string BlankEveryPrefix => IsChinese ? "每" : "Blank line every";
    public static string BlankEverySuffix => IsChinese ? "条空一行 (0=关)" : "clips (0=off)";

    public static string BlankLineDisabled => IsChinese ? "已关闭自动空行" : "Auto blank lines off";
    public static string BlankLineEnabled(int n) => IsChinese
        ? $"每 {n} 条后插入空行"
        : $"Insert blank line every {n} clips";

    public static string ShowWindow => IsChinese ? "显示窗口" : "Show window";
    public static string PauseResume => IsChinese ? "暂停/继续" : "Pause / Resume";

    public static string HistoryLabel => IsChinese ? "历史" : "history";

    public static string ClipboardReadFailed => IsChinese ? "读取剪贴板失败" : "Failed to read clipboard";
    public static string SkippedDuplicate => IsChinese ? "与上一条相同，已跳过" : "Same as previous, skipped";
    public static string WriteFailed => IsChinese ? "写入文件失败：\n" : "Failed to write file:\n";
    public static string Truncated => IsChinese ? "内容过长，已截断到 256KB" : "Too long; truncated to 256KB";
    public static string BlankInserted(int index) => IsChinese
        ? $"第 {index} 条后已插入空行"
        : $"Blank line inserted after #{index}";

    public static string Resumed => IsChinese ? "已继续监听" : "Listening resumed";
    public static string Paused => IsChinese ? "已暂停监听" : "Listening paused";
    public static string FileSwitched => IsChinese ? "已切换保存文件" : "Save file changed";
    public static string Cleared => IsChinese ? "已清空" : "Cleared";
    public static string HiddenToTray => IsChinese ? "已隐藏到托盘，仍在监听" : "Hidden to tray; still listening";

    public static string Listening => IsChinese ? "监听中" : "Listening";
    public static string StatusPaused => IsChinese ? "已暂停" : "Paused";
    public static string TotalCount(int n) => IsChinese ? $"共 {n} 条" : $"{n} clips";
    public static string TrayListening => IsChinese ? "ClipJournal - 监听中" : "ClipJournal - Listening";
    public static string TrayPaused => IsChinese ? "ClipJournal - 已暂停" : "ClipJournal - Paused";

    public static string ChooseFileTitle => IsChinese ? "选择保存的 txt 文件" : "Choose the save txt file";
    public static string FilterTxt => IsChinese
        ? "文本文件 (*.txt)|*.txt"
        : "Text files (*.txt)|*.txt";

    public static string ClearConfirm(string path) => IsChinese
        ? "将清空列表，并清空以下 txt 文件中的全部内容：\n" + path + "\n\n确定吗？"
        : "This will clear the list and erase all content in:\n" + path + "\n\nContinue?";

    public static string OpenOnlyTxt => IsChinese
        ? "仅支持打开 .txt 文件"
        : "Only .txt files can be opened here";
}
