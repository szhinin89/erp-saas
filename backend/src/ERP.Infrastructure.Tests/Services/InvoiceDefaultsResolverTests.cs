using ERP.Domain.Configuration.Constants;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Enums;
using ERP.Domain.Configuration.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// CONFIG-FOUNDATION-P1-04 — cobertura de InvoiceDefaultsResolver (movido desde
/// GetSalesInvoiceDefaultsQueryHandler en CONFIG-FOUNDATION-P0-01/P1-04): precedencia de bodega
/// de venta (Branch OrgSetting → Warehouse.IsMain → null + selección manual), fail-closed en
/// valores corruptos/cruzados.
/// </summary>
public sealed class InvoiceDefaultsResolverTests
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

        public Fixture()
        {
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

        public InvoiceDefaultsResolver BuildResolver() =>
            new(OrgRepo.Object, EpRepo.Object, WarehouseRepo.Object);

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

        var result = await f.BuildResolver().GetAsync(TenantId, CompanyId, BranchId, default);

        result.DefaultWarehouseId.Should().Be(warehouse.Id);
        result.DefaultWarehouseSource.Should().Be("BranchSetting");
        result.RequiresManualWarehouseSelection.Should().BeFalse();
        result.ConfigurationWarnings.Should().BeEmpty();
        f.WarehouseRepo.Verify(
            r =>
                r.GetMainForBranchAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
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

        var result = await f.BuildResolver().GetAsync(TenantId, CompanyId, BranchId, default);

        result.DefaultWarehouseId.Should().BeNull();
        result.DefaultWarehouseSource.Should().Be("None");
        result.RequiresManualWarehouseSelection.Should().BeTrue();
        result.ConfigurationWarnings.Should().NotBeEmpty();
        f.WarehouseRepo.Verify(
            r =>
                r.GetMainForBranchAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
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

        var result = await f.BuildResolver().GetAsync(TenantId, CompanyId, BranchId, default);

        result.DefaultWarehouseId.Should().Be(mainWarehouse.Id);
        result.DefaultWarehouseSource.Should().Be("BranchMainWarehouse");
        result.RequiresManualWarehouseSelection.Should().BeFalse();
    }

    [Fact]
    public async Task Sin_OrgSetting_ni_IsMain_devuelve_null_y_exige_seleccion_manual()
    {
        var f = new Fixture();
        f.SetBranchWarehouseSetting(null);
        f.WarehouseRepo
            .Setup(r => r.GetMainForBranchAsync(TenantId, BranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Warehouse?)null);

        var result = await f.BuildResolver().GetAsync(TenantId, CompanyId, BranchId, default);

        result.DefaultWarehouseId.Should().BeNull();
        result.DefaultWarehouseSource.Should().Be("None");
        result.RequiresManualWarehouseSelection.Should().BeTrue();
    }

    [Fact]
    public async Task No_hay_fuga_cross_tenant_company_las_consultas_usan_el_tenant_y_company_activos()
    {
        var f = new Fixture();
        f.SetBranchWarehouseSetting(null);
        f.WarehouseRepo
            .Setup(r => r.GetMainForBranchAsync(TenantId, BranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Warehouse?)null);

        await f.BuildResolver().GetAsync(TenantId, CompanyId, BranchId, default);

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

        var result = await f.BuildResolver().GetAsync(TenantId, CompanyId, BranchId, default);

        result.DefaultWarehouseId.Should().BeNull();
        result.DefaultWarehouseSource.Should().Be("None");
        result.RequiresManualWarehouseSelection.Should().BeTrue();
        result.ConfigurationWarnings.Should().NotBeEmpty();
        f.WarehouseRepo.Verify(
            r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.WarehouseRepo.Verify(
            r =>
                r.GetMainForBranchAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Sin_contexto_de_sucursal_null_no_resuelve_bodega()
    {
        var f = new Fixture();

        var result = await f.BuildResolver().GetAsync(TenantId, CompanyId, null, default);

        result.DefaultWarehouseId.Should().BeNull();
        result.DefaultWarehouseSource.Should().Be("None");
        result.RequiresManualWarehouseSelection.Should().BeTrue();
    }

    [Fact]
    public async Task Resuelve_doc_type_payment_method_y_payment_term_desde_scope_Company()
    {
        var f = new Fixture();
        var paymentTermId = Guid.NewGuid();
        f.OrgRepo
            .Setup(r =>
                r.GetAllForScopeAsync(
                    TenantId,
                    CompanyId,
                    OrgScope.Company,
                    CompanyId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new[]
                {
                    OrgSetting.Create(
                        TenantId,
                        CompanyId,
                        OrgScope.Company,
                        CompanyId,
                        OrgSettingKeys.Invoice.DefaultDocTypeCode,
                        "01",
                        SettingDataType.String,
                        UserId
                    ),
                    OrgSetting.Create(
                        TenantId,
                        CompanyId,
                        OrgScope.Company,
                        CompanyId,
                        OrgSettingKeys.Invoice.DefaultPaymentMethodCode,
                        "01",
                        SettingDataType.String,
                        UserId
                    ),
                    OrgSetting.Create(
                        TenantId,
                        CompanyId,
                        OrgScope.Company,
                        CompanyId,
                        OrgSettingKeys.Invoice.DefaultPaymentTermId,
                        paymentTermId.ToString(),
                        SettingDataType.Guid,
                        UserId
                    ),
                }
            );

        var result = await f.BuildResolver().GetAsync(TenantId, CompanyId, null, default);

        result.DefaultDocTypeCode.Should().Be("01");
        result.DefaultSriPaymentMethodCode.Should().Be("01");
        result.DefaultPaymentTermId.Should().Be(paymentTermId);
    }
}
