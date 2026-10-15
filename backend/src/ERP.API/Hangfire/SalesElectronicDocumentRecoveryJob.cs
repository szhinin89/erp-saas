using ERP.Infrastructure.BackgroundProcessing;
using ERP.Application.Modules.Sales.Services;
using ERP.Infrastructure.Services;

namespace ERP.API.Hangfire;

public sealed class SalesElectronicDocumentRecoveryJob(
    IServiceScopeFactory scopeFactory, ILogger<SalesElectronicDocumentRecoveryJob> logger)
    : IRecurringJob
{
    public async Task ExecuteAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SalesElectronicRecoveryCandidate> candidates;
        await using (var discovery = scopeFactory.CreateAsyncScope())
            candidates = await discovery.ServiceProvider
                .GetRequiredService<ISalesElectronicDocumentRecovery>()
                .GetCandidatesAsync(batchSize, cancellationToken);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var context = JobExecutionContext.Begin(candidate.TenantId, candidate.CompanyId);
            await using var scope = scopeFactory.CreateAsyncScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<ISalesElectronicDocumentRecovery>()
                    .RecoverAsync(candidate, cancellationToken);
                JobMetrics.RecordProcessed();
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                JobMetrics.RecordFailed();
                logger.LogError(ex, "Sales electronic recovery failed for {InvoiceId} ({TenantId}/{CompanyId}/{BranchId})",
                    candidate.InvoiceId, candidate.TenantId, candidate.CompanyId, candidate.BranchId);
                // Absence of the document remains durable work for the next execution.
            }
        }
    }
}
