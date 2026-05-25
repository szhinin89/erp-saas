using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Persistence.Outbox;

internal sealed class OutboxProcessor : IOutboxProcessor
{
    private const int BatchSize = 50;

    private readonly ErpDbContext _db;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(ErpDbContext db, ILogger<OutboxProcessor> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _db.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return;

        _logger.LogDebug("Outbox: processing {Count} pending messages", pending.Count);

        foreach (var message in pending)
        {
            try
            {
                // Foundation phase: durably mark as processed.
                //
                // Future enhancements (add here, do NOT couple to domain):
                //   Phase 4 — forward to analytics sink / read-model projector
                //   Phase 5 — route to ERP.AI.Application event handlers
                //   Phase 6 — publish to external message bus (Kafka, Service Bus)
                //
                // The EventName and MetadataJson fields are already populated and stable
                // for downstream routing once those phases are implemented.
                message.MarkProcessed();

                _logger.LogDebug(
                    "Outbox: processed {EventName} v{Version} (Id={Id}, Subscriber={SubscriberId})",
                    message.EventName, message.EventVersion, message.Id, message.SubscriberId);
            }
            catch (Exception ex)
            {
                message.MarkFailed(ex.Message);
                _logger.LogError(ex,
                    "Outbox: failed to process {EventName} (Id={Id})",
                    message.EventName, message.Id);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        // TODO (Phase 4 — Retention): add a separate purge job that deletes or archives
        // OutboxMessages where ProcessedOnUtc < UtcNow - RetentionWindow.
        // Policy defined in AI-RULES/OUTBOX-RETENTION.md.
        // Do NOT implement here; create a dedicated IOutboxRetentionJob.
    }
}
