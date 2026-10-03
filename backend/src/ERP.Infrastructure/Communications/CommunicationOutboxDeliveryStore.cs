using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.Services;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — exclusión y fencing de la entrega en PostgreSQL
/// (ADR-039 D9). Ninguna operación mantiene una transacción abierta: cada método es UNA sentencia
/// (autocommit), y el envío SMTP ocurre entre el claim y la finalización, fuera de toda transacción.
/// <list type="bullet">
/// <item><see cref="ClaimNextAsync"/>: <c>UPDATE … FROM (SELECT … FOR UPDATE SKIP LOCKED LIMIT 1)
/// RETURNING</c>. Reclama una fila <c>Pending</c> vencida o una <c>Processing</c> con lease vencido
/// (recuperación de worker muerto), con un <c>ClaimToken</c> nuevo. Un <c>Processing</c> sin lease
/// (filas atascadas antes de este cambio) cuenta como vencido. Workers/nodos concurrentes nunca
/// obtienen la misma fila: <c>SKIP LOCKED</c> la saltea mientras otro la reclama y, una vez
/// confirmado, el lease vigente la excluye.</item>
/// <item><see cref="MarkSentAsync"/>/<see cref="MarkFailedAsync"/>: <c>UPDATE … WHERE id AND
/// status = Processing AND claim_token = @token</c>. 0 filas = el claim se perdió (otro worker la
/// recuperó); la fila no se toca.</item>
/// </list>
/// El claim no filtra por tenant/empresa (es cross-tenant por diseño) y devuelve solo lo mínimo para
/// abrir el scope; las finalizaciones corren dentro de <c>JobExecutionContext</c> y conservan los
/// filtros globales.
/// </summary>
public sealed class CommunicationOutboxDeliveryStore
{
    private static readonly Guid SystemActorId = Guid.Empty;

    private readonly ErpDbContext _db;

    public CommunicationOutboxDeliveryStore(ErpDbContext db)
    {
        _db = db;
    }

    public async Task<ClaimedCommunication?> ClaimNextAsync(
        DateTime utcNow,
        TimeSpan lease,
        CancellationToken ct = default
    )
    {
        var claimToken = Guid.NewGuid();
        var leaseUntil = utcNow + lease;
        var pending = nameof(CommunicationStatus.Pending);
        var processing = nameof(CommunicationStatus.Processing);

        var claimed = await _db
            .Database.SqlQuery<ClaimedCommunication>(
                $"""
                UPDATE communication_outbox AS o
                SET status = {processing},
                    claim_token = {claimToken},
                    lease_until_utc = {leaseUntil},
                    processing_started_at_utc = {utcNow},
                    retry_count = o.retry_count + CASE WHEN c.previous_status = {processing} THEN 1 ELSE 0 END,
                    updated_at = {utcNow},
                    updated_by = {SystemActorId}
                FROM (
                    SELECT id, status AS previous_status
                    FROM communication_outbox
                    WHERE (status = {pending}
                           AND scheduled_at_utc <= {utcNow}
                           AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= {utcNow}))
                       OR (status = {processing} AND (lease_until_utc IS NULL OR lease_until_utc < {utcNow}))
                    ORDER BY CASE priority WHEN 'High' THEN 3 WHEN 'Normal' THEN 2 ELSE 1 END DESC,
                             scheduled_at_utc
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                ) AS c
                WHERE o.id = c.id
                RETURNING o.id AS "Id",
                          o.tenant_id AS "TenantId",
                          o.company_id AS "CompanyId",
                          o.claim_token AS "ClaimToken",
                          o.retry_count AS "RetryCount",
                          o.max_retries AS "MaxRetries",
                          o.purpose AS "Purpose",
                          o.channel AS "Channel",
                          (c.previous_status = {processing}) AS "Recovered"
                """
            )
            .ToListAsync(ct);

        return claimed.Count == 0 ? null : claimed[0];
    }

    public async Task<bool> MarkSentAsync(Guid id, Guid claimToken, DateTime utcNow, CancellationToken ct = default) =>
        await OwnedClaim(id, claimToken)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(x => x.Status, CommunicationStatus.Sent)
                    .SetProperty(x => x.SentAtUtc, utcNow)
                    .SetProperty(x => x.FailedAtUtc, (DateTime?)null)
                    .SetProperty(x => x.NextAttemptAtUtc, (DateTime?)null)
                    .SetProperty(x => x.LastError, (string?)null)
                    .SetProperty(x => x.FailureCategory, (CommunicationFailureCategory?)null)
                    .SetProperty(x => x.ClaimToken, (Guid?)null)
                    .SetProperty(x => x.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(x => x.ProcessingStartedAtUtc, (DateTime?)null)
                    .SetProperty(x => x.UpdatedAt, utcNow)
                    .SetProperty(x => x.UpdatedBy, SystemActorId),
                ct
            ) == 1;

    public async Task<bool> MarkFailedAsync(
        Guid id,
        Guid claimToken,
        CommunicationFailureOutcome outcome,
        CommunicationFailureCategory category,
        string error,
        DateTime utcNow,
        CancellationToken ct = default
    )
    {
        var safeError = error.Length > CommunicationOutbox.LastErrorMaxLen
            ? error[..CommunicationOutbox.LastErrorMaxLen]
            : error;

        return await OwnedClaim(id, claimToken)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(x => x.Status, outcome.Status)
                    .SetProperty(x => x.RetryCount, outcome.RetryCount)
                    .SetProperty(x => x.NextAttemptAtUtc, outcome.NextAttemptAtUtc)
                    .SetProperty(x => x.FailedAtUtc, utcNow)
                    .SetProperty(x => x.LastError, safeError)
                    .SetProperty(x => x.FailureCategory, category)
                    .SetProperty(x => x.ClaimToken, (Guid?)null)
                    .SetProperty(x => x.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(x => x.ProcessingStartedAtUtc, (DateTime?)null)
                    .SetProperty(x => x.UpdatedAt, utcNow)
                    .SetProperty(x => x.UpdatedBy, SystemActorId),
                ct
            ) == 1;
    }

    /// <summary>Contenido de la comunicación reclamada (para enviarla), solo si el claim sigue siendo propio.</summary>
    public Task<CommunicationOutbox?> LoadOwnedAsync(Guid id, Guid claimToken, CancellationToken ct = default) =>
        OwnedClaim(id, claimToken).AsNoTracking().Include(x => x.Attachments).FirstOrDefaultAsync(ct);

    private IQueryable<CommunicationOutbox> OwnedClaim(Guid id, Guid claimToken) =>
        _db.CommunicationOutbox.Where(x =>
            x.Id == id && x.Status == CommunicationStatus.Processing && x.ClaimToken == claimToken
        );
}

/// <summary>Lo mínimo devuelto por el claim cross-tenant: identidad, scope y contadores.</summary>
public sealed class ClaimedCommunication
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid CompanyId { get; init; }
    public Guid ClaimToken { get; init; }
    public int RetryCount { get; init; }
    public int MaxRetries { get; init; }
    public string Purpose { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;

    /// <summary>True si se recuperó una fila Processing con lease vencido (worker anterior muerto).</summary>
    public bool Recovered { get; init; }
}
