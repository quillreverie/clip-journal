# ClipJournal

English | [简体中文](README.zh-CN.md)

A small Windows tray app that watches the clipboard for text, flattens each copy into **one line**, and appends it to a local `.txt` file. The main window shows live progress; the app stays in the system tray.

## Behavior

- Listens for system clipboard **text** only
- Collapses multi-line content into a single line (newlines / tabs → spaces)
- UI list uses sequential numbers; the **txt file stores plain content only** (no numbers)
- Optional “blank line every N clips” in the txt (0 = off)
- Skips when the new line is identical to the previous one
- Ignores images and other non-text clipboard data
- Default output: `Documents\ClipJournal\clips.txt` (changeable in the UI)
- Closing the window hides to tray; use tray **Exit** to quit

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) for development
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) for published builds (`--self-contained false`)

## Build & run

```powershell
git clone https://github.com/quillreverie/clip-journal.git
cd clip-journal
dotnet test
dotnet run --project src\ClipJournal
```

Publish:

```powershell
dotnet publish src\ClipJournal\ClipJournal.csproj -c Release -r win-x64 --self-contained false -o artifacts\ClipJournal
```

Then run `artifacts\ClipJournal\ClipJournal.exe`.

## UI

| Action | Description |
|---|---|
| Pause / Resume | Temporarily stop or resume collection |
| Search | Filter the latest 500 on-screen previews by content or number |
| Copy a clip | Hover and use the copy action, or select a row and press Enter / Ctrl+C |
| Open txt | Open the current file with the default app |
| Open folder | Reveal the file in Explorer |
| Change file | Pick a new save path (old file is kept) |
| Clear | Clear the list and empty the current txt |
| Blank line every N | After clips N, 2N, 3N… insert a blank line in the txt; 0 disables |
| Exit | Stop listening and quit |

Keyboard shortcuts: `Ctrl+F` focuses search, and `Ctrl+Shift+P` pauses or resumes capture.

## Language

UI language follows the Windows display language: **Chinese** when the OS UI culture is `zh*`, otherwise **English**.

## Privacy

Copied text is written **in plain text** to a local file. Pause or clear before copying passwords, codes, or secrets.

## License

[MIT](LICENSE)
