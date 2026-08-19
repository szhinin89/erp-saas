using ERP.Application.Common;
using ERP.Application.Modules.Sales.UseCases.GetSalesInvoiceDefaults;
using ERP.Domain.Modules.Sales.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// CONFIG-FOUNDATION-P1-04 — GetSalesInvoiceDefaultsQueryHandler ya no lee
/// IOrgSettingsRepository/IEmissionPointRepository/IWarehouseRepository directamente: delega toda
/// la resolución en IInvoiceDefaultsResolver y solo ensambla el DTO de salida (agregando las
/// constantes Fallback*, que no son org_settings). La cobertura de la lógica de resolución en sí
/// (precedencia de bodega, fail-closed, etc.) vive en InvoiceDefaultsResolverTests
/// (ERP.Infrastructure.Tests, contra la implementación real).
/// </summary>
public sealed class GetSalesInvoiceDefaultsHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IInvoiceDefaultsResolver> Resolver { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            Branch.Setup(b => b.HasBranchContext).Returns(true);
            Branch.Setup(b => b.BranchId).Returns(BranchId);
        }

        public GetSalesInvoiceDefaultsQueryHandler BuildHandler() =>
            new(Resolver.Object, Tenant.Object, Company.Object, Branch.Object);
    }

    [Fact]
    public async Task Ensambla_el_DTO_a_partir_del_resultado_del_resolver()
    {
        var f = new Fixture();
        var warehouseId = Guid.NewGuid();
        var emissionPointId = Guid.NewGuid();
        var paymentTermId = Guid.NewGuid();
        f.Resolver
            .Setup(r => r.GetAsync(TenantId, CompanyId, BranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new InvoiceDefaultsResult(
                    DefaultDocTypeCode: "01",
                    DefaultSriPaymentMethodCode: "01",
                    DefaultPaymentTermId: paymentTermId,
                    DefaultEmissionPointId: emissionPointId,
                    DefaultWarehouseId: warehouseId,
                    DefaultWarehouseSource: "BranchSetting",
                    RequiresManualWarehouseSelection: false,
                    ConfigurationWarnings: Array.Empty<string>()
                )
            );

        var result = await f.BuildHandler().Handle(new GetSalesInvoiceDefaultsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultDocTypeCode.Should().Be("01");
        result.Value.DefaultSriPaymentMethodCode.Should().Be("01");
        result.Value.DefaultPaymentTermId.Should().Be(paymentTermId);
        result.Value.DefaultEmissionPointId.Should().Be(emissionPointId);
        result.Value.DefaultWarehouseId.Should().Be(warehouseId);
        result.Value.DefaultWarehouseSource.Should().Be("BranchSetting");
        result.Value.RequiresManualWarehouseSelection.Should().BeFalse();
        result.Value.ConfigurationWarnings.Should().BeEmpty();
        // Fallback* son constantes de plataforma (SriSettings), no org_settings — el handler las
        // agrega siempre, nunca las inventa ni las lee de ningún repositorio.
        result.Value.FallbackDocTypeCode.Should().NotBeNullOrWhiteSpace();
        result.Value.FallbackSriPaymentMethodCode.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Pasa_null_como_branchId_al_resolver_cuando_no_hay_contexto_de_sucursal()
    {
        var f = new Fixture();
        f.Branch.Setup(b => b.HasBranchContext).Returns(false);
        f.Resolver
            .Setup(r => r.GetAsync(TenantId, CompanyId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new InvoiceDefaultsResult(null, null, null, null, null, "None", true, Array.Empty<string>())
            );

        var result = await f.BuildHandler().Handle(new GetSalesInvoiceDefaultsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DefaultWarehouseId.Should().BeNull();
        result.Value.RequiresManualWarehouseSelection.Should().BeTrue();
        f.Resolver.Verify(
            r => r.GetAsync(TenantId, CompanyId, null, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }
}
