using System.Runtime.InteropServices;

namespace ClipJournal;

public static class SingleInstance
{
    private static readonly IntPtr BroadcastHandle = new(0xffff);
    private const string ShowMessageName = "ClipJournal.ShowMainWindow.7F53E2BD";

    public static int ShowWindowMessage { get; } =
        unchecked((int)RegisterWindowMessage(ShowMessageName));

    public static IDisposable? TryAcquire(string name)
    {
        var mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        if (createdNew)
        {
            return new MutexHolder(mutex);
        }

        mutex.Dispose();
        return null;
    }

    public static bool SignalShowWindow()
        => PostMessage(BroadcastHandle, unchecked((uint)ShowWindowMessage), IntPtr.Zero, IntPtr.Zero);

    private sealed class MutexHolder : IDisposable
    {
        private Mutex? _mutex;

        public MutexHolder(Mutex mutex)
        {
            _mutex = mutex;
        }

        public void Dispose()
        {
            var mutex = Interlocked.Exchange(ref _mutex, null);
            if (mutex is null)
            {
                return;
            }

            try
            {
                mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Already released or not owned.
            }

            mutex.Dispose();
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string messageName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);
}
