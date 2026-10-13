using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// FASE 5 PURCHASE-UX — el listado de compras expone proveedor (snapshot) y total del documento,
/// y reenvía la búsqueda tal cual al repositorio (que la aplica a número o proveedor).
/// </summary>
public sealed class GetPurchaseListHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static PurchaseInvoice CreateInvoiceWithLine()
    {
        var invoice = PurchaseInvoice.CreateDraft(
            TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Distribuidora Andina S.A.",
            "1790012345001",
            "01",
            "001-001-000000123",
            new DateOnly(2026, 10, 1),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Contado",
            1,
            0
        );
        var line = PurchaseInvoiceDetail.Create(invoice.Id, TenantId, "Arroz 1kg", 2m, 10m, "0", "UNIT");
        invoice.ReplaceLines([line], Guid.NewGuid());
        return invoice;
    }

    [Fact]
    public async Task Listado_expone_proveedor_y_total_del_documento()
    {
        var invoice = CreateInvoiceWithLine();
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r =>
                r.GetPagedAsync(TenantId, "andina", null, 1, 25, null, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                (
                    (IReadOnlyList<PurchaseInvoice>)[invoice],
                    (IReadOnlyDictionary<Guid, int>)new Dictionary<Guid, int> { [invoice.Id] = 1 },
                    1
                )
            );
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);

        var result = await new GetPurchaseListHandler(repo.Object, tenant.Object).Handle(
            new GetPurchaseListQuery(Search: "andina"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        var dto = result.Value!.Items.Single();
        dto.SupplierName.Should().Be("Distribuidora Andina S.A.");
        dto.SupplierTaxId.Should().Be("1790012345001");
        dto.GrandTotal.Should().Be(invoice.GrandTotal);
        dto.GrandTotal.Should().Be(20m);
        dto.LineCount.Should().Be(1);
    }
}
