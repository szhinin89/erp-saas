using ERP.Application.Modules.Communications.Services;
using ERP.Domain.Modules.Communications.Entities;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.Services;
using ERP.Domain.Modules.Communications.ValueObjects;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Communications;

/// <summary>
/// ZH-COMMUNICATIONS-DELIVERY-HARDENING-01 — exclusión y fencing de la entrega en PostgreSQL
/// (ADR-039 D9). Ninguna operación mantiene una transacción abierta durante el envío: el claim es
/// UNA sentencia y cada finalización una transacción corta; SMTP ocurre entre ambos.
/// <list type="bullet">
/// <item><see cref="ClaimNextAsync"/>: <c>UPDATE … FROM (SELECT … FOR UPDATE SKIP LOCKED LIMIT 1)
/// RETURNING</c>. Reclama una fila <c>Pending</c> vencida o una <c>Processing</c> con lease vencido
/// (recuperación de worker muerto), con un <c>ClaimToken</c> nuevo. Un <c>Processing</c> sin lease
/// (filas atascadas antes de este cambio) cuenta como vencido.</item>
/// <item>Finalizaciones: <c>UPDATE … WHERE id AND status = Processing AND claim_token = @token</c>.
/// 0 filas = el claim se perdió (otro worker la recuperó); la fila no se toca.</item>
/// </list>
/// ZH-COMMUNICATIONS-CONTRACT-01 (ADR-039 D11) — historial: la MISMA sentencia del claim cierra como
/// <c>Abandoned</c> el intento abierto de un worker muerto e inicia el intento nuevo (número
/// correlativo); cada finalización cierra su intento por ClaimToken (<c>Sent</c>/<c>Failed</c>, o
/// <c>ClaimLost</c> si perdió el fencing) en la misma transacción que actualiza la comunicación.
/// <para>
/// Alcance: el claim es cross-tenant y devuelve solo lo mínimo para abrir el scope. Las filas Company
/// se leen/finalizan con los filtros globales activos (dentro de <c>JobExecutionContext</c>); las
/// System (sin tenant/empresa) solo por la vía explícita de plataforma, acotada por id + ClaimToken.
/// </para>
/// </summary>
public sealed class CommunicationOutboxDeliveryStore
{
    public const string EmailTransport = "smtp";
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
        var attemptId = Guid.NewGuid();
        var leaseUntil = utcNow + lease;
        var pending = nameof(CommunicationStatus.Pending);
        var processing = nameof(CommunicationStatus.Processing);
        var abandoned = nameof(CommunicationAttemptResult.Abandoned);
        var transport = EmailTransport;

        var claimed = await _db
            .Database.SqlQuery<ClaimedCommunication>(
                $"""
                WITH candidate AS (
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
                ),
                claimed AS (
                    UPDATE communication_outbox AS o
                    SET status = {processing},
                        claim_token = {claimToken},
                        lease_until_utc = {leaseUntil},
                        processing_started_at_utc = {utcNow},
                        retry_count = o.retry_count + CASE WHEN c.previous_status = {processing} THEN 1 ELSE 0 END,
                        updated_at = {utcNow},
                        updated_by = {SystemActorId}
                    FROM candidate AS c
                    WHERE o.id = c.id
                    RETURNING o.id, o.scope_kind, o.tenant_id, o.company_id, o.claim_token, o.retry_count,
                              o.max_retries, o.purpose, o.channel, c.previous_status
                ),
                abandoned_attempt AS (
                    UPDATE communication_delivery_attempts AS a
                    SET result = {abandoned},
                        completed_at_utc = {utcNow},
                        error_safe_text = 'Lease vencido sin finalizar: resultado del transporte desconocido.'
                    FROM claimed
                    WHERE a.communication_id = claimed.id AND a.completed_at_utc IS NULL
                    RETURNING a.id
                ),
                started_attempt AS (
                    INSERT INTO communication_delivery_attempts
                        (id, communication_id, tenant_id, company_id, attempt_number, claim_token, started_at_utc, transport)
                    SELECT {attemptId}, claimed.id, claimed.tenant_id, claimed.company_id,
                           COALESCE((SELECT MAX(x.attempt_number) FROM communication_delivery_attempts AS x
                                     WHERE x.communication_id = claimed.id), 0) + 1,
                           claimed.claim_token, {utcNow}, {transport}
                    FROM claimed
                    RETURNING attempt_number
                )
                SELECT claimed.id AS "Id",
                       claimed.scope_kind AS "ScopeKind",
                       claimed.tenant_id AS "TenantId",
                       claimed.company_id AS "CompanyId",
                       claimed.claim_token AS "ClaimToken",
                       claimed.retry_count AS "RetryCount",
                       claimed.max_retries AS "MaxRetries",
                       claimed.purpose AS "Purpose",
                       claimed.channel AS "Channel",
                       (claimed.previous_status = {processing}) AS "Recovered",
                       (SELECT attempt_number FROM started_attempt) AS "AttemptNumber"
                FROM claimed
                """
            )
            .ToListAsync(ct);

