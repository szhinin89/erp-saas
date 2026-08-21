using ZH.PrintAgent.Contracts;

namespace ZH.PrintAgent.Core;

public sealed class PrintJobProcessor
{
    private readonly IPrintJobStore store;
    private readonly IReceiptPrinter printer;
    private readonly IPrinterLockProvider lockProvider;
    private readonly ReceiptFormatter formatter;
    private readonly ISystemClock clock;
    private readonly PrintProcessingOptions options;

    public PrintJobProcessor(
        IPrintJobStore store,
        IReceiptPrinter printer,
        IPrinterLockProvider lockProvider,
        ReceiptFormatter formatter,
        ISystemClock clock,
        PrintProcessingOptions options)
    {
        this.store = store;
        this.printer = printer;
        this.lockProvider = lockProvider;
        this.formatter = formatter;
        this.clock = clock;
        this.options = options;
    }

    public async Task<PrintJob?> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var leased = await store.TryLeaseNextDueJobAsync(clock.UtcNow, cancellationToken);
        if (leased is null)
        {
            return null;
        }

        await using var printerLock = await lockProvider.AcquireAsync(leased.PrinterName, cancellationToken);
        var formattedReceipt = formatter.Format80Mm(leased.Receipt);

        try
        {
            for (var copy = 0; copy < leased.Copies; copy++)
            {
                await printer.PrintAsync(leased, formattedReceipt, cancellationToken);
            }

            var printed = leased.MarkPrinted(clock.UtcNow);
            await store.UpdateAsync(printed, cancellationToken);
            return printed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failed = leased.MarkFailed(ex.Message, clock.UtcNow, options.MaxAttempts, options.BaseRetryDelay);
            await store.UpdateAsync(failed, cancellationToken);
            return failed;
        }
    }

    public async Task<int> RecoverStaleProcessingAsync(CancellationToken cancellationToken)
    {
        var jobs = await store.ListAsync(cancellationToken);
        var cutoff = clock.UtcNow.Subtract(options.ProcessingStaleAfter);
        var recovered = 0;

        foreach (var job in jobs.Where(job =>
                     job.Status == PrintJobStatus.Processing &&
                     (job.ProcessingStartedAt is null || job.ProcessingStartedAt <= cutoff)))
        {
            await store.UpdateAsync(job.MarkNeedsReview(
                clock.UtcNow,
                "Print job was processing when the agent stopped. Review physical printer output before retrying manually."), cancellationToken);
            recovered++;
        }

        return recovered;
    }
}
