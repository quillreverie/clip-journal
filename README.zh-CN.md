# ClipJournal

[English](README.md) | 简体中文

Windows 剪贴板文本收集小工具：每次复制的文字自动压成**一行**，追加到本地 `.txt`。窗口列表实时显示进度，系统托盘常驻。

## 需求与行为

- 监听系统剪贴板（文本）
- 多行内容压成单行（换行/Tab → 空格）
- 界面列表带递增序号；**txt 成品只写正文**（不写序号）
- 可设「每 N 条空一行」：在 txt 里分组插入空行（0=关闭）
- 与上一条完全相同则跳过
- 图片等非文本忽略
- 默认写入：`文档\ClipJournal\clips.txt`（可在界面更换）
- 点窗口关闭只是隐藏到托盘；托盘菜单「退出」才真正结束

## 运行环境

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（开发）
- 运行已发布版本需要 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

## 开发与运行

```powershell
git clone https://github.com/quillreverie/clip-journal.git
cd clip-journal
dotnet test
dotnet run --project src\ClipJournal
```

发布：

```powershell
dotnet publish src\ClipJournal\ClipJournal.csproj -c Release -r win-x64 --self-contained false -o artifacts\ClipJournal
```

运行 `artifacts\ClipJournal\ClipJournal.exe`。

## 界面操作

| 操作 | 说明 |
|---|---|
| 暂停 / 继续 | 临时停止或恢复收集 |
| 打开 txt | 用默认程序打开当前文件 |
| 打开文件夹 | 在资源管理器中定位文件 |
| 更换文件 | 选择新的保存路径（旧文件保留） |
| 清空 | 清空列表并清空当前 txt |
| 每 N 条空一行 | 写入第 N、2N、3N… 条后，在 txt 多插一个空行；填 0 关闭 |
| 退出 | 停止监听并退出程序 |

## 语言

界面语言跟随 Windows 显示语言：系统 UI 区域为 `zh*` 时用**中文**，否则用**英文**。

## 隐私说明

本程序会把你复制的文字以**明文**写入本地 txt。请勿在复制密码、验证码、密钥时保持监听；可使用「暂停」或「清空」。

## 许可证

[MIT](LICENSE)
