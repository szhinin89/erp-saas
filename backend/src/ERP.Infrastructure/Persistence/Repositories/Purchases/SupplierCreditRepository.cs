using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Purchases;

/// <summary>Implementación de <see cref="ISupplierCreditRepository"/> — diseño P0-02 §7.4/§15.1, Fase 2.</summary>
public sealed class SupplierCreditRepository : ISupplierCreditRepository
{
    // Namespace de hash independiente de "PurchaseInvoice.FinancialLock"/"SalesReturn.Lock"/
    // "PurchaseReturn.Sequence"/IJournalEntryRepository.AcquireIdempotencyLockAsync — Lock B del
    // diseño (§15.1), sobre (TenantId, SupplierCreditId), siempre adquirido después de Lock A
    // cuando ambos participan en la misma operación (§15.4).
    private const string LockNamespace = "SupplierCredit.Lock";

    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public SupplierCreditRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    public Task<SupplierCredit?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db
            .SupplierCredits.ForOperationalScope(tenantId, _company)
            .Include(x => x.Movements)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<SupplierCredit?> GetBySourcePurchaseReturnIdAsync(
        Guid tenantId,
        Guid sourcePurchaseReturnId,
        CancellationToken ct = default
    ) =>
        _db
            .SupplierCredits.ForOperationalScope(tenantId, _company)
            .Include(x => x.Movements)
            .FirstOrDefaultAsync(x => x.SourcePurchaseReturnId == sourcePurchaseReturnId, ct);

    public async Task AcquireLockAsync(
        Guid tenantId,
        Guid supplierCreditId,
        CancellationToken ct = default
    )
    {
        // pg_advisory_xact_lock(int4, int4) — ámbito de transacción, se libera automáticamente al
        // COMMIT/ROLLBACK de la transacción ambiente; nunca abre ni comitea una transacción propia.
        var hash1 = StableHash(
            System
                .Text.Encoding.UTF8.GetBytes(LockNamespace)
                .Concat(tenantId.ToByteArray())
                .ToArray()
        );
        var hash2 = StableHash(supplierCreditId.ToByteArray());
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({hash1}, {hash2})",
            ct
        );
    }

    private static int StableHash(byte[] bytes)
    {
        int h = 17;
        foreach (var b in bytes)
            h = unchecked(h * 31 + b);
        return h;
    }

    public Task AddAsync(SupplierCredit supplierCredit, CancellationToken ct = default) =>
        _db.SupplierCredits.AddAsync(supplierCredit, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public Task<Guid?> GetIdBySourcePurchaseReturnIdAsync(
        Guid tenantId,
        Guid sourcePurchaseReturnId,
        CancellationToken ct = default
    ) =>
        _db
            .SupplierCredits.ForOperationalScope(tenantId, _company)
            .AsNoTracking()
            .Where(x => x.SourcePurchaseReturnId == sourcePurchaseReturnId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

    public Task<Guid?> GetIdBySourceSupplierPaymentIdAsync(
        Guid tenantId,
        Guid sourceSupplierPaymentId,
        CancellationToken ct = default
    ) =>
        _db
            .SupplierCredits.ForOperationalScope(tenantId, _company)
            .AsNoTracking()
            .Where(x => x.SourceSupplierPaymentId == sourceSupplierPaymentId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<
        IReadOnlyDictionary<Guid, SupplierCreditSourceDocument>
    > GetSourceDocumentsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> supplierCreditIds,
        CancellationToken ct = default
    )
    {
        if (supplierCreditIds.Count == 0)
            return new Dictionary<Guid, SupplierCreditSourceDocument>();

        var credits = _db
            .SupplierCredits.ForOperationalScope(tenantId, _company)
            .AsNoTracking()
            .Where(c => supplierCreditIds.Contains(c.Id));

        var fromReturns = await (
            from c in credits
            join r in _db.PurchaseReturns.AsNoTracking() on c.SourcePurchaseReturnId equals r.Id
            where r.TenantId == tenantId
            select new
            {
                c.Id,
                DocumentId = r.Id,
                r.ReturnNumber,
                r.AuthorizedAtUtc,
            }
        ).ToListAsync(ct);

        var fromPayments = await (
            from c in credits
            join p in _db.SupplierPayments.AsNoTracking() on c.SourceSupplierPaymentId equals p.Id
            where p.TenantId == tenantId
            select new
            {
                c.Id,
                DocumentId = p.Id,
                p.SystemNumber,
                p.PaymentDate,
            }
        ).ToListAsync(ct);

        return fromReturns
            .Select(x =>
                (
                    x.Id,
                    Doc: new SupplierCreditSourceDocument(
                        x.DocumentId,
                        x.ReturnNumber,
                        null,
                        x.AuthorizedAtUtc
                    )
                )
            )
            .Concat(
                fromPayments.Select(x =>
                    (
                        x.Id,
                        Doc: new SupplierCreditSourceDocument(
                            x.DocumentId,
                            x.SystemNumber,
                            (DateOnly?)x.PaymentDate,
                            null
                        )
                    )
                )
            )
            .ToDictionary(x => x.Id, x => x.Doc);
    }

    public async Task<SupplierCreditOpenBalance> GetOpenBalanceBySupplierAsync(
        Guid tenantId,
        Guid supplierId,
        CancellationToken ct = default
    )
    {
        var open = _db
            .SupplierCredits.ForOperationalScope(tenantId, _company)
            .AsNoTracking()
            .Where(x => x.SupplierId == supplierId && x.AvailableAmount > 0);
        var aggregate = await open.GroupBy(_ => 1)
            .Select(g => new { Available = g.Sum(x => x.AvailableAmount), Count = g.Count() })
            .FirstOrDefaultAsync(ct);
        if (aggregate is null)
            return new SupplierCreditOpenBalance(0m, 0, null);

        Guid? singleId =
            aggregate.Count == 1
                ? await open.Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct)
                : null;
        return new SupplierCreditOpenBalance(aggregate.Available, aggregate.Count, singleId);
    }

    public async Task<(IReadOnlyList<SupplierCredit> Items, int Total)> SearchAsync(
        Guid tenantId,
        SupplierCreditSearchCriteria criteria,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = _db.SupplierCredits.ForOperationalScope(tenantId, _company).AsNoTracking();
        if (criteria.SupplierId is { } supplierId)
            query = query.Where(x => x.SupplierId == supplierId);
        if (criteria.SourceType == SupplierCreditSourceType.SupplierPayment)
            query = query.Where(x => x.SourceSupplierPaymentId != null);
        else if (criteria.SourceType == SupplierCreditSourceType.PurchaseReturn)
            query = query.Where(x => x.SourcePurchaseReturnId != null);
        if (criteria.IsOpen is { } isOpen)
            query = isOpen
                ? query.Where(x => x.AvailableAmount > 0)
                : query.Where(x => x.AvailableAmount <= 0);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }
}
