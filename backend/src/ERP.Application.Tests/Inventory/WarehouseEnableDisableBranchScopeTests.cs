using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.DisableWarehouse;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.EnableWarehouse;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Inventory;

/// <summary>
/// ZH-AUTH-INVENTORY-BRANCH-READ-SCOPE-06 — a diferencia de Create/UpdateWarehouseCommand
/// (configuración company-wide, ver <see cref="WarehouseBranchOwnershipTests"/>),
/// habilitar/deshabilitar es una decisión operativa que debe tomarse desde la sucursal dueña de
/// la bodega: un usuario activo en la Sucursal A no debe poder alternar la disponibilidad de una
/// bodega de la Sucursal B de la misma empresa.
/// </summary>
public sealed class WarehouseEnableDisableBranchScopeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchAId = Guid.NewGuid();
    private static readonly Guid BranchBId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Warehouse CreateWarehouse(Guid branchId, bool active = true)
    {
        var warehouse = Warehouse.Create(
            TenantId,
            branchId,
            "Bodega Test",
            "WT",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            UserId,
            CompanyId
        );
        if (!active)
            warehouse.Disable(UserId);
        return warehouse;
    }

    private sealed class Fixture
    {
        public Mock<IWarehouseRepository> Repo { get; } = new();
        public Mock<IUserActivityRepository> Activity { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public Fixture(Guid activeBranchId)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            Branch.Setup(b => b.BranchId).Returns(activeBranchId);
            User.Setup(u => u.UserId).Returns(UserId);
            User.Setup(u => u.Email).Returns("tester@example.com");
            User.Setup(u => u.FullName).Returns("Tester");
        }

        public EnableWarehouseCommandHandler BuildEnableHandler() =>
            new(Repo.Object, Activity.Object, Tenant.Object, Company.Object, Branch.Object, User.Object);

        public DisableWarehouseCommandHandler BuildDisableHandler() =>
            new(Repo.Object, Activity.Object, Tenant.Object, Company.Object, Branch.Object, User.Object);
    }

    [Fact]
    public async Task Habilitar_bodega_de_otra_sucursal_devuelve_NotFound()
    {
        var warehouseOfBranchB = CreateWarehouse(BranchBId, active: false);
        var f = new Fixture(activeBranchId: BranchAId);
        f.Repo.Setup(r => r.GetByIdForCompanyAsync(TenantId, CompanyId, warehouseOfBranchB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouseOfBranchB);

        var result = await f.BuildEnableHandler()
            .Handle(new EnableWarehouseCommand(warehouseOfBranchB.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        warehouseOfBranchB.IsActive.Should().BeFalse("el rechazo debe ocurrir antes de mutar la entidad");
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deshabilitar_bodega_de_otra_sucursal_devuelve_NotFound()
    {
        var warehouseOfBranchB = CreateWarehouse(BranchBId, active: true);
        var f = new Fixture(activeBranchId: BranchAId);
        f.Repo.Setup(r => r.GetByIdForCompanyAsync(TenantId, CompanyId, warehouseOfBranchB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouseOfBranchB);

        var result = await f.BuildDisableHandler()
            .Handle(new DisableWarehouseCommand(warehouseOfBranchB.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        warehouseOfBranchB.IsActive.Should().BeTrue("el rechazo debe ocurrir antes de mutar la entidad");
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Habilitar_bodega_de_la_sucursal_activa_funciona()
    {
        var warehouseOfBranchA = CreateWarehouse(BranchAId, active: false);
        var f = new Fixture(activeBranchId: BranchAId);
        f.Repo.Setup(r => r.GetByIdForCompanyAsync(TenantId, CompanyId, warehouseOfBranchA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouseOfBranchA);

        var result = await f.BuildEnableHandler()
            .Handle(new EnableWarehouseCommand(warehouseOfBranchA.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        warehouseOfBranchA.IsActive.Should().BeTrue();
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deshabilitar_bodega_de_la_sucursal_activa_funciona()
    {
        var warehouseOfBranchA = CreateWarehouse(BranchAId, active: true);
        var f = new Fixture(activeBranchId: BranchAId);
        f.Repo.Setup(r => r.GetByIdForCompanyAsync(TenantId, CompanyId, warehouseOfBranchA.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouseOfBranchA);

        var result = await f.BuildDisableHandler()
            .Handle(new DisableWarehouseCommand(warehouseOfBranchA.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        warehouseOfBranchA.IsActive.Should().BeFalse();
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
