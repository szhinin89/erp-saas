using ERP.Application.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class WarehouseRepository : IWarehouseRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentCompany _company;

    public WarehouseRepository(ErpDbContext context, ICurrentCompany company)
    {
        _context = context;
        _company = company;
    }

    private IQueryable<Warehouse> Scoped(Guid tenantId) =>
        _context.Warehouses.ForOperationalScope(tenantId, _company);

    public Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default) =>
        _context.Warehouses.AddAsync(warehouse, cancellationToken).AsTask();

    public Task<Warehouse?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default
    ) => Scoped(tenantId).FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<Warehouse?> GetByIdForCompanyAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default
    ) =>
        Scoped(tenantId)
            .Where(w => w.CompanyId == companyId)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task<Warehouse?> GetByIdForCompanyForUpdateAsync(
        Guid tenantId,
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        // Patrón oficial (StockTransferRepository.GetByIdForUpdateAsync): lock → lectura acotada → Reload.
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM warehouses WHERE tenant_id = {tenantId} AND company_id = {companyId} AND id = {id} FOR UPDATE",
            cancellationToken
        );
        var warehouse = await GetByIdForCompanyAsync(tenantId, companyId, id, cancellationToken);
        if (warehouse is not null)
            await _context.Entry(warehouse).ReloadAsync(cancellationToken);
        return warehouse;
    }

    public async Task<bool> ExistsCodeAsync(
        Guid tenantId,
        Guid branchId,
        string code,
        Guid? excludeId,
        CancellationToken cancellationToken = default
    )
    {
        var q = Scoped(tenantId).Where(w => w.BranchId == branchId && w.Code == code.Trim());
        if (excludeId.HasValue)
            q = q.Where(w => w.Id != excludeId.Value);
        return await q.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Warehouse>> GetAsync(
        Guid tenantId,
        bool? activeFilter,
        string? search,
        Guid? branchId,
        CancellationToken cancellationToken = default
    )
    {
        var q = Scoped(tenantId);

        if (activeFilter is true)
            q = q.Where(b => b.IsActive);
        else if (activeFilter is false)
            q = q.Where(b => !b.IsActive);

        if (branchId.HasValue)
            q = q.Where(b => b.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(b =>
                b.Name.Contains(s)
                || (b.Code != null && b.Code.Contains(s))
                || (b.Address != null && b.Address.Contains(s))
                || (b.Manager != null && b.Manager.Contains(s))
            );
        }

        return await q.OrderBy(b => b.Name).ToListAsync(cancellationToken);
    }

    public Task<Warehouse?> GetMainForBranchAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken = default
    ) =>
        Scoped(tenantId)
            .Where(w => w.BranchId == branchId && w.IsMain && w.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<WarehouseBranchChangeBlocker> GetBranchChangeBlockerAsync(
        Guid tenantId,
        Guid companyId,
        Guid warehouseId,
        CancellationToken cancellationToken = default
    )
    {
        var id = warehouseId;
        if (
            await _context.CurrentStocks.AnyAsync(
                s => s.TenantId == tenantId && s.CompanyId == companyId && s.WarehouseId == id && s.Quantity != 0,
                cancellationToken
            )
        )
            return WarehouseBranchChangeBlocker.Stock;

        if (
            await _context.StockMovements.AnyAsync(
                m => m.TenantId == tenantId && m.WarehouseId == id,
                cancellationToken
            )
        )
            return WarehouseBranchChangeBlocker.Movements;

        if (
            await _context.Lots.AnyAsync(l => l.TenantId == tenantId && l.WarehouseId == id, cancellationToken)
            || await _context.SerialNumbers.AnyAsync(
                s => s.TenantId == tenantId && s.WarehouseId == id,
                cancellationToken
            )
        )
            return WarehouseBranchChangeBlocker.LotsSerialsOrReservations;

        if (await HasDocumentReferencesAsync(tenantId, id, cancellationToken))
            return WarehouseBranchChangeBlocker.Documents;

        var idText = id.ToString();
        if (
            await _context.OrgSettings.AnyAsync(
                s => s.TenantId == tenantId && s.CompanyId == companyId && s.Value == idText,
                cancellationToken
            )
        )
            return WarehouseBranchChangeBlocker.DefaultSetting;

        if (
            await _context.CashRegisters.AnyAsync(
                c => c.TenantId == tenantId && c.DefaultWarehouseId == id,
                cancellationToken
            )
        )
            return WarehouseBranchChangeBlocker.CashRegisterDefault;

        return WarehouseBranchChangeBlocker.None;
    }

    /// <summary>Documentos (incluidos borradores) y asignaciones de costo que nombran la bodega.</summary>
    private async Task<bool> HasDocumentReferencesAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        await _context.StockTransfers.AnyAsync(
            t => t.TenantId == tenantId && (t.SourceWarehouseId == id || t.TargetWarehouseId == id),
            ct
        )
        || await _context.StockAdjustments.AnyAsync(a => a.TenantId == tenantId && a.WarehouseId == id, ct)
        || await _context.PurchaseInvoices.AnyAsync(p => p.TenantId == tenantId && p.GlobalWarehouseId == id, ct)
        || await _context.PurchaseInvoiceDetails.AnyAsync(d => d.WarehouseId == id, ct)
        || await _context.PurchaseReturnDetails.AnyAsync(d => d.WarehouseId == id, ct)
        || await _context.SalesInvoiceDetails.AnyAsync(d => d.WarehouseId == id, ct)
        || await _context.SalesReturnDetails.AnyAsync(d => d.WarehouseId == id, ct)
        || await _context.Set<SaleCostObligation>().AnyAsync(o => o.TenantId == tenantId && o.WarehouseId == id, ct)
        || await _context.Set<SaleCostAllocation>().AnyAsync(a => a.TenantId == tenantId && a.WarehouseId == id, ct);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
