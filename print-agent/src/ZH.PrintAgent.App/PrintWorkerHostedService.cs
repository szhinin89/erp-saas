using ZH.PrintAgent.Core;

namespace ZH.PrintAgent.App;

public sealed class PrintWorkerHostedService : BackgroundService
{
    private readonly PrintJobProcessor processor;
    private readonly PrintAgentOptions options;
    private readonly ILogger<PrintWorkerHostedService> logger;

    public PrintWorkerHostedService(
        PrintJobProcessor processor,
        PrintAgentOptions options,
        ILogger<PrintWorkerHostedService> logger)
    {
        this.processor = processor;
        this.options = options;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var recovered = await processor.RecoverStaleProcessingAsync(stoppingToken);
        logger.LogInformation(
            "Marked {RecoveredJobs} stale processing print jobs as NeedsReview after startup.",
            recovered);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await processor.ProcessNextAsync(stoppingToken);
                if (processed is null)
                {
                    await Task.Delay(options.PollIntervalMilliseconds, stoppingToken);
                    continue;
                }

                logger.LogInformation(
                    "Processed print job {JobId} for printer {PrinterName} with status {Status} after {Attempts} attempts.",
                    processed.JobId,
                    processed.PrinterName,
                    processed.Status,
                    processed.Attempts);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected print worker failure. Loop will continue.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
