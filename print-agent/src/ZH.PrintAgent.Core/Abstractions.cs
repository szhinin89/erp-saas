using ZH.PrintAgent.Contracts;

namespace ZH.PrintAgent.Core;

public interface IPrintJobStore
{
    Task<StoreAddResult> TryAddAsync(PrintJob job, CancellationToken cancellationToken);
    Task<PrintJob?> GetAsync(string jobId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PrintJob>> ListAsync(CancellationToken cancellationToken);
    Task<PrintJob?> TryLeaseNextDueJobAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task UpdateAsync(PrintJob job, CancellationToken cancellationToken);
}

public sealed record StoreAddResult(bool Added, PrintJob Job);

public interface IReceiptPrinter
{
    Task PrintAsync(PrintJob job, string formattedReceipt, CancellationToken cancellationToken);
}

public interface IPrinterCatalog
{
    Task<IReadOnlyList<PrinterInfo>> ListAsync(CancellationToken cancellationToken);
    Task<PrinterInfo?> FindEnabledAsync(string printerName, CancellationToken cancellationToken);
}

public interface IPrinterLockProvider
{
    ValueTask<IAsyncDisposable> AcquireAsync(string printerName, CancellationToken cancellationToken);
}

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
