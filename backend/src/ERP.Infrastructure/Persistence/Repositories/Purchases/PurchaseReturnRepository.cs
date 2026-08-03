using ERP.Application.Common;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Purchases;

/// <summary>Implementación de <see cref="IPurchaseReturnRepository"/> — diseño P0-02 §7.1/§15.1, Fase 2.</summary>
public sealed class PurchaseReturnRepository : IPurchaseReturnRepository
{
    // Namespace de hash independiente de "SalesReturn.Lock"/"SupplierCredit.Lock"/
    // "PurchaseReturn.Sequence"/IJournalEntryRepository.AcquireIdempotencyLockAsync — Lock A del
    // diseño (§15.1), sobre (TenantId, PurchaseInvoiceId).
    private const string LockNamespace = "PurchaseInvoice.FinancialLock";

    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public PurchaseReturnRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    public Task<PurchaseReturn?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db
            .PurchaseReturns.ForOperationalScope(tenantId, _company)
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task AcquireFinancialLockAsync(
        Guid tenantId,
        Guid purchaseInvoiceId,
        CancellationToken ct = default
    )
    {
        // pg_advisory_xact_lock(int4, int4) — ámbito de transacción, se libera automáticamente al
        // COMMIT/ROLLBACK de la transacción ambiente; nunca abre ni comitea una transacción propia
        // (mismo mecanismo que SalesReturnRepository.AcquireReturnLockAsync/DocumentSequenceRepository).
        var hash1 = StableHash(
            System.Text.Encoding.UTF8.GetBytes(LockNamespace).Concat(tenantId.ToByteArray()).ToArray()
        );
        var hash2 = StableHash(purchaseInvoiceId.ToByteArray());
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({hash1}, {hash2})",
            ct
        );
    }

    // Hash estable (no depende de HashCode.GetHashCode, no-determinístico en .NET 5+) — mismo
    // algoritmo que SalesReturnRepository/JournalEntryRepository/DocumentSequenceRepository.
    private static int StableHash(byte[] bytes)
    {
        int h = 17;
        foreach (var b in bytes)
            h = unchecked(h * 31 + b);
        return h;
    }

    public Task AddAsync(PurchaseReturn purchaseReturn, CancellationToken ct = default) =>
        _db.PurchaseReturns.AddAsync(purchaseReturn, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    /// <inheritdoc/>
    public Task<bool> ExistsAuthorizedByPurchaseInvoiceIdAsync(
        Guid tenantId,
        Guid companyId,
        Guid purchaseInvoiceId,
        CancellationToken cancellationToken = default
    ) =>
        _db.PurchaseReturns.AnyAsync(
            x =>
                x.TenantId == tenantId
                && x.CompanyId == companyId
                && x.PurchaseInvoiceId == purchaseInvoiceId
                && x.Status == PurchaseReturnStatus.Authorized,
            cancellationToken
        );

    /// <inheritdoc/>
    public Task<PurchaseReturn?> GetByCreateClientRequestIdAsync(
        Guid tenantId,
        Guid createClientRequestId,
        CancellationToken ct = default
    ) =>
        _db
            .PurchaseReturns.Include(x => x.Lines)
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.CreateClientRequestId == createClientRequestId,
                ct
            );

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, decimal>> GetReturnedQuantitiesByInvoiceDetailIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> invoiceDetailIds,
        CancellationToken ct = default
    )
    {
        if (invoiceDetailIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        var rows = await _db
            .PurchaseReturns.Where(r =>
                r.TenantId == tenantId && r.Status == PurchaseReturnStatus.Authorized
            )
            .SelectMany(r => r.Lines)
            .Where(l => invoiceDetailIds.Contains(l.OriginalInvoiceDetailId))
            .GroupBy(l => l.OriginalInvoiceDetailId)
            .Select(g => new { OriginalInvoiceDetailId = g.Key, Total = g.Sum(l => l.Quantity) })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.OriginalInvoiceDetailId, r => r.Total);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<PurchaseReturn> Items, int Total)> GetPagedAsync(
        Guid tenantId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var q = _db.PurchaseReturns.ForOperationalScope(tenantId, _company);

        if (
            !string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<PurchaseReturnStatus>(status.Trim(), true, out var parsedStatus)
        )
            q = q.Where(x => x.Status == parsedStatus);

        var total = await q.CountAsync(ct);
        var items = await q.Include(x => x.Lines)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    /// <inheritdoc/>
    public Task<Guid?> GetPurchaseInvoiceIdAsync(
        Guid tenantId,
        Guid purchaseReturnId,
        CancellationToken ct = default
    ) =>
        _db
            .PurchaseReturns.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == purchaseReturnId)
            .Select(x => (Guid?)x.PurchaseInvoiceId)
            .FirstOrDefaultAsync(ct);
}
