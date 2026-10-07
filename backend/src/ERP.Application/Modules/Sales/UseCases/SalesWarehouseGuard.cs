using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Sales.UseCases;

internal static class SalesWarehouseGuard
{
    public const string Error = "La bodega no pertenece al tenant, empresa y sucursal de la venta.";

    public static async Task<bool> AreValidAsync(
        IEnumerable<Guid?> warehouseIds,
        Guid tenantId,
        Guid companyId,
        Guid branchId,
        IWarehouseRepository warehouses,
        CancellationToken ct)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty || branchId == Guid.Empty)
            return false;
        foreach (var id in warehouseIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct())
        {
            if (id == Guid.Empty) return false;
            var warehouse = await warehouses.GetByIdForCompanyAsync(tenantId, companyId, id, ct);
            if (warehouse is null || warehouse.Id != id || warehouse.TenantId != tenantId
                || warehouse.CompanyId != companyId || warehouse.BranchId != branchId)
                return false;
        }
        return true;
    }
}
