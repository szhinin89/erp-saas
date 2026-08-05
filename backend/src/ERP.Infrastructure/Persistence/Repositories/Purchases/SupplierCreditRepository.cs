using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.Entities;
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

    public async Task<(IReadOnlyList<SupplierCredit> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = _db
            .SupplierCredits.ForOperationalScope(tenantId, _company)
            .Include(x => x.Movements);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }
}
