using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Accounting.Continuity;
using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// Remediación MANUAL de devoluciones de compra Authorized sin asiento ni obligación contable
/// (ACCOUNTING-CONTINUITY-04). Converge al mecanismo durable común: registra la obligación
/// Purchases/PurchaseReturn con <see cref="AccountingPostingRecorder"/> (mismo hecho compuesto que la
/// autorización, desde los snapshots persistidos) y ejecuta su intento con
/// <see cref="AccountingPostingExecutor"/>. Nunca publica directamente con el motor.
/// </summary>
/// <remarks>
/// Solo procesa los IDs indicados explícitamente: sin IDs no recorre ni reconstruye el histórico.
/// Dry-run por defecto. Una devolución con asiento se informa sin cambios; con obligación ya
/// existente (Pending/Failed) se informa su estado y se resuelve con el reproceso manual de
/// Accounting, no aquí. La fecha del hecho es la fecha local de la autorización; si Accounting no
/// aplica en esa fecha no se crea obligación. Cada devolución usa su propia transacción.
/// </remarks>
public sealed partial class PurchaseReturnPostingRemediationService
{
    private readonly ErpDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AccountingPostingRecorder _recorder;
    private readonly IAccountingPostingDispatcher _dispatcher;
    private readonly ICompanyClock _companyClock;
    private readonly ILogger<PurchaseReturnPostingRemediationService> _logger;