        return claimed.Count == 0 ? null : claimed[0];
    }

    public Task<bool> MarkSentAsync(
        ClaimedCommunication claim,
        EmailDeliveryReceipt receipt,
        DateTime utcNow,
        CancellationToken ct = default
    ) =>
        FinalizeAsync(
            claim,
            outbox => outbox.ExecuteUpdateAsync(
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
            ),
            owned: new AttemptCompletion(
                CommunicationAttemptResult.Sent,
                FailureCategory: null,
                ProviderCode: null,
                Truncate(receipt.ProviderMessageId, CommunicationDeliveryAttempt.ProviderMessageIdMaxLen),
                ErrorSafeText: null
            ),
            lostDetail: "Transporte aceptado (Sent) después de perder el claim; no modificó la comunicación.",
            utcNow,
            ct
        );

    public Task<bool> MarkFailedAsync(
        ClaimedCommunication claim,
        CommunicationFailureOutcome outcome,
        CommunicationFailureCategory category,
        string errorSafeText,
        string? providerCode,
        DateTime utcNow,
        CancellationToken ct = default
    )
    {
        var outboxError = Truncate(errorSafeText, CommunicationOutbox.LastErrorMaxLen);
        return FinalizeAsync(
            claim,
            outbox => outbox.ExecuteUpdateAsync(
                s => s
                    .SetProperty(x => x.Status, outcome.Status)
                    .SetProperty(x => x.RetryCount, outcome.RetryCount)
                    .SetProperty(x => x.NextAttemptAtUtc, outcome.NextAttemptAtUtc)
                    .SetProperty(x => x.FailedAtUtc, utcNow)
                    .SetProperty(x => x.LastError, outboxError)
                    .SetProperty(x => x.FailureCategory, category)
                    .SetProperty(x => x.ClaimToken, (Guid?)null)
                    .SetProperty(x => x.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(x => x.ProcessingStartedAtUtc, (DateTime?)null)
                    .SetProperty(x => x.UpdatedAt, utcNow)
                    .SetProperty(x => x.UpdatedBy, SystemActorId),
                ct
            ),
            owned: new AttemptCompletion(
                CommunicationAttemptResult.Failed,
                category,
                Truncate(providerCode, CommunicationDeliveryAttempt.ProviderCodeMaxLen),
                ProviderMessageId: null,
                Truncate(errorSafeText, CommunicationDeliveryAttempt.ErrorSafeTextMaxLen)
            ),
            lostDetail: Truncate(
                $"Transporte fallido ({category}) después de perder el claim; no modificó la comunicación.",
                CommunicationDeliveryAttempt.ErrorSafeTextMaxLen
            )!,
            utcNow,
            ct
        );
    }

    /// <summary>Contenido de la comunicación reclamada (para enviarla), solo si el claim sigue siendo propio.</summary>
    public Task<CommunicationOutbox?> LoadOwnedAsync(ClaimedCommunication claim, CancellationToken ct = default) =>
        OwnedClaim(claim).AsNoTracking().Include(x => x.Attachments).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Actualiza la comunicación con fencing y cierra su intento en la misma transacción corta: el
    /// intento propio queda Sent/Failed; si el fencing falló, ClaimLost (sin tocar la comunicación).
    /// </summary>
    private async Task<bool> FinalizeAsync(
        ClaimedCommunication claim,
        Func<IQueryable<CommunicationOutbox>, Task<int>> updateOutbox,
        AttemptCompletion owned,
        string lostDetail,
        DateTime utcNow,
        CancellationToken ct
    )
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            var stillOwned = await updateOutbox(OwnedClaim(claim)) == 1;
            var completion = stillOwned
                ? owned
                : new AttemptCompletion(CommunicationAttemptResult.ClaimLost, null, null, null, lostDetail);

            await Attempts(claim)
                .Where(a => a.ClaimToken == claim.ClaimToken)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(a => a.Result, completion.Result)
                        .SetProperty(a => a.CompletedAtUtc, utcNow)
                        .SetProperty(a => a.FailureCategory, completion.FailureCategory)
                        .SetProperty(a => a.ProviderCode, completion.ProviderCode)
                        .SetProperty(a => a.ProviderMessageId, completion.ProviderMessageId)
                        .SetProperty(a => a.ErrorSafeText, completion.ErrorSafeText),
                    ct
                );

            await transaction.CommitAsync(ct);
            return stillOwned;
        });
    }

    private IQueryable<CommunicationOutbox> OwnedClaim(ClaimedCommunication claim) =>
        Outbox(claim).Where(x =>
            x.Id == claim.Id && x.Status == CommunicationStatus.Processing && x.ClaimToken == claim.ClaimToken
        );

    // Company: filtros globales activos (el caller abrió JobExecutionContext de la fila).
    // System: no hay contexto de tenant que lo vuelva visible; vía explícita de plataforma, acotada
    // siempre por id + ClaimToken (secreto del claim vigente).
    private IQueryable<CommunicationOutbox> Outbox(ClaimedCommunication claim) =>
        claim.GetScope().Kind == CommunicationScopeKind.System
            ? _db.CommunicationOutbox.AsPlatformQuery()
            : _db.CommunicationOutbox;

    private IQueryable<CommunicationDeliveryAttempt> Attempts(ClaimedCommunication claim) =>
        claim.GetScope().Kind == CommunicationScopeKind.System
            ? _db.CommunicationDeliveryAttempts.AsPlatformQuery()
            : _db.CommunicationDeliveryAttempts;

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    private sealed record AttemptCompletion(
        CommunicationAttemptResult Result,
        CommunicationFailureCategory? FailureCategory,
        string? ProviderCode,
        string? ProviderMessageId,
        string? ErrorSafeText
    );
}

/// <summary>Lo mínimo devuelto por el claim cross-tenant: identidad, alcance, intento y contadores.</summary>
public sealed class ClaimedCommunication
{
    public Guid Id { get; init; }
    public string ScopeKind { get; init; } = string.Empty;
    public Guid? TenantId { get; init; }
    public Guid? CompanyId { get; init; }
    public Guid ClaimToken { get; init; }
    public int RetryCount { get; init; }
    public int MaxRetries { get; init; }
    public string Purpose { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;

    /// <summary>True si se recuperó una fila Processing con lease vencido (worker anterior muerto).</summary>
    public bool Recovered { get; init; }

    /// <summary>Número del intento abierto por este claim (1, 2, …).</summary>
    public int AttemptNumber { get; init; }

    public CommunicationScope GetScope() =>
        CommunicationScope.From(Enum.Parse<CommunicationScopeKind>(ScopeKind), TenantId, CompanyId, branchId: null);
}
