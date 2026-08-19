using ERP.Domain.Modules.Inventory.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Inventory;

/// <summary>
/// CONFIG-FOUNDATION-P0-01 — antes de este cambio, Warehouse.IsMain solo podía fijarse en
/// Create/CreateSystemSeeded; no existía ningún método controlado para cambiarlo después. Este
/// test cubre el nuevo <see cref="Warehouse.SetMain"/>, único punto de mutación del flag.
/// </summary>
public sealed class WarehouseSetMainTests
{
    private static Warehouse MakeWarehouse(bool isMain = false) =>
        Warehouse.Create(
            tenantId: Guid.NewGuid(),
            branchId: Guid.NewGuid(),
            name: "Bodega Central",
            code: "BOD-01",
            storageType: null,
            address: null,
            phone: null,
            email: null,
            manager: null,
            latitude: null,
            longitude: null,
            capacity: null,
            dailyDispatchGoal: null,
            createdBy: Guid.NewGuid(),
            companyId: Guid.NewGuid(),
            isMain: isMain
        );

    [Fact]
    public void SetMain_true_marca_la_bodega_como_principal_y_audita_el_cambio()
    {
        var warehouse = MakeWarehouse(isMain: false);
        var updatedBy = Guid.NewGuid();

        warehouse.SetMain(true, updatedBy);

        warehouse.IsMain.Should().BeTrue();
        warehouse.UpdatedBy.Should().Be(updatedBy);
        warehouse.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void SetMain_false_desmarca_la_bodega_como_principal()
    {
        var warehouse = MakeWarehouse(isMain: true);

        warehouse.SetMain(false, Guid.NewGuid());

        warehouse.IsMain.Should().BeFalse();
    }
}
