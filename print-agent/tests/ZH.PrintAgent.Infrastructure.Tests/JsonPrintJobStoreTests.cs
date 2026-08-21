using ZH.PrintAgent.Contracts;
using ZH.PrintAgent.Core;
using ZH.PrintAgent.Infrastructure;

namespace ZH.PrintAgent.Infrastructure.Tests;

public sealed class JsonPrintJobStoreTests
{
    [Fact]
    public async Task Store_persists_pending_jobs_after_reopen()
    {
        var path = Path.Combine(Path.GetTempPath(), "zh-print-agent-tests", Guid.NewGuid().ToString("N"), "jobs.json");
        var store = new JsonPrintJobStore(path);
        var job = PrintJob.Create(ValidRequest("persisted"), DateTimeOffset.UtcNow);

        await store.TryAddAsync(job, CancellationToken.None);
        var reopened = new JsonPrintJobStore(path);
        var loaded = await reopened.GetAsync("persisted", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(PrintJobStatus.Pending, loaded.Status);
    }

    [Fact]
    public async Task Store_does_not_duplicate_same_job_id()
    {
        var path = Path.Combine(Path.GetTempPath(), "zh-print-agent-tests", Guid.NewGuid().ToString("N"), "jobs.json");
        var store = new JsonPrintJobStore(path);
        var job = PrintJob.Create(ValidRequest("duplicate"), DateTimeOffset.UtcNow);

        var first = await store.TryAddAsync(job, CancellationToken.None);
        var second = await store.TryAddAsync(job, CancellationToken.None);
        var allJobs = await store.ListAsync(CancellationToken.None);

        Assert.True(first.Added);
        Assert.False(second.Added);
        Assert.Single(allJobs);
    }

    [Fact]
    public async Task TryLeaseNextDueJobAsync_marks_job_processing_persistently()
    {
        var path = Path.Combine(Path.GetTempPath(), "zh-print-agent-tests", Guid.NewGuid().ToString("N"), "jobs.json");
        var store = new JsonPrintJobStore(path);
        await store.TryAddAsync(PrintJob.Create(ValidRequest("lease"), DateTimeOffset.UtcNow), CancellationToken.None);

        var leased = await store.TryLeaseNextDueJobAsync(DateTimeOffset.UtcNow, CancellationToken.None);
        var reopened = new JsonPrintJobStore(path);
        var loaded = await reopened.GetAsync("lease", CancellationToken.None);

        Assert.NotNull(leased);
        Assert.NotNull(loaded);
        Assert.Equal(PrintJobStatus.Processing, loaded.Status);
        Assert.Equal(1, loaded.Attempts);
    }

    [Fact]
    public async Task Store_recovers_from_backup_when_primary_json_is_corrupt()
    {
        var directory = Path.Combine(Path.GetTempPath(), "zh-print-agent-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "jobs.json");
        var store = new JsonPrintJobStore(path);
        await store.TryAddAsync(PrintJob.Create(ValidRequest("backup-job"), DateTimeOffset.UtcNow), CancellationToken.None);
        await store.TryAddAsync(PrintJob.Create(ValidRequest("newer-job"), DateTimeOffset.UtcNow), CancellationToken.None);
        await File.WriteAllTextAsync(path, "{ this is not json", CancellationToken.None);

        var recoveredStore = new JsonPrintJobStore(path);
        var recovered = await recoveredStore.GetAsync("backup-job", CancellationToken.None);

        Assert.NotNull(recovered);
        Assert.NotEmpty(Directory.GetFiles(directory, "*.corrupt-*"));
    }

    private static SubmitPrintJobRequest ValidRequest(string jobId)
    {
        return new SubmitPrintJobRequest
        {
            JobId = jobId,
            PrinterName = "POS-80",
            Receipt = new ReceiptDocument
            {
                MerchantName = "ZH Technologies",
                RawLines = new[] { "Test receipt" }
            }
        };
    }
}

public sealed class SimulatedReceiptPrinterTests
{
    [Fact]
    public async Task PrintAsync_writes_receipt_text_to_output_directory()
    {
        var output = Path.Combine(Path.GetTempPath(), "zh-print-agent-tests", Guid.NewGuid().ToString("N"), "printed");
        var printer = new SimulatedReceiptPrinter(output, Array.Empty<string>());
        var job = PrintJob.Create(JsonPrintJobStoreTests_Requests.ValidRequest("printed"), DateTimeOffset.UtcNow).MarkProcessing(DateTimeOffset.UtcNow);

        await printer.PrintAsync(job, "receipt body", CancellationToken.None);

        Assert.NotEmpty(Directory.GetFiles(output, "*.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task PrintAsync_can_simulate_printer_failure()
    {
        var output = Path.Combine(Path.GetTempPath(), "zh-print-agent-tests", Guid.NewGuid().ToString("N"), "printed");
        var printer = new SimulatedReceiptPrinter(output, new[] { "POS-80" });
        var job = PrintJob.Create(JsonPrintJobStoreTests_Requests.ValidRequest("failed"), DateTimeOffset.UtcNow).MarkProcessing(DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            printer.PrintAsync(job, "receipt body", CancellationToken.None));
    }
}

public sealed class RoutingReceiptPrinterTests
{
    [Fact]
    public async Task PrintAsync_routes_simulated_driver_to_simulated_printer()
    {
        var output = Path.Combine(Path.GetTempPath(), "zh-print-agent-tests", Guid.NewGuid().ToString("N"), "printed");
        var router = new RoutingReceiptPrinter(
            new ConfiguredPrinterCatalog(new[]
            {
                new PrinterInfo { Name = "POS-80", Driver = PrinterDrivers.Simulated, Enabled = true }
            }),
            new SimulatedReceiptPrinter(output, Array.Empty<string>()),
            new WindowsRawReceiptPrinter());
        var job = PrintJob.Create(JsonPrintJobStoreTests_Requests.ValidRequest("routed"), DateTimeOffset.UtcNow)
            .MarkProcessing(DateTimeOffset.UtcNow);

        await router.PrintAsync(job, "receipt body", CancellationToken.None);

        Assert.NotEmpty(Directory.GetFiles(output, "*.txt", SearchOption.AllDirectories));
    }
}

public sealed class KeyedSemaphorePrinterLockProviderTests
{
    [Fact]
    public async Task AcquireAsync_serializes_locks_for_same_printer_name()
    {
        var provider = new KeyedSemaphorePrinterLockProvider();
        await using var first = await provider.AcquireAsync("POS-80", CancellationToken.None);

        var secondTask = provider.AcquireAsync("pos-80", CancellationToken.None).AsTask();
        await Task.Delay(50);

        Assert.False(secondTask.IsCompleted);
        await first.DisposeAsync();
        await using var second = await secondTask;
    }
}

internal static class JsonPrintJobStoreTests_Requests
{
    public static SubmitPrintJobRequest ValidRequest(string jobId)
    {
        return new SubmitPrintJobRequest
        {
            JobId = jobId,
            PrinterName = "POS-80",
            Receipt = new ReceiptDocument
            {
                MerchantName = "ZH Technologies",
                RawLines = new[] { "Test receipt" }
            }
        };
    }
}
