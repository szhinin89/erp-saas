using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>FLOW-READY-02D.1 — GetPurchaseInvoiceTaxSummariesHandler: proyección de solo lectura de los summaries ya persistidos.</summary>
public sealed class PurchaseInvoiceTaxSummaryQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid WhId = Guid.NewGuid();
    private static readonly Guid PtId = Guid.NewGuid();

    private static PurchaseInvoice BuildConfirmedInvoice()
    {
        var inv = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1234567890001",
            "01",
            "001-001-000000001",
            DateOnly.FromDateTime(DateTime.UtcNow),
            UserId,
            PtId,
            "Contado",
            1,
            30,
            globalWarehouseId: WhId
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto 1",
            quantity: 1,
            unitPrice: 100m,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: Guid.NewGuid(),
            warehouseId: WhId
        );
        inv.ReplaceLines([line], UserId);
        line.ApplyTaxes("10", 15m, "IVA 15%", null, 0m, null);
        inv.Confirm(UserId);
        return inv;
    }

    private static ICurrentTenant FixedTenant()
    {
        var m = new Mock<ICurrentTenant>();
        m.SetupGet(x => x.TenantId).Returns(TenantId);
        return m.Object;
    }

    [Fact]
    public async Task Handle_devuelve_los_summaries_persistidos_de_la_factura()
    {
        var inv = BuildConfirmedInvoice();
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, inv.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inv);
        var creditNoteRepo = new Mock<IPurchaseCreditNoteRepository>();
        creditNoteRepo
            .Setup(r =>
                r.GetCreditedTaxableBaseByPurchaseTaxSummaryIdsAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, decimal>());
        var handler = new GetPurchaseInvoiceTaxSummariesHandler(
            repo.Object,
            creditNoteRepo.Object,
            FixedTenant()
        );

        var result = await handler.Handle(
            new GetPurchaseInvoiceTaxSummariesQuery(inv.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        var dto = result.Value!.Single();
        dto.VatCode.Should().Be("10");
        dto.VatRate.Should().Be(15m);
        dto.TotalAmount.Should().Be(dto.TaxableBase + dto.IceAmount + dto.VatAmount);
        dto.CreditedTaxableBase.Should().Be(0m);
        dto.AvailableTaxableBase.Should().Be(dto.TaxableBase);
    }

    [Fact]
    public async Task Handle_sobre_factura_inexistente_retorna_404()
    {
        var repo = new Mock<IPurchaseInvoiceRepository>();
        repo.Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseInvoice?)null);
        var creditNoteRepo = new Mock<IPurchaseCreditNoteRepository>();
        creditNoteRepo
            .Setup(r =>
                r.GetCreditedTaxableBaseByPurchaseTaxSummaryIdsAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, decimal>());
        var handler = new GetPurchaseInvoiceTaxSummariesHandler(
            repo.Object,
            creditNoteRepo.Object,
            FixedTenant()
        );

        var result = await handler.Handle(
            new GetPurchaseInvoiceTaxSummariesQuery(Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }
}
