using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Inventory;

public sealed class InventoryCostLedger(ErpDbContext db) : IInventoryCostLedger
{
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _ownedTransaction;
    public async Task<IReadOnlyList<InventoryCostPosting>> GetInvoicePostingsAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct)
    {
        var persisted = await db.Set<InventoryCostPosting>().Where(p => p.TenantId == tenantId
            && p.CompanyId == companyId && p.InvoiceId == invoiceId).ToListAsync(ct);
        return persisted.Concat(db.Set<InventoryCostPosting>().Local).Where(p => p.TenantId == tenantId
            && p.CompanyId == companyId && p.InvoiceId == invoiceId).DistinctBy(p => p.Id).ToList();
    }
    public async Task<IReadOnlyList<InventoryCostPosting>> GetPendingPostingsAsync(Guid tenantId, Guid companyId, CancellationToken ct) =>
        await db.Set<InventoryCostPosting>().Where(p => p.TenantId == tenantId && p.CompanyId == companyId
            && (p.Status == "Pending" || p.Status == "Failed")).ToListAsync(ct);
    public async Task<IReadOnlyList<SaleCostObligation>> GetPendingObligationsAsync(Guid tenantId, Guid companyId, CancellationToken ct) =>
        await db.Set<SaleCostObligation>().Where(p => p.TenantId == tenantId && p.CompanyId == companyId
            && p.PendingQuantity > 0m).ToListAsync(ct);
    public async Task LockInvoiceAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) return;
        if (db.Database.CurrentTransaction is null)
            _ownedTransaction = await db.Database.BeginTransactionAsync(ct);
        var key = $"inventory-cost:{tenantId}:{companyId}:{invoiceId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
        foreach (var entry in db.ChangeTracker.Entries<InventoryCostPosting>().Where(e =>
            e.State == EntityState.Unchanged && e.Entity.TenantId == tenantId
            && e.Entity.CompanyId == companyId && e.Entity.InvoiceId == invoiceId).ToList())
            await entry.ReloadAsync(ct);
        foreach (var entry in db.ChangeTracker.Entries<SaleCostObligation>().Where(e =>
            e.State == EntityState.Unchanged && e.Entity.TenantId == tenantId
            && e.Entity.CompanyId == companyId && e.Entity.InvoiceId == invoiceId).ToList())
            await entry.ReloadAsync(ct);
    }
    public void Add(InventoryCostPosting posting) => db.Set<InventoryCostPosting>().Add(posting);
    public async Task<decimal?> GetCurrentInvoiceCostAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct)
    {
        var persisted = await db.Set<SaleCostObligation>().Where(o => o.TenantId == tenantId
            && o.CompanyId == companyId && o.InvoiceId == invoiceId).ToListAsync(ct);
        var costs = persisted.Concat(db.Set<SaleCostObligation>().Local).Where(o => o.TenantId == tenantId
            && o.CompanyId == companyId && o.InvoiceId == invoiceId).DistinctBy(o => o.Id).ToList();
        return costs.Count == 0 ? null : costs.Sum(o => o.ResolvedCost + o.PendingQuantity * (o.ProvisionalUnitCost ?? 0m));
    }
    public async Task<int> SaveAsync(CancellationToken ct)
    {
        try
        {
            var result = await db.SaveChangesAsync(ct);
            if (_ownedTransaction is not null) await _ownedTransaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            if (_ownedTransaction is not null) await _ownedTransaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (_ownedTransaction is not null) await _ownedTransaction.DisposeAsync();
            _ownedTransaction = null;
        }
    }
}
