using ERP.Application.Modules.Accounting.Posting;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// PURCHASE-RETURN-ACCOUNTING-NOT-GENERATED-01 — remedio controlado, de una sola vez, para
/// <c>PurchaseReturn</c> ya <c>Authorized</c> ANTES de que <c>MinimalPostingRules</c> tuviera la
/// clave ("Purchases","PurchaseReturn"): esas devoluciones quedaron con Kardex/CxP/SupplierCredit
/// aplicados pero sin <c>JournalEntry</c> — <c>PurchaseReturnAuthorizedPostingTranslator</c> ya
/// corrió en su momento y falló fail-closed ("RULE_NOT_FOUND"), y ese evento de dominio ya se
/// consumió (nunca se vuelve a publicar). Este servicio reconstruye el mismo <see cref="PostingFact"/>
/// desde los campos ya persistidos en <c>PurchaseReturn</c> (AuthorizedVatTotal/AuthorizedIceTotal/
/// AuthorizedIrbpnrTotal/AppliedToPayableAmount/SupplierCreditAmount/CostVarianceTotal/
/// HistoricalCostTotal — el mismo snapshot congelado que el evento habría transportado) y lo pasa
/// por <see cref="IPostingEngine.PostAsync"/>, el mismo pipeline real de producción.
///
/// Alcance deliberadamente angosto: SOLO <c>PurchaseReturn.Status == Authorized</c> (el estado
/// reportado en el bug). Una devolución que además fue <c>Cancelled</c> necesitaría también el
/// asiento reverso de "Purchases"/"PurchaseReturnCancelled" — gap distinto, sin PostingRule propia,
/// registrado aparte, NO remediado aquí (ver docs/decisions o ticket de seguimiento).
///
/// Idempotente por construcción: <see cref="IPostingEngine.PostAsync"/> ya resuelve
/// <see cref="PostingIdempotencyGuard"/> por (SourceModule, FactType, SourceEventId) — correr esto
/// dos veces nunca duplica un asiento ya creado (ni por este remedio ni por una autorización nueva
/// que ya posteó correctamente tras el fix de MinimalPostingRules).
///
/// Dry-run por defecto (<paramref name="apply"/> = false en el comando CLI): reporta qué crearía
/// sin escribir nada — ninguna transacción se abre en modo dry-run. Con <c>apply:true</c>, cada
/// devolución se postea en su propia transacción (aislada: si una falla, las demás no se ven
/// afectadas).
/// </summary>
public sealed partial class PurchaseReturnPostingRemediationService
{
    private readonly ErpDbContext _db;
    private readonly IPostingEngine _postingEngine;
    private readonly ILogger<PurchaseReturnPostingRemediationService> _logger;

    public PurchaseReturnPostingRemediationService(
        ErpDbContext db,
        IPostingEngine postingEngine,
        ILogger<PurchaseReturnPostingRemediationService> logger
    )
    {
        _db = db;
        _postingEngine = postingEngine;
        _logger = logger;
    }

