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

            if (!OpenClipboard(IntPtr.Zero))
            {
                continue;
            }

            try
            {
                // Sample the sequence number only after the clipboard is open, so the
                // consistency check covers the read itself rather than the (longer)
                // window before OpenClipboard, where a fast-changing clipboard would
                // wrongly discard every valid snapshot.
                var seqBefore = GetClipboardSequenceNumber();

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
                    var seqAfter = GetClipboardSequenceNumber();
                    // Bind the read length to the global memory's actual size. The clipboard
                    // data is supposed to be NUL-terminated, but a buggy or hostile producer
                    // can omit it; an unbounded PtrToStringUni would then scan past the
                    // allocation into garbage or fault. Char count = byte size / 2.
                    var sizeBytes = GlobalSize(handle);
                    var charCount = sizeBytes <= 0 ? 0 : (int)(sizeBytes / 2);
                    var value = charCount > 0
                        ? Marshal.PtrToStringUni(pointer, charCount)!
                        : string.Empty;
                    if (value.Length > 0 && value[^1] == '\0')
                    {
                        // Strip the explicit NUL terminator; keep any text after if present.
                        var firstNul = value.IndexOf('\0');
                        if (firstNul >= 0)
                        {
                            value = value[..firstNul];
                        }
                    }

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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalSize(IntPtr hMem);
}
