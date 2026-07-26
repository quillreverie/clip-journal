namespace ClipJournal;

public static class SingleInstance
{
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
}
