using ZH.PrintAgent.Contracts;

namespace ZH.PrintAgent.Core;

public sealed class PrintJobService
{
    private readonly IPrintJobStore store;
    private readonly IPrinterCatalog printerCatalog;
    private readonly ISystemClock clock;

    public PrintJobService(IPrintJobStore store, IPrinterCatalog printerCatalog, ISystemClock clock)
    {
        this.store = store;
        this.printerCatalog = printerCatalog;
        this.clock = clock;
    }

    public async Task<SubmitPrintJobResult> SubmitAsync(SubmitPrintJobRequest request, CancellationToken cancellationToken)
    {
        var validationErrors = PrintJobValidator.Validate(request);
        if (validationErrors.Count > 0)
        {
            return SubmitPrintJobResult.Invalid(validationErrors);
        }

        var printer = await printerCatalog.FindEnabledAsync(request.PrinterName.Trim(), cancellationToken);
        if (printer is null)
        {
            return SubmitPrintJobResult.Invalid(new[]
            {
                $"Printer '{request.PrinterName}' is not configured or is disabled."
            });
        }

        if (!PrinterDrivers.IsSupported(printer.Driver))
        {
            return SubmitPrintJobResult.Invalid(new[]
            {
                $"Printer '{request.PrinterName}' uses unsupported driver '{printer.Driver}'."
            });
        }

        var job = PrintJob.Create(request, clock.UtcNow);
        var result = await store.TryAddAsync(job, cancellationToken);
        return SubmitPrintJobResult.Accepted(result.Job, duplicate: !result.Added);
    }

    public async Task<PrintJob?> GetAsync(string jobId, CancellationToken cancellationToken)
    {
        return await store.GetAsync(jobId, cancellationToken);
    }

    public async Task<IReadOnlyList<PrintJob>> ListAsync(CancellationToken cancellationToken)
    {
        return await store.ListAsync(cancellationToken);
    }

    public async Task<PrintJob?> CancelAsync(string jobId, CancellationToken cancellationToken)
    {
        var job = await store.GetAsync(jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        if (job.Status is PrintJobStatus.Printed or PrintJobStatus.Cancelled)
        {
            return job;
        }

        var cancelled = job.MarkCancelled(clock.UtcNow);
        await store.UpdateAsync(cancelled, cancellationToken);
        return cancelled;
    }

    public async Task<PrintJob?> RetryAsync(string jobId, CancellationToken cancellationToken)
    {
        var job = await store.GetAsync(jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        if (job.Status is PrintJobStatus.Printed or PrintJobStatus.Processing)
        {
            return job;
        }

        var retry = job.MarkManualRetry(clock.UtcNow);
        await store.UpdateAsync(retry, cancellationToken);
        return retry;
    }

    public async Task<PrintJob?> MarkReviewedAsync(string jobId, CancellationToken cancellationToken)
    {
        var job = await store.GetAsync(jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        if (job.Status is not (PrintJobStatus.Failed or PrintJobStatus.NeedsReview))
        {
            return job;
        }

        var reviewed = job.MarkReviewed(clock.UtcNow);
        await store.UpdateAsync(reviewed, cancellationToken);
        return reviewed;
    }
}

public sealed record SubmitPrintJobResult(
    bool Success,
    bool Duplicate,
    PrintJob? Job,
    IReadOnlyList<string> Errors)
{
    public static SubmitPrintJobResult Accepted(PrintJob job, bool duplicate)
    {
        return new SubmitPrintJobResult(true, duplicate, job, Array.Empty<string>());
    }

    public static SubmitPrintJobResult Invalid(IReadOnlyList<string> errors)
    {
        return new SubmitPrintJobResult(false, false, null, errors);
    }
}
