using System.Runtime.InteropServices;

namespace ClipJournal;

public static class ClipboardReader
{
    private const uint CfUnicodeText = 13;
    private const int MaxRetries = 5;
    private const int RetryDelayMs = 20;

    /// <summary>
    /// Tries to read Unicode text from the clipboard.
    /// Returns false only when the clipboard could not be opened after retries.
    /// Returns true with <paramref name="text"/> null when there is no Unicode text.
    /// </summary>
    public static bool TryReadUnicodeText(out string? text, out uint sequence)
    {
        text = null;
        sequence = 0;

        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                Thread.Sleep(RetryDelayMs);
            }

            var seqBefore = GetClipboardSequenceNumber();
            if (!OpenClipboard(IntPtr.Zero))
            {
                continue;
            }

            try
            {
                if (!IsClipboardFormatAvailable(CfUnicodeText))
                {
                    sequence = GetClipboardSequenceNumber();
                    return true;
                }

                var handle = GetClipboardData(CfUnicodeText);
                if (handle == IntPtr.Zero)
                {
                    continue;
                }

                var pointer = GlobalLock(handle);
                if (pointer == IntPtr.Zero)
                {
                    continue;
                }

                try
                {
                    var value = Marshal.PtrToStringUni(pointer);
                    var seqAfter = GetClipboardSequenceNumber();
                    if (seqBefore != seqAfter)
                    {
                        // Clipboard changed while we were reading.
                        continue;
                    }

                    sequence = seqAfter;
                    text = value;
                    return true;
                }
                finally
                {
                    GlobalUnlock(handle);
                }
            }
            finally
            {
                CloseClipboard();
            }
        }

        return false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);
}
