using System.Reflection;
using ERP.API.Controllers;
using ERP.Domain.Kernel.Attributes;
using ERP.Domain.Kernel.Modules;
using ERP.Domain.Kernel.Permissions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace ERP.API.Tests;

/// <summary>
/// ACCOUNTING-12B — ventas con costo pendiente: endpoint y NavItem "Costos pendientes" con permiso
/// propio inventory.pending-costs.view (inventory.stock.view corresponde a Kardex).
/// </summary>
public sealed class InventoryPendingCostsPermissionContractTests
{
    [Fact]
    public void A12B_endpoint_y_NavItem_de_costos_pendientes_usan_permiso_propio()
    {
        InventoryPermissions.PendingCostsView.Should().Be("inventory.pending-costs.view");

        typeof(StockController)
            .GetMethod(nameof(StockController.GetPendingCosts))!
            .GetCustomAttributes<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .Should().ContainSingle().Which.Should().Be($"perm:{InventoryPermissions.PendingCostsView}");

        var field = typeof(InventoryModule).GetField(nameof(InventoryModule.PendingCosts))!;
        field.GetRawConstantValue().Should().Be("/inventory/pending-costs");
        var navItem = field.GetCustomAttribute<NavItemAttribute>()!;
        navItem.Permission.Should().Be(InventoryPermissions.PendingCostsView);
        navItem.ParentId.Should().Be("e2000000-0000-4000-9000-000000000010");

        typeof(InventoryModule)
            .GetField(nameof(InventoryModule.OperationGroup))!
            .GetCustomAttribute<NavItemAttribute>()!
            .PermissionsAnyCsv!.Split(',').Should().Contain(InventoryPermissions.PendingCostsView);
    }
}
