using ERP.Infrastructure.Communications;
using ERP.Infrastructure.BackgroundProcessing;
namespace ERP.API.Hangfire;
public sealed class ReconcileElectronicDocumentCommunicationsJob(IElectronicDocumentCommunicationReconciler reconciler) : IRecurringJob
{
    public async Task ExecuteAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var result = await reconciler.ReconcileAsync(batchSize, cancellationToken);
        JobMetrics.RecordProcessed(result.Queued + result.AlreadyQueued);
        JobMetrics.RecordFailed(result.Failed);
    }
}
