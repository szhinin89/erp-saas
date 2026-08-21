using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// ERP-CORE-CLOSEOUT-05-FIX02 (P1-1) — GetPurchaseByIdHandler filtraba solo por Tenant+Company,
/// permitiendo leer por GUID una compra de otra sucursal de la misma empresa. Mismo patrón ya
/// aplicado a GetSalesInvoiceByIdHandler en FIX01. GetPurchaseListQuery queda sin cambios — su
/// alcance company-wide es una decisión de negocio ya documentada en el propio código.
/// </summary>
public sealed class GetPurchaseByIdHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IPurchaseInvoiceRepository> Repo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();

        public Fixture(Guid? branchContextId = null)
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Branch.Setup(b => b.BranchId).Returns(branchContextId ?? BranchId);
        }

        public GetPurchaseByIdHandler BuildHandler() => new(Repo.Object, Tenant.Object, Branch.Object);
    }

    private static PurchaseInvoice CreateInvoice(Guid branchId) =>
        PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            branchId,
            SupplierId,
            "Proveedor Test",
            "1790012345001",
            "01",
            "001-001-000000123",
            new DateOnly(2026, 8, 20),
            UserId,
            PaymentTermId,
            "Contado",
            1,
            0
        );

    [Fact]
    public async Task Compra_de_la_sucursal_activa_devuelve_detalle()
    {
        var invoice = CreateInvoice(BranchId);
        var f = new Fixture();
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var result = await f.BuildHandler()
            .Handle(new GetPurchaseByIdQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Id.Should().Be(invoice.Id);
    }

    [Fact]
    public async Task Compra_de_otra_sucursal_de_la_misma_empresa_devuelve_NotFound()
    {
        var otherBranchId = Guid.NewGuid();
        var invoice = CreateInvoice(otherBranchId);
        var f = new Fixture(branchContextId: BranchId);
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var result = await f.BuildHandler()
            .Handle(new GetPurchaseByIdQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Compra_inexistente_devuelve_NotFound()
    {
        var f = new Fixture();
        var missingId = Guid.NewGuid();
        f.Repo.Setup(r => r.GetByIdAsync(TenantId, missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseInvoice?)null);

        var result = await f.BuildHandler()
            .Handle(new GetPurchaseByIdQuery(missingId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }
}
