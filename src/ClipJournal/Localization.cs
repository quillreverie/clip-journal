using System.Globalization;

namespace ClipJournal;

/// <summary>
/// UI language follows the Windows display language: Chinese for zh*, English otherwise.
/// </summary>
public static class Localization
{
    public static bool IsChinese { get; } =
        CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    public static string AppName => "ClipJournal";
    public static string BrandSubtitle => IsChinese ? "安静地保存每次复制" : "A quiet log for every copy";

    public static string AlreadyRunning => IsChinese
        ? "ClipJournal 已在运行。"
        : "ClipJournal is already running.";

    public static string PrivacyTitle => IsChinese
        ? "开始使用 ClipJournal"
        : "Start using ClipJournal";

    public static string PrivacyMessage(string clipsPath) => IsChinese
        ? "ClipJournal 会监听剪贴板中的文字，将多行内容整理为一行，并写入本地 txt 文件。\n\n" +
          "默认保存位置：\n" + clipsPath + "\n\n" +
          "内容不会上传。复制密码、验证码或其他敏感信息前，建议先暂停监听。\n\n是否继续？"
        : "ClipJournal watches the clipboard for text, turns multi-line content into one line, and writes it to a local .txt file.\n\n" +
          "Default save location:\n" + clipsPath + "\n\n" +
          "Nothing is uploaded. Pause listening before copying passwords, codes, or other sensitive information.\n\nContinue?";

    public static string CaptureSectionLabel => IsChinese ? "剪贴板捕获" : "Clipboard capture";
    public static string ListeningHeadline => IsChinese ? "正在监听剪贴板" : "Listening to clipboard";
    public static string PausedHeadline => IsChinese ? "监听已暂停" : "Capture is paused";
    public static string LocalCountDescription(int count) => IsChinese
        ? $"本地已保存 {count} 条"
        : $"{count} saved locally";
    public static string PauseAction => IsChinese ? "暂停捕获" : "Pause capture";
    public static string ResumeAction => IsChinese ? "继续捕获" : "Resume capture";
    public static string PrivacyHint => IsChinese
        ? "记录只保存在本机。复制密码或验证码前，建议先暂停捕获。"
        : "Clips stay on this device. Pause capture before copying passwords or codes.";

    public static string RecordsTitle => IsChinese ? "剪贴记录" : "Clipboard journal";
    public static string RecordsSubtitle => IsChinese
        ? "自动整理为单行，最近复制的内容会出现在底部"
        : "Flattened into one line, with the newest clips at the bottom";
    public static string PreviewScope => IsChinese ? "显示最近 500 条预览" : "Showing the latest 500 previews";
    public static string CountSummary(int total) => IsChinese ? $"{total} 条记录" : $"{total} clips";
    public static string FilteredCountSummary(int filtered, int total) => IsChinese
        ? $"找到 {filtered} 条 · 共 {total} 条"
        : $"{filtered} found · {total} total";
    public static string FilteredPreviewSummary(int filtered) => IsChinese
        ? $"当前预览中找到 {filtered} 条"
        : $"{filtered} found in this preview";
    public static string SearchPlaceholder => IsChinese ? "搜索内容或序号" : "Search content or number";
    public static string ClearSearch => IsChinese ? "清除搜索" : "Clear search";
    public static string ClearRecords => IsChinese ? "清空记录" : "Clear journal";
    public static string ClearDialogTitle => IsChinese ? "清空全部记录？" : "Clear the entire journal?";
    public static string ContinueAction => IsChinese ? "继续使用" : "Continue";
    public static string CancelAction => IsChinese ? "取消" : "Cancel";
    public static string GotItAction => IsChinese ? "知道了" : "Got it";
    public static string ErrorTitle => IsChinese ? "出现了一点问题" : "Something went wrong";
    public static string EmptyTitle => IsChinese ? "等待第一次复制" : "Waiting for your first copy";
    public static string EmptyHint => IsChinese
        ? "在任意应用中复制文字，这里会自动出现记录"
        : "Copy text in any app and it will appear here automatically";
    public static string NoResultsTitle => IsChinese ? "没有匹配的记录" : "No matching clips";
    public static string NoResultsHint => IsChinese
        ? "换个关键词试试，或清除搜索条件"
        : "Try a different keyword or clear the search";
    public static string HistoryItem => IsChinese ? "历史" : "History";

    public static string StorageTitle => IsChinese ? "保存与格式" : "Save & format";
    public static string StoragePathLabel => IsChinese ? "当前保存位置" : "Current save location";
    public static string OpenFileShort => IsChinese ? "打开文本" : "Open file";
    public static string OpenFolderShort => IsChinese ? "打开位置" : "Open folder";
    public static string ChangeFileShort => IsChinese ? "更换保存文件" : "Change save file";
    public static string BlankLineLabel => IsChinese ? "自动分段" : "Automatic spacing";
    public static string BlankLineDescription => IsChinese
        ? "每隔指定条数插入空行；设为 0 表示关闭"
        : "Insert a blank line after this many clips; 0 turns it off";
    public static string BlankLineValue(int value) => value == 0
        ? (IsChinese ? "自动分段已关闭" : "Automatic spacing is off")
        : (IsChinese ? $"每 {value} 条插入空行" : $"Blank line every {value} clips");

