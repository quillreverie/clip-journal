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
                    // Bind the read length to the global memory's actual size. The clipboard
                    // data is supposed to be NUL-terminated, but a buggy or hostile producer
                    // can omit it; an unbounded PtrToStringUni would then scan past the
                    // allocation into garbage or fault. Char count = byte size / 2.
                    var sizeBytes = GlobalSize(handle);
                    var charCount = sizeBytes <= 0 ? 0 : (int)(sizeBytes / 2);
                    var value = charCount > 0
                        ? Marshal.PtrToStringUni(pointer, charCount)!
                        : string.Empty;

                    // Sample the sequence number *after* the memory copy so the consistency
                    // check covers the read itself, not just the window before it.
                    var seqAfter = GetClipboardSequenceNumber();
                    if (seqBefore != seqAfter)
                    {
                        // Clipboard changed while we were reading.
                        continue;
                    }

                    // CF_UNICODETEXT is a NUL-terminated C string: cut at the first NUL.
                    // GlobalSize may report a block larger than the text (trailing padding),
                    // so we cannot trust charCount as the real content length.
                    var firstNul = value.IndexOf('\0');
                    if (firstNul >= 0)
                    {
                        value = value[..firstNul];
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
