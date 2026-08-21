using System.Collections.Concurrent;
using ZH.PrintAgent.Core;

namespace ZH.PrintAgent.Infrastructure;

public sealed class KeyedSemaphorePrinterLockProvider : IPrinterLockProvider
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<IAsyncDisposable> AcquireAsync(string printerName, CancellationToken cancellationToken)
    {
        var key = string.IsNullOrWhiteSpace(printerName) ? "default" : printerName.Trim();
        var semaphore = locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim semaphore;
        private bool disposed;

        public Releaser(SemaphoreSlim semaphore)
        {
            this.semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            if (!disposed)
            {
                semaphore.Release();
                disposed = true;
            }

            return ValueTask.CompletedTask;
        }
    }
}