    public static string Exit => IsChinese ? "退出" : "Exit";

    public static string BlankLineDisabled => IsChinese ? "已关闭自动分段" : "Automatic spacing turned off";
    public static string BlankLineEnabled(int n) => IsChinese
        ? $"现在每 {n} 条插入空行"
        : $"A blank line will be inserted every {n} clips";

    public static string ShowWindow => IsChinese ? "显示 ClipJournal" : "Show ClipJournal";

    public static string ClipboardReadFailed => IsChinese ? "暂时无法读取剪贴板" : "Clipboard could not be read";
    public static string ClipboardListenFailed => IsChinese
        ? "无法启动剪贴板监听，可能是另一个程序正在独占剪贴板。"
        : "Could not start clipboard listening; another app may be locking the clipboard.";
    public static string SkippedDuplicate => IsChinese ? "与上一条相同，已跳过" : "Same as the previous clip, so it was skipped";
    public static string Truncated => IsChinese ? "内容过长，已截断到 256KB" : "The clip was truncated to 256KB";
    public static string WriteFailedHint => IsChinese
        ? "写入文件失败：可能被其他程序占用或权限不足。可在「保存与格式」中更换保存文件。"
        : "Failed to write the file: it may be open in another program or you lack access. Try a different save file in \"Save & format\".";
    public static string UnexpectedError => IsChinese
        ? "捕获过程出现意外错误，已跳过本次复制。"
        : "An unexpected error occurred during capture; this clip was skipped.";
    public static string CopyFailedHint => IsChinese
        ? "复制到剪贴板失败：可能被其他程序占用，稍后再试。"
        : "Could not copy to the clipboard; it may be held by another app. Try again shortly.";
    public static string OpenFileFailedHint => IsChinese
        ? "无法打开该文件或所在位置，请确认文件仍然存在且未被移动。"
        : "Could not open the file or its folder. Make sure it still exists and hasn't been moved.";
    public static string ChangeFileFailedHint => IsChinese
        ? "无法切换到该保存文件：路径无效、不可写或被占用。"
        : "Could not switch to that save file: the path is invalid, not writable, or in use.";
    public static string ClearFailedHint => IsChinese
        ? "无法清空记录：文件可能正在被其他程序占用。"
        : "Could not clear the journal; the file may be open in another program.";
    public static string SaveSettingsFailedHint => IsChinese
        ? "无法保存设置：请检查磁盘空间或对该位置的写入权限。"
        : "Could not save settings; check free disk space and write access to that location.";
    public static string BlankInserted(int index) => IsChinese
        ? $"已在第 {index} 条后插入空行"
        : $"Blank line inserted after clip {index}";
    public static string Resumed => IsChinese ? "已继续捕获" : "Capture resumed";
    public static string Paused => IsChinese ? "已暂停捕获" : "Capture paused";
    public static string FileSwitched => IsChinese ? "已切换保存文件" : "Save file changed";
    public static string Cleared => IsChinese ? "记录已清空" : "Journal cleared";
    public static string CopySuccess => IsChinese ? "已复制这条记录" : "Clip copied";

    public static string TrayListening => IsChinese ? "ClipJournal · 正在捕获" : "ClipJournal · Capturing";
    public static string TrayPaused => IsChinese ? "ClipJournal · 已暂停" : "ClipJournal · Paused";
    public static string TrayHintTitle => IsChinese ? "ClipJournal 仍在运行" : "ClipJournal is still running";
    public static string TrayHintBody => IsChinese
        ? "窗口已隐藏到系统托盘。再次启动 ClipJournal 也会直接唤回窗口。"
        : "The window is in the system tray. Starting ClipJournal again will bring it back.";

    public static string ChooseFileTitle => IsChinese ? "选择保存的 txt 文件" : "Choose a text file";
    public static string FilterTxt => IsChinese ? "文本文件 (*.txt)|*.txt" : "Text files (*.txt)|*.txt";
    public static string ClearConfirm(string path) => IsChinese
        ? "这会清空界面中的全部记录，并永久清空以下 txt 文件：\n\n" + path + "\n\n确定继续吗？"
        : "This permanently clears every clip from the journal and empties this text file:\n\n" + path + "\n\nContinue?";
    public static string OpenOnlyTxt => IsChinese ? "这里只能打开 .txt 文件" : "Only .txt files can be opened here";

    public static string SettingsCorruptWarning
        => IsChinese
            ? "settings.json 已损坏，已恢复为默认设置。原文件已尝试备份为 settings.json.broken。"
            : "settings.json was unreadable and has been reset to defaults. The original was attempted to be backed up as settings.json.broken.";
}
