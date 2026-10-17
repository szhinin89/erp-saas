using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Sales.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Inventory;

public sealed class InventoryCostLedger(ErpDbContext db) : IInventoryCostLedger
{
    public async Task<IReadOnlyList<PendingSaleCostView>> GetPendingObligationsAsync(Guid tenantId, Guid companyId, CancellationToken ct) =>
        await (from o in db.Set<SaleCostObligation>()
                where o.TenantId == tenantId && o.CompanyId == companyId && o.PendingQuantity > 0m
                join i in db.Set<SalesInvoice>().Where(i => i.TenantId == tenantId && i.CompanyId == companyId)
                    on o.InvoiceId equals i.Id into invoices
                from i in invoices.DefaultIfEmpty()
                join it in db.Set<Item>().Where(it => it.TenantId == tenantId && it.CompanyId == companyId)
                    on o.ProductId equals it.Id into items
                from it in items.DefaultIfEmpty()
                join w in db.Set<Warehouse>().Where(w => w.TenantId == tenantId && w.CompanyId == companyId)
                    on o.WarehouseId equals w.Id into warehouses
                from w in warehouses.DefaultIfEmpty()
                orderby o.SequenceNumber
                select new PendingSaleCostView(o.Id, o.InvoiceId, o.InvoiceLineId,
                    i == null ? null : i.InvoiceNumber, i == null ? null : (DateOnly?)i.IssueDate,
                    o.ProductId, it == null ? null : it.Code.SKU, it == null ? null : it.Code.ShortName,
                    o.WarehouseId, w == null ? null : w.Name, o.PendingQuantity, o.ProvisionalUnitCost))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DateOnly>> GetPendingObligationInvoiceDatesAsync(Guid tenantId, Guid companyId, CancellationToken ct) =>
        await db.Set<SaleCostObligation>()
            .Where(o => o.TenantId == tenantId && o.CompanyId == companyId && o.PendingQuantity > 0m)
            .Join(db.Set<SalesInvoice>().Where(i => i.TenantId == tenantId && i.CompanyId == companyId),
                o => o.InvoiceId, i => i.Id, (o, i) => new { i.Id, i.IssueDate })
            .Distinct().Select(x => x.IssueDate).ToListAsync(ct);

    /// <summary>Serializa la valoración del costo de una factura; requiere la transacción del llamador.</summary>
    public async Task LockInvoiceAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) return;
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("El lock de costo de ventas requiere una transacción activa.");
        var key = $"inventory-cost:{tenantId}:{companyId}:{invoiceId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
        foreach (var entry in db.ChangeTracker.Entries<SaleCostObligation>().Where(e =>
            e.State == EntityState.Unchanged && e.Entity.TenantId == tenantId
            && e.Entity.CompanyId == companyId && e.Entity.InvoiceId == invoiceId).ToList())
            await entry.ReloadAsync(ct);
    }

    public async Task<decimal?> GetCurrentInvoiceCostAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct)
    {
        var persisted = await db.Set<SaleCostObligation>().Where(o => o.TenantId == tenantId
            && o.CompanyId == companyId && o.InvoiceId == invoiceId).ToListAsync(ct);
        var costs = persisted.Concat(db.Set<SaleCostObligation>().Local).Where(o => o.TenantId == tenantId
            && o.CompanyId == companyId && o.InvoiceId == invoiceId).DistinctBy(o => o.Id).ToList();
        return costs.Count == 0 ? null : costs.Sum(o => o.ResolvedCost + o.PendingQuantity * (o.ProvisionalUnitCost ?? 0m));
    }
}
