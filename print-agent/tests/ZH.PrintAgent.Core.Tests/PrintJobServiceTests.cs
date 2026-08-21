using ZH.PrintAgent.Contracts;
using ZH.PrintAgent.Core;

namespace ZH.PrintAgent.Core.Tests;

public sealed class PrintJobServiceTests
{
    [Fact]
    public async Task SubmitAsync_rejects_missing_required_fields()
    {
        var service = new PrintJobService(new InMemoryPrintJobStore(), TestPrinterCatalog.Default(), new FixedClock());

        var result = await service.SubmitAsync(new SubmitPrintJobRequest(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("jobId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Contains("printerName", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SubmitAsync_rejects_null_receipt()
    {
        var service = new PrintJobService(new InMemoryPrintJobStore(), TestPrinterCatalog.Default(), new FixedClock());

        var result = await service.SubmitAsync(new SubmitPrintJobRequest
        {
            JobId = "null-receipt",
            PrinterName = "POS-80",
            Receipt = null
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("receipt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SubmitAsync_rejects_unknown_or_disabled_printer()
    {
        var service = new PrintJobService(new InMemoryPrintJobStore(), TestPrinterCatalog.Default(), new FixedClock());

        var unknown = await service.SubmitAsync(ValidRequest("unknown", "MISSING"), CancellationToken.None);
        var disabled = await service.SubmitAsync(ValidRequest("disabled", "DISABLED"), CancellationToken.None);

        Assert.False(unknown.Success);
        Assert.False(disabled.Success);
        Assert.Contains(unknown.Errors, error => error.Contains("not configured or is disabled", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(disabled.Errors, error => error.Contains("not configured or is disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SubmitAsync_rejects_unsupported_driver()
    {
        var service = new PrintJobService(
            new InMemoryPrintJobStore(),
            new TestPrinterCatalog(new[]
            {
                new PrinterInfo { Name = "ODD", Driver = "parallel-port", Enabled = true }
            }),
            new FixedClock());

        var result = await service.SubmitAsync(ValidRequest("unsupported", "ODD"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("unsupported driver", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SubmitAsync_is_idempotent_for_same_job_id()
    {
        var store = new InMemoryPrintJobStore();
        var service = new PrintJobService(store, TestPrinterCatalog.Default(), new FixedClock());
        var request = ValidRequest("same-job", "POS-80");

        var first = await service.SubmitAsync(request, CancellationToken.None);
        var second = await service.SubmitAsync(request, CancellationToken.None);

        Assert.True(first.Success);
        Assert.False(first.Duplicate);
        Assert.True(second.Success);
        Assert.True(second.Duplicate);
        Assert.Single(await store.ListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RetryAsync_moves_failed_job_back_to_pending()
    {
        var store = new InMemoryPrintJobStore();
        var clock = new FixedClock();
        var service = new PrintJobService(store, TestPrinterCatalog.Default(), clock);
        var request = ValidRequest("retry-job", "POS-80");
        var submitted = await service.SubmitAsync(request, CancellationToken.None);
        var failed = submitted.Job!
            .MarkProcessing(clock.UtcNow)
            .MarkFailed("offline", clock.UtcNow, maxAttempts: 1, TimeSpan.FromSeconds(1));
        await store.UpdateAsync(failed, CancellationToken.None);

        var retried = await service.RetryAsync("retry-job", CancellationToken.None);

        Assert.NotNull(retried);
        Assert.Equal(PrintJobStatus.Pending, retried.Status);
        Assert.Equal(0, retried.Attempts);
        Assert.Null(retried.LastError);
    }

    [Fact]
    public async Task RetryAsync_moves_needs_review_job_back_to_pending_only_when_requested()
    {
        var store = new InMemoryPrintJobStore();
        var clock = new FixedClock();
        var service = new PrintJobService(store, TestPrinterCatalog.Default(), clock);
        var job = PrintJob
            .Create(ValidRequest("review-job", "POS-80"), clock.UtcNow)
            .MarkNeedsReview(clock.UtcNow, "manual review needed");
        await store.TryAddAsync(job, CancellationToken.None);

        var retried = await service.RetryAsync("review-job", CancellationToken.None);

        Assert.NotNull(retried);
        Assert.Equal(PrintJobStatus.Pending, retried.Status);
        Assert.Equal(0, retried.Attempts);
        Assert.Null(retried.LastError);
    }

    public static SubmitPrintJobRequest ValidRequest(string jobId, string printerName)
    {
        return new SubmitPrintJobRequest
        {
            JobId = jobId,
            PrinterName = printerName,
            Copies = 1,
            Receipt = new ReceiptDocument
            {
                MerchantName = "ZH Technologies",
                Items = new[]
                {
                    new ReceiptLineItem { Name = "Demo item", Quantity = 1, UnitPrice = 10, Total = 10 }
                },
                Totals = new[]
                {
                    new ReceiptTotalLine { Label = "TOTAL", Amount = 10 }
                }
            }
        };
    }
}

public sealed class ReceiptFormatterTests
{
    [Fact]
    public void Format80Mm_keeps_lines_within_thermal_width()
    {
        var formatter = new ReceiptFormatter();
        var receipt = new ReceiptDocument
        {
            MerchantName = "ZH Technologies",
            HeaderLines = new[]
            {
                "A very long header line that should wrap cleanly before it reaches the physical paper edge"
            },
            Items = new[]
            {
                new ReceiptLineItem
                {
                    Name = "Long product name with enough words to wrap over several lines",
                    Quantity = 2,
                    UnitPrice = 3.25m,
                    Total = 6.50m
                }
            }
        };

        var formatted = formatter.Format80Mm(receipt);

        Assert.All(
            formatted.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries),
            line => Assert.True(line.Length <= ReceiptFormatter.Columns80Mm, $"Line too long: '{line}'"));
    }
}

public sealed class PrintJobProcessorTests
{
    [Fact]
    public async Task ProcessNextAsync_marks_job_failed_with_retry_backoff()
    {
        var store = new InMemoryPrintJobStore();
        var clock = new FixedClock();
        await store.TryAddAsync(PrintJob.Create(PrintJobServiceTests.ValidRequest("fail-job", "POS-80"), clock.UtcNow), CancellationToken.None);
        var processor = new PrintJobProcessor(
            store,
            new FailingPrinter(),
            new NoopPrinterLockProvider(),
            new ReceiptFormatter(),
            clock,
            new PrintProcessingOptions { MaxAttempts = 2, BaseRetryDelay = TimeSpan.FromSeconds(7) });

        var processed = await processor.ProcessNextAsync(CancellationToken.None);

        Assert.NotNull(processed);
        Assert.Equal(PrintJobStatus.Pending, processed.Status);
        Assert.Equal(1, processed.Attempts);
        Assert.Equal(clock.UtcNow.AddSeconds(7), processed.NextAttemptAt);
    }

    [Fact]
    public async Task RecoverStaleProcessingAsync_moves_old_processing_jobs_to_needs_review()
    {
        var store = new InMemoryPrintJobStore();
        var clock = new FixedClock();
        var oldProcessing = PrintJob
            .Create(PrintJobServiceTests.ValidRequest("stale", "POS-80"), clock.UtcNow.AddMinutes(-10))
            .MarkProcessing(clock.UtcNow.AddMinutes(-10));
        await store.TryAddAsync(oldProcessing, CancellationToken.None);
        var processor = new PrintJobProcessor(
            store,
            new SuccessfulPrinter(),
            new NoopPrinterLockProvider(),
            new ReceiptFormatter(),
            clock,
            new PrintProcessingOptions { ProcessingStaleAfter = TimeSpan.FromMinutes(5) });

        var recovered = await processor.RecoverStaleProcessingAsync(CancellationToken.None);
        var job = await store.GetAsync("stale", CancellationToken.None);

        Assert.Equal(1, recovered);
        Assert.NotNull(job);
        Assert.Equal(PrintJobStatus.NeedsReview, job.Status);
        Assert.Contains("Review physical printer output", job.LastError);
    }
}

internal sealed class TestPrinterCatalog : IPrinterCatalog
{
    private readonly IReadOnlyList<PrinterInfo> printers;

    public TestPrinterCatalog(IReadOnlyList<PrinterInfo> printers)
    {
        this.printers = printers;
    }

    public static TestPrinterCatalog Default()
    {
        return new TestPrinterCatalog(new[]
        {
            new PrinterInfo { Name = "POS-80", Driver = PrinterDrivers.Simulated, Enabled = true },
            new PrinterInfo { Name = "DISABLED", Driver = PrinterDrivers.Simulated, Enabled = false }
        });
    }

    public Task<IReadOnlyList<PrinterInfo>> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(printers);
    }

    public Task<PrinterInfo?> FindEnabledAsync(string printerName, CancellationToken cancellationToken)
    {
        var printer = printers.FirstOrDefault(candidate =>
            candidate.Enabled &&
            string.Equals(candidate.Name, printerName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(printer);
    }
}

internal sealed class InMemoryPrintJobStore : IPrintJobStore
{
    private readonly List<PrintJob> jobs = new();

    public Task<StoreAddResult> TryAddAsync(PrintJob job, CancellationToken cancellationToken)
    {
        var existing = jobs.FirstOrDefault(candidate =>
            string.Equals(candidate.JobId, job.JobId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return Task.FromResult(new StoreAddResult(false, existing));
        }

        jobs.Add(job);
        return Task.FromResult(new StoreAddResult(true, job));
    }

    public Task<PrintJob?> GetAsync(string jobId, CancellationToken cancellationToken)
    {
        return Task.FromResult(jobs.FirstOrDefault(candidate =>
            string.Equals(candidate.JobId, jobId, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<IReadOnlyList<PrintJob>> ListAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<PrintJob>>(jobs.ToArray());
    }

    public Task<PrintJob?> TryLeaseNextDueJobAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var index = jobs.FindIndex(job =>
            job.Status == PrintJobStatus.Pending &&
            (job.NextAttemptAt is null || job.NextAttemptAt <= now));
        if (index < 0)
        {
            return Task.FromResult<PrintJob?>(null);
        }

        jobs[index] = jobs[index].MarkProcessing(now);
        return Task.FromResult<PrintJob?>(jobs[index]);
    }

    public Task UpdateAsync(PrintJob job, CancellationToken cancellationToken)
    {
        var index = jobs.FindIndex(candidate =>
            string.Equals(candidate.JobId, job.JobId, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            jobs.Add(job);
        }
        else
        {
            jobs[index] = job;
        }

        return Task.CompletedTask;
    }
}

internal sealed class FixedClock : ISystemClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
}

internal sealed class FailingPrinter : IReceiptPrinter
{
    public Task PrintAsync(PrintJob job, string formattedReceipt, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("printer offline");
    }
}

internal sealed class SuccessfulPrinter : IReceiptPrinter
{
    public Task PrintAsync(PrintJob job, string formattedReceipt, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

internal sealed class NoopPrinterLockProvider : IPrinterLockProvider
{
    public ValueTask<IAsyncDisposable> AcquireAsync(string printerName, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<IAsyncDisposable>(new NoopReleaser());
    }

    private sealed class NoopReleaser : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