    public PurchaseReturnPostingRemediationService(
        ErpDbContext db,
        IUnitOfWork unitOfWork,
        AccountingPostingRecorder recorder,
        IAccountingPostingDispatcher dispatcher,
        ICompanyClock companyClock,
        ILogger<PurchaseReturnPostingRemediationService> logger
    )
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _recorder = recorder;
        _dispatcher = dispatcher;
        _companyClock = companyClock;
        _logger = logger;
    }

    public async Task<PurchaseReturnPostingRemediationSummary> RunAsync(
        bool apply,
        IReadOnlyCollection<Guid> purchaseReturnIds,
        CancellationToken cancellationToken = default
    )
    {
        var rows = new List<PurchaseReturnPostingRemediationRow>();
        if (purchaseReturnIds.Count == 0)
            return new PurchaseReturnPostingRemediationSummary(apply, rows);

        var returns = await _db
            .PurchaseReturns.AsPlatformQuery()
            .Where(r => purchaseReturnIds.Contains(r.Id))
            .Select(r => new
            {
                r.Id,
                r.TenantId,
                r.CompanyId,
                r.Status,
                r.ReturnNumber,
                r.PurchaseInvoiceId,
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

        foreach (var id in purchaseReturnIds.Distinct())
        {
            var r = returns.SingleOrDefault(x => x.Id == id);
            if (r is null)
            {
                rows.Add(new(id, null, null, PurchaseReturnRemediationState.NotFound, null));
                continue;
            }
            if (
                r.Status != PurchaseReturnStatus.Authorized
                || r.AuthorizedAtUtc is null
                || r.AppliedToPayableAmount is null
                || r.HistoricalCostTotal is null
            )
            {
                rows.Add(new(r.Id, r.ReturnNumber, r.CompanyId, PurchaseReturnRemediationState.NotEligible, null));
                continue;
            }

            using var _ = JobExecutionContext.Begin(r.TenantId, r.CompanyId);

            var posted = await _db.JournalEntries.AnyAsync(
                j =>
                    j.TenantId == r.TenantId
                    && j.CompanyId == r.CompanyId
                    && j.SourceModule == PurchaseReturnPosting.SourceModuleName
                    && j.SourceEventType == PurchaseReturnPosting.FactTypeName
                    && j.SourceEventId == r.Id,
                cancellationToken
            );
            if (posted)
            {
                rows.Add(new(r.Id, r.ReturnNumber, r.CompanyId, PurchaseReturnRemediationState.AlreadyPosted, null));
                continue;
            }

            var existing = await ExistingPostingAsync(r.TenantId, r.CompanyId, r.Id, cancellationToken);
            if (existing is not null)
            {
                rows.Add(new(r.Id, r.ReturnNumber, r.CompanyId, PurchaseReturnRemediationState.ObligationExists, existing.ToString()));
                continue;
            }

            if (!apply)
            {
                rows.Add(new(r.Id, r.ReturnNumber, r.CompanyId, PurchaseReturnRemediationState.WouldRecord, null));
                continue;
            }

            var entryDate = await _companyClock.LocalDateAsync(r.CompanyId, r.TenantId, r.AuthorizedAtUtc.Value, cancellationToken);
            var request = PurchaseReturnPosting.Request(
                r.TenantId,
                r.CompanyId,
                r.Id,
                r.ReturnNumber,
                entryDate,
                r.AuthorizedVatTotal ?? 0m,
                r.AuthorizedIceTotal ?? 0m,
                r.AuthorizedIrbpnrTotal ?? 0m,
                r.AppliedToPayableAmount.Value,
                r.SupplierCreditAmount ?? 0m,
                r.CostVarianceTotal ?? 0m,
                r.HistoricalCostTotal.Value,
                r.PurchaseInvoiceId,
                supplierCreditId: null
            );

            try
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                await _recorder.RecordAsync(request, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync(CancellationToken.None);
                _unitOfWork.ClearChangeTracker();
                LogRecordFailed(ex, r.Id, r.ReturnNumber);
                rows.Add(new(r.Id, r.ReturnNumber, r.CompanyId, PurchaseReturnRemediationState.Error, ex.Message));
                continue;
            }

            // Intento inicial con el ejecutor común (configuración contable vigente).
            await _dispatcher.DispatchPendingAsync(cancellationToken);
            var result = await ExistingPostingAsync(r.TenantId, r.CompanyId, r.Id, cancellationToken);
            rows.Add(
                new(
                    r.Id,
                    r.ReturnNumber,
                    r.CompanyId,
                    result is null ? PurchaseReturnRemediationState.NotApplicable : PurchaseReturnRemediationState.Recorded,
                    result?.ToString()
                )
            );
        }

        return new PurchaseReturnPostingRemediationSummary(apply, rows);
    }

    private Task<AccountingPostingStatus?> ExistingPostingAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct) =>
        _db
            .AccountingPostings.AsNoTracking()
            .Where(p =>
                p.TenantId == tenantId
                && p.CompanyId == companyId
                && p.SourceModule == PurchaseReturnPosting.SourceModuleName
                && p.FactType == PurchaseReturnPosting.FactTypeName
                && p.SourceEventId == id
            )
            .Select(p => (AccountingPostingStatus?)p.Status)
            .SingleOrDefaultAsync(ct);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "[remediate-purchase-return-postings] No se pudo registrar la obligación de PurchaseReturn {PurchaseReturnId} ({ReturnNumber})."
    )]
    private partial void LogRecordFailed(Exception ex, Guid purchaseReturnId, string? returnNumber);
}

public enum PurchaseReturnRemediationState
{
    NotFound,
    NotEligible,
    AlreadyPosted,
    ObligationExists,
    WouldRecord,
    Recorded,
    NotApplicable,
    Error,
}

/// <summary>Resultado de una devolución indicada a <see cref="PurchaseReturnPostingRemediationService.RunAsync"/>.</summary>
public sealed record PurchaseReturnPostingRemediationRow(
    Guid PurchaseReturnId,
    string? ReturnNumber,
    Guid? CompanyId,
    PurchaseReturnRemediationState State,
    string? Detail
);

/// <summary>Resumen impreso por el comando CLI <c>remediate-purchase-return-postings</c>.</summary>
public sealed record PurchaseReturnPostingRemediationSummary(
    bool Applied,
    IReadOnlyList<PurchaseReturnPostingRemediationRow> Rows
);
