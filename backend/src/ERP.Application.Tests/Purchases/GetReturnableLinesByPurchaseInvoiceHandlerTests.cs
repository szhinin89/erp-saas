using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>P0-02 Fase 5 — <c>GetReturnableLinesByPurchaseInvoiceHandler</c>: remanente con 0 y N devoluciones autorizadas previas simuladas (§10.2).</summary>
public sealed class GetReturnableLinesByPurchaseInvoiceHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();

    private static PurchaseInvoice ConfirmedInvoiceWithOneLine(decimal quantity = 10)
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
            PaymentTermId,
            "Contado",
            1,
            30,
            globalWarehouseId: WarehouseId
        );
        var line = PurchaseInvoiceDetail.Create(
            inv.Id,
            TenantId,
            "Producto 1",
            quantity: quantity,
            unitPrice: 10.00m,
            vatCode: "10",
            uomCode: "UNIT",
            itemId: Guid.NewGuid(),
            warehouseId: WarehouseId
        );
        inv.ReplaceLines(new[] { line }, UserId);
        inv.Confirm(UserId);
        return inv;
    }

    private static (
        GetReturnableLinesByPurchaseInvoiceHandler handler,
        Mock<IPurchaseInvoiceRepository> invoiceRepo,
        Mock<IPurchaseReturnRepository> returnRepo
    ) BuildHandler()
    {
        var invoiceRepo = new Mock<IPurchaseInvoiceRepository>();
        var returnRepo = new Mock<IPurchaseReturnRepository>();
        var t = new Mock<ICurrentTenant>();
        t.SetupGet(x => x.TenantId).Returns(TenantId);
        var handler = new GetReturnableLinesByPurchaseInvoiceHandler(
            invoiceRepo.Object,
            returnRepo.Object,
            t.Object
        );
        return (handler, invoiceRepo, returnRepo);
    }

    [Fact]
    public async Task Sin_devoluciones_previas_el_remanente_es_igual_a_la_cantidad_original()
    {
        var (handler, invoiceRepo, returnRepo) = BuildHandler();
        var invoice = ConfirmedInvoiceWithOneLine(10);
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        returnRepo
            .Setup(r =>
                r.GetReturnedQuantitiesByInvoiceDetailIdsAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, decimal>());

        var result = await handler.Handle(
            new GetReturnableLinesByPurchaseInvoiceQuery(invoice.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        var line = result.Value!.Single();
        line.OriginalQuantity.Should().Be(10);
        line.ReturnedQuantity.Should().Be(0);
        line.RemainingQuantity.Should().Be(10);
        line.WarehouseId.Should().Be(WarehouseId);
    }

    [Fact]
    public async Task Con_devoluciones_autorizadas_previas_el_remanente_descuenta_lo_ya_devuelto()
    {
        var (handler, invoiceRepo, returnRepo) = BuildHandler();
        var invoice = ConfirmedInvoiceWithOneLine(10);
        var lineId = invoice.Lines.Single().Id;
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        returnRepo
            .Setup(r =>
                r.GetReturnedQuantitiesByInvoiceDetailIdsAsync(
                    TenantId,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new Dictionary<Guid, decimal> { [lineId] = 4 });

        var result = await handler.Handle(
            new GetReturnableLinesByPurchaseInvoiceQuery(invoice.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        var line = result.Value!.Single();
        line.OriginalQuantity.Should().Be(10);
        line.ReturnedQuantity.Should().Be(4);
        line.RemainingQuantity.Should().Be(6);
    }

    [Fact]
    public async Task Factura_inexistente_retorna_NotFound()
    {
        var (handler, invoiceRepo, _) = BuildHandler();
        invoiceRepo
            .Setup(r => r.GetByIdAsync(TenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PurchaseInvoice?)null);

        var result = await handler.Handle(
            new GetReturnableLinesByPurchaseInvoiceQuery(Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }
}
