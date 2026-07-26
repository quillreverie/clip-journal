using System.Runtime.InteropServices;

namespace ClipJournal;

public sealed class ClipboardListener : NativeWindow, IDisposable
{
    private const int WmClipboardUpdate = 0x031D;
    private static readonly IntPtr HwndMessage = new(-3);
    private bool _started;

    public event Action? ClipboardUpdate;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        CreateHandle(new CreateParams
        {
            Parent = HwndMessage,
        });

        if (!AddClipboardFormatListener(Handle))
        {
            var error = Marshal.GetLastWin32Error();
            DestroyHandle();
            throw new InvalidOperationException($"AddClipboardFormatListener failed: {error}");
        }

        _started = true;
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        if (Handle != IntPtr.Zero)
        {
            RemoveClipboardFormatListener(Handle);
            DestroyHandle();
        }

        _started = false;
    }

    public void Dispose()
    {
        Stop();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmClipboardUpdate)
        {
            ClipboardUpdate?.Invoke();
        }

        base.WndProc(ref m);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
