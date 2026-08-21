using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.CreateWarehouse;
using ERP.Application.Modules.Inventory.Warehouses.UseCases.UpdateWarehouse;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Branches.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Inventory;

/// <summary>
/// ERP-CORE-CLOSEOUT-05-FIX01 (P0-4) — Create/UpdateWarehouseCommandHandler confiaban en
/// <c>command.BranchId</c> sin validar que la sucursal exista y pertenezca a la empresa activa.
/// Un usuario de la Empresa A podía crear/actualizar una bodega apuntando a una sucursal de la
/// Empresa B del mismo tenant. Ahora ambos handlers resuelven la sucursal vía
/// <see cref="IBranchRepository"/> y rechazan el comando si no existe o pertenece a otra empresa.
/// </summary>
public sealed class WarehouseBranchOwnershipTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyAId = Guid.NewGuid();
    private static readonly Guid CompanyBId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static Branch CreateBranch(Guid companyId) =>
        Branch.Create(
            tenantId: TenantId,
            name: "Sucursal Test",
            address: "Av. Test 100",
            code: "BT",
            description: null,
            reference: null,
            postalCode: null,
            phone: null,
            secondaryPhone: null,
            email: null,
            website: null,
            managerName: null,
            managerPosition: null,
            managerEmail: null,
            managerPhone: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            openingDate: null,
            internalNotes: null,
            isMainBranch: true,
            createdBy: UserId,
            companyId: companyId
        );

    // ── CreateWarehouseCommandHandler ────────────────────────────────────

    private sealed class CreateFixture
    {
        public Mock<IWarehouseRepository> Repo { get; } = new();
        public Mock<IBranchRepository> BranchRepo { get; } = new();
        public Mock<IUserActivityRepository> Activity { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public CreateFixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyAId);
            User.Setup(u => u.UserId).Returns(UserId);
            Repo.Setup(r =>
                    r.ExistsCodeAsync(
                        TenantId,
                        It.IsAny<Guid>(),
                        It.IsAny<string>(),
                        null,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(false);
        }

        public CreateWarehouseCommandHandler BuildHandler() =>
            new(Repo.Object, BranchRepo.Object, Activity.Object, Tenant.Object, Company.Object, User.Object);
    }

    private static CreateWarehouseCommand BuildCreateCommand(Guid branchId) =>
        new(
            branchId,
            "Bodega Test",
            StorageType: null,
            Address: null,
            Phone: null,
            Email: null,
            Manager: null,
            Latitude: null,
            Longitude: null,
            Capacity: null,
            DailyDispatchGoal: null
        );

    [Fact]
    public async Task Crear_bodega_con_sucursal_de_otra_empresa_es_rechazado()
    {
        var branchOfCompanyB = CreateBranch(CompanyBId);
        var f = new CreateFixture();
        f.BranchRepo.Setup(r => r.GetByIdAsync(TenantId, branchOfCompanyB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branchOfCompanyB);

        var result = await f.BuildHandler()
            .Handle(BuildCreateCommand(branchOfCompanyB.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.Repo.Verify(r => r.AddAsync(It.IsAny<Warehouse>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Crear_bodega_con_sucursal_inexistente_es_rechazado()
    {
        var f = new CreateFixture();
        var missingBranchId = Guid.NewGuid();
        f.BranchRepo.Setup(r => r.GetByIdAsync(TenantId, missingBranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Branch?)null);

        var result = await f.BuildHandler()
            .Handle(BuildCreateCommand(missingBranchId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.Repo.Verify(r => r.AddAsync(It.IsAny<Warehouse>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Crear_bodega_con_sucursal_de_la_propia_empresa_funciona()
    {
        var ownBranch = CreateBranch(CompanyAId);
        var f = new CreateFixture();
        f.BranchRepo.Setup(r => r.GetByIdAsync(TenantId, ownBranch.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownBranch);

        var result = await f.BuildHandler()
            .Handle(BuildCreateCommand(ownBranch.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Repo.Verify(r => r.AddAsync(It.IsAny<Warehouse>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UpdateWarehouseCommandHandler ────────────────────────────────────

    private sealed class UpdateFixture
    {
        public Mock<IWarehouseRepository> Repo { get; } = new();
        public Mock<IBranchRepository> BranchRepo { get; } = new();
        public Mock<IUserActivityRepository> Activity { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();

        public UpdateFixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyAId);
            User.Setup(u => u.UserId).Returns(UserId);
        }

        public UpdateWarehouseCommandHandler BuildHandler() =>
            new(Repo.Object, BranchRepo.Object, Activity.Object, Tenant.Object, Company.Object, User.Object);
    }

    private static Warehouse CreateWarehouse(Guid branchId) =>
        Warehouse.Create(
            TenantId,
            branchId,
            "Bodega Existente",
            "WEX",
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
            CompanyAId
        );

    private static UpdateWarehouseCommand BuildUpdateCommand(Guid id, Guid branchId) =>
        new(
            id,
            branchId,
            "Bodega Existente",
            StorageType: null,
            Address: null,
            Phone: null,
            Email: null,
            Manager: null,
            Latitude: null,
            Longitude: null,
            Capacity: null,
            DailyDispatchGoal: null
        );

    [Fact]
    public async Task Actualizar_bodega_hacia_sucursal_de_otra_empresa_es_rechazado()
    {
        var originalBranch = CreateBranch(CompanyAId);
        var warehouse = CreateWarehouse(originalBranch.Id);
        var branchOfCompanyB = CreateBranch(CompanyBId);

        var f = new UpdateFixture();
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, warehouse.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouse);
        f.BranchRepo.Setup(r => r.GetByIdAsync(TenantId, branchOfCompanyB.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branchOfCompanyB);

        var result = await f.BuildHandler()
            .Handle(BuildUpdateCommand(warehouse.Id, branchOfCompanyB.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Actualizar_bodega_hacia_sucursal_de_la_propia_empresa_funciona()
    {
        var originalBranch = CreateBranch(CompanyAId);
        var warehouse = CreateWarehouse(originalBranch.Id);
        var otherOwnBranch = CreateBranch(CompanyAId);

        var f = new UpdateFixture();
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, warehouse.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouse);
        f.BranchRepo.Setup(r => r.GetByIdAsync(TenantId, otherOwnBranch.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherOwnBranch);

        var result = await f.BuildHandler()
            .Handle(BuildUpdateCommand(warehouse.Id, otherOwnBranch.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        f.Repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