    public async Task<PurchaseReturnPostingRemediationSummary> RunAsync(
        bool apply,
        CancellationToken cancellationToken = default
    )
    {
        var authorizedReturns = await _db
            .PurchaseReturns.IgnoreQueryFilters()
            .Where(r => r.Status == PurchaseReturnStatus.Authorized)
            .Select(r => new
            {
                r.Id,
                r.TenantId,
                r.CompanyId,
                r.ReturnNumber,
                r.AuthorizedAtUtc,
                r.AuthorizedVatTotal,
                r.AuthorizedIceTotal,
                r.AuthorizedIrbpnrTotal,
                r.AppliedToPayableAmount,
                r.SupplierCreditAmount,
                r.CostVarianceTotal,
                r.HistoricalCostTotal,
            })
            .ToListAsync(cancellationToken);

        var rows = new List<PurchaseReturnPostingRemediationRow>();

        foreach (var r in authorizedReturns)
        {
            // Campos nullable en BD (solo se llenan al autorizar) — una fila con alguno en null
            // nunca pasó por Authorize() con éxito y no es candidata a este remedio.
            if (
                r.AuthorizedAtUtc is null
                || r.AppliedToPayableAmount is null
                || r.HistoricalCostTotal is null
            )
                continue;

            using var _ = JobExecutionContext.Begin(r.TenantId, r.CompanyId);

            var alreadyPosted = await _db
                .JournalEntries.IgnoreQueryFilters()
                .AnyAsync(
                    j =>
                        j.CompanyId == r.CompanyId
                        && j.SourceModule == "Purchases"
                        && j.SourceEventType == "PurchaseReturn"
                        && j.SourceEventId == r.Id,
                    cancellationToken
                );

            if (alreadyPosted)
            {
                rows.Add(
                    new PurchaseReturnPostingRemediationRow(
                        r.Id,
                        r.ReturnNumber,
                        r.CompanyId,
                        AlreadyPosted: true,
                        Applied: false,
                        AppliedToPayableAmount: r.AppliedToPayableAmount.Value,
                        HistoricalCostTotal: r.HistoricalCostTotal.Value,
                        Error: null
                    )
                );
                continue;
            }

            var fact = PurchaseReturnPostingFactBuilder.Build(
                r.TenantId,
                r.CompanyId,
                r.Id,
                DateOnly.FromDateTime(r.AuthorizedAtUtc.Value),
                r.AuthorizedVatTotal ?? 0m,
                r.AuthorizedIceTotal ?? 0m,
                r.AuthorizedIrbpnrTotal ?? 0m,
                r.AppliedToPayableAmount.Value,
                r.SupplierCreditAmount ?? 0m,
                r.CostVarianceTotal ?? 0m,
                r.HistoricalCostTotal.Value
            );

            if (!apply)
            {
                rows.Add(
                    new PurchaseReturnPostingRemediationRow(
                        r.Id,
                        r.ReturnNumber,
                        r.CompanyId,
                        AlreadyPosted: false,
                        Applied: false,
                        AppliedToPayableAmount: r.AppliedToPayableAmount.Value,
                        HistoricalCostTotal: r.HistoricalCostTotal.Value,
                        Error: null
                    )
                );
                continue;
            }

            await using var transaction = await _db.Database.BeginTransactionAsync(
                cancellationToken
            );
            try
            {
                var result = await _postingEngine.PostAsync(fact, cancellationToken);
                if (!result.IsSuccess)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    LogRemediationPostingFailed(r.Id, r.ReturnNumber, result.Code, result.Error);
                    rows.Add(
                        new PurchaseReturnPostingRemediationRow(
                            r.Id,
                            r.ReturnNumber,
                            r.CompanyId,
                            AlreadyPosted: false,
                            Applied: false,
                            AppliedToPayableAmount: r.AppliedToPayableAmount.Value,
                            HistoricalCostTotal: r.HistoricalCostTotal.Value,
                            Error: $"{result.Code}: {result.Error}"
                        )
                    );
                    continue;
                }

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                LogRemediationPosted(r.Id, r.ReturnNumber);
                rows.Add(
                    new PurchaseReturnPostingRemediationRow(
                        r.Id,
                        r.ReturnNumber,
                        r.CompanyId,
                        AlreadyPosted: false,
                        Applied: true,
                        AppliedToPayableAmount: r.AppliedToPayableAmount.Value,
                        HistoricalCostTotal: r.HistoricalCostTotal.Value,
                        Error: null
                    )
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                LogRemediationPostingException(ex, r.Id, r.ReturnNumber);
                rows.Add(
                    new PurchaseReturnPostingRemediationRow(
                        r.Id,
                        r.ReturnNumber,
                        r.CompanyId,
                        AlreadyPosted: false,
                        Applied: false,
                        AppliedToPayableAmount: r.AppliedToPayableAmount.Value,
                        HistoricalCostTotal: r.HistoricalCostTotal.Value,
                        Error: ex.Message
                    )
                );
            }
        }

        return new PurchaseReturnPostingRemediationSummary(apply, rows);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[remediate-purchase-return-postings] Asiento creado para PurchaseReturn {PurchaseReturnId} ({ReturnNumber})."
    )]
    private partial void LogRemediationPosted(Guid purchaseReturnId, string? returnNumber);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[remediate-purchase-return-postings] Posting falló para PurchaseReturn {PurchaseReturnId} ({ReturnNumber}): {Code} — {Error}"
    )]
    private partial void LogRemediationPostingFailed(
        Guid purchaseReturnId,
        string? returnNumber,
        string? code,
        string? error
    );

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "[remediate-purchase-return-postings] Excepción al postear PurchaseReturn {PurchaseReturnId} ({ReturnNumber})."
    )]
    private partial void LogRemediationPostingException(
        Exception ex,
        Guid purchaseReturnId,
        string? returnNumber
    );
}

/// <summary>Resultado de una fila procesada por <see cref="PurchaseReturnPostingRemediationService.RunAsync"/>.</summary>
public sealed record PurchaseReturnPostingRemediationRow(
    Guid PurchaseReturnId,
    string? ReturnNumber,
    Guid CompanyId,
    bool AlreadyPosted,
    bool Applied,
    decimal AppliedToPayableAmount,
    decimal HistoricalCostTotal,
    string? Error
);

/// <summary>Resumen impreso por el comando CLI <c>remediate-purchase-return-postings</c>.</summary>
public sealed record PurchaseReturnPostingRemediationSummary(
    bool Applied,
    IReadOnlyList<PurchaseReturnPostingRemediationRow> Rows
)
{
    public int TotalAuthorizedReturns => Rows.Count;
    public int AlreadyPostedCount => Rows.Count(r => r.AlreadyPosted);
    public int MissingCount => Rows.Count(r => !r.AlreadyPosted);
    public int PostedNowCount => Rows.Count(r => r.Applied);
    public int FailedCount => Rows.Count(r => !r.AlreadyPosted && !r.Applied && r.Error is not null);
}
