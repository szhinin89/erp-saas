using ERP.Application.Common;
using ERP.Application.Modules.Sales.UseCases.GetSalesInvoiceDefaults;
using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// CONFIG-FOUNDATION-P0-01 — cobertura de la resolución server-side del default de bodega de
/// venta (Branch OrgSetting → Warehouse.IsMain de la sucursal → null + selección manual).
/// Ver GetSalesInvoiceDefaultsQueryHandler.ResolveDefaultWarehouseAsync.
/// </summary>
public sealed class GetSalesInvoiceDefaultsHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IOrgSettingsRepository> OrgRepo { get; } = new();
        public Mock<IEmissionPointRepository> EpRepo { get; } = new();
        public Mock<IWarehouseRepository> WarehouseRepo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            Branch.Setup(b => b.HasBranchContext).Returns(true);
            Branch.Setup(b => b.BranchId).Returns(BranchId);

            OrgRepo
                .Setup(r =>
                    r.GetAllForScopeAsync(
                        TenantId,
                        CompanyId,
                        OrgScope.Company,
                        CompanyId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Array.Empty<OrgSetting>());

            EpRepo
                .Setup(r =>
                    r.GetDefaultForCompanyAsync(TenantId, CompanyId, It.IsAny<CancellationToken>())
                )
                .ReturnsAsync((EmissionPoint?)null);
        }

        public GetSalesInvoiceDefaultsQueryHandler BuildHandler() =>
            new(
                OrgRepo.Object,
                EpRepo.Object,
                WarehouseRepo.Object,
                Tenant.Object,
                Company.Object,
                Branch.Object
            );

        public void SetBranchWarehouseSetting(string? rawValue)
        {
            var setting = OrgSetting.Create(
                TenantId,
                CompanyId,
                OrgScope.Branch,
                BranchId,
                OrgSettingKeys.Invoice.DefaultWarehouseId,
                rawValue,
                SettingDataType.Guid,
                UserId
            );
            OrgRepo
                .Setup(r =>
                    r.GetAsync(
                        TenantId,
                        CompanyId,
                        OrgScope.Branch,
                        BranchId,
                        OrgSettingKeys.Invoice.DefaultWarehouseId,
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(setting);
        }
    }

    private static Warehouse MakeWarehouse(Guid branchId, Guid companyId, bool isMain = false) =>
        Warehouse.Create(
            tenantId: TenantId,
            branchId: branchId,
            name: "Bodega Test",
            code: "W-" + Guid.NewGuid().ToString("N")[..6],
            storageType: null,
            address: null,
            phone: null,
            email: null,
            manager: null,
            latitude: null,
            longitude: null,
            capacity: null,
            dailyDispatchGoal: null,
            createdBy: UserId,
            companyId: companyId,
            isMain: isMain
        );

    [Fact]
    public async Task Branch_tiene_OrgSetting_valido_devuelve_esa_bodega()
    {
        var f = new Fixture();
        var warehouse = MakeWarehouse(BranchId, CompanyId);
        f.SetBranchWarehouseSetting(warehouse.Id.ToString());
        f.WarehouseRepo
            .Setup(r => r.GetByIdAsync(TenantId, warehouse.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(warehouse);

        var result = await f.BuildHandler().Handle(new GetSalesInvoiceDefaultsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultWarehouseId.Should().Be(warehouse.Id);
        result.Value!.DefaultWarehouseSource.Should().Be("BranchSetting");
        result.Value!.RequiresManualWarehouseSelection.Should().BeFalse();
        result.Value!.ConfigurationWarnings.Should().BeEmpty();
        f.WarehouseRepo.Verify(
            r => r.GetMainForBranchAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Branch_OrgSetting_apunta_a_bodega_de_otra_sucursal_falla_cerrado_sin_fallback()
    {
        var f = new Fixture();
        var otherBranchId = Guid.NewGuid();
        var warehouseFromOtherBranch = MakeWarehouse(otherBranchId, CompanyId);
        f.SetBranchWarehouseSetting(warehouseFromOtherBranch.Id.ToString());
        f.WarehouseRepo
            .Setup(r =>
                r.GetByIdAsync(
                    TenantId,
                    warehouseFromOtherBranch.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(warehouseFromOtherBranch);

        var result = await f.BuildHandler().Handle(new GetSalesInvoiceDefaultsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultWarehouseId.Should().BeNull();
        result.Value!.DefaultWarehouseSource.Should().Be("None");
        result.Value!.RequiresManualWarehouseSelection.Should().BeTrue();
        result.Value!.ConfigurationWarnings.Should().NotBeEmpty();
        // Fail-closed: nunca cae a Warehouse.IsMain cuando el valor configurado es inválido.
        f.WarehouseRepo.Verify(
            r => r.GetMainForBranchAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Sin_OrgSetting_con_Warehouse_IsMain_devuelve_la_bodega_principal()
    {
        var f = new Fixture();
        f.SetBranchWarehouseSetting(null);
        var mainWarehouse = MakeWarehouse(BranchId, CompanyId, isMain: true);
        f.WarehouseRepo
            .Setup(r => r.GetMainForBranchAsync(TenantId, BranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mainWarehouse);

        var result = await f.BuildHandler().Handle(new GetSalesInvoiceDefaultsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultWarehouseId.Should().Be(mainWarehouse.Id);
        result.Value!.DefaultWarehouseSource.Should().Be("BranchMainWarehouse");
        result.Value!.RequiresManualWarehouseSelection.Should().BeFalse();
    }

    [Fact]
    public async Task Sin_OrgSetting_ni_IsMain_devuelve_null_y_exige_seleccion_manual()
    {
        var f = new Fixture();
        f.SetBranchWarehouseSetting(null);
        f.WarehouseRepo
            .Setup(r => r.GetMainForBranchAsync(TenantId, BranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Warehouse?)null);

        var result = await f.BuildHandler().Handle(new GetSalesInvoiceDefaultsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultWarehouseId.Should().BeNull();
        result.Value!.DefaultWarehouseSource.Should().Be("None");
        result.Value!.RequiresManualWarehouseSelection.Should().BeTrue();
    }

    [Fact]
    public async Task No_hay_fuga_cross_tenant_company_las_consultas_usan_el_tenant_y_company_activos()
    {
        var f = new Fixture();
        f.SetBranchWarehouseSetting(null);
        f.WarehouseRepo
            .Setup(r => r.GetMainForBranchAsync(TenantId, BranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Warehouse?)null);

        await f.BuildHandler().Handle(new GetSalesInvoiceDefaultsQuery(), default);

        f.OrgRepo.Verify(
            r =>
                r.GetAsync(
                    TenantId,
                    CompanyId,
                    OrgScope.Branch,
                    BranchId,
                    OrgSettingKeys.Invoice.DefaultWarehouseId,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        f.WarehouseRepo.Verify(
            r => r.GetMainForBranchAsync(TenantId, BranchId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Valor_corrupto_no_guid_falla_cerrado_sin_fallback_silencioso()
    {
        var f = new Fixture();
        f.SetBranchWarehouseSetting("no-es-un-guid");

        var result = await f.BuildHandler().Handle(new GetSalesInvoiceDefaultsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultWarehouseId.Should().BeNull();
        result.Value!.DefaultWarehouseSource.Should().Be("None");
        result.Value!.RequiresManualWarehouseSelection.Should().BeTrue();
        result.Value!.ConfigurationWarnings.Should().NotBeEmpty();
        f.WarehouseRepo.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.WarehouseRepo.Verify(
            r => r.GetMainForBranchAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Sin_contexto_de_sucursal_no_resuelve_bodega()
    {
        var f = new Fixture();
        f.Branch.Setup(b => b.HasBranchContext).Returns(false);

        var result = await f.BuildHandler().Handle(new GetSalesInvoiceDefaultsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultWarehouseId.Should().BeNull();
        result.Value!.DefaultWarehouseSource.Should().Be("None");
        result.Value!.RequiresManualWarehouseSelection.Should().BeTrue();
    }
}
