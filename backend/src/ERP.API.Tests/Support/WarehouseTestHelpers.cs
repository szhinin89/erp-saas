using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.API.Tests.Support;

internal static class WarehouseTestHelpers
{
    internal static Warehouse CreateSecondary(
        Guid tenantId,
        Guid branchId,
        string name,
        string code,
        Guid createdBy,
        Guid companyId)
        => Warehouse.Create(
            tenantId,
            branchId,
            name,
            code,
            storageType: null,
            address: null,
            phone: null,
            email: null,
            manager: null,
            latitude: null,
            longitude: null,
            capacity: null,
            dailyDispatchGoal: null,
            createdBy: createdBy,
            companyId: companyId);
}
