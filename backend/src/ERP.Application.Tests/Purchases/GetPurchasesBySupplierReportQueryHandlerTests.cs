using ERP.Application.Common;
using ERP.Application.Modules.Purchases.UseCases.GetPurchasesBySupplierReport;
using ERP.Domain.Modules.Purchases.Entities;
using ERP.Domain.Modules.Purchases.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Purchases;

/// <summary>
/// ERP-CORE-CLOSEOUT-08 — GetPurchasesBySupplierReportQueryHandler sumaba Totals sobre TODAS las
/// compras del rango sin filtrar por Status, inflando "gasto" con compras Draft (aún no
/// confirmadas) y Cancelled. Las filas siguen mostrando todas las compras (con su Status real,
/// para auditoría/trazabilidad) — solo Totals se restringe a Confirmed.
/// </summary>
public sealed class GetPurchasesBySupplierReportQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SupplierId = Guid.NewGuid();
    private static readonly Guid PaymentTermId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IPurchaseInvoiceRepository> Repo { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();

        public Fixture() => Tenant.Setup(t => t.TenantId).Returns(TenantId);

        public GetPurchasesBySupplierReportQueryHandler BuildHandler() =>
            new(Repo.Object, Tenant.Object);
    }

    private static PurchaseInvoice CreateInvoice(decimal unitPrice, bool confirm, bool cancel = false)
    {
        var invoice = PurchaseInvoice.CreateDraft(
            TenantId,
            CompanyId,
            BranchId,
            SupplierId,
            "Proveedor Test",
            "1790012345001",
            "01",
            $"001-001-{Random.Shared.Next(100000, 999999)}",
            new DateOnly(2026, 8, 20),
            UserId,
            PaymentTermId,
            "Contado",
            1,
            30,
            globalWarehouseId: WarehouseId
        );

        var line = PurchaseInvoiceDetail.Create(
            invoice.Id,
            TenantId,
            "Producto Test",
            quantity: 1m,
            unitPrice: unitPrice,
            vatCode: "0",
            uomCode: "UNIT",
            itemId: ItemId,
            warehouseId: WarehouseId
        );
        invoice.ReplaceLines([line], UserId);

        if (confirm)
        {
            invoice.Confirm(UserId);
            if (cancel)
                invoice.Cancel("Anulación de prueba", UserId);
        }

        return invoice;
    }

    [Fact]
    public async Task Totales_solo_incluyen_compras_Confirmed_excluyendo_Draft_y_Cancelled()
    {
        var confirmed = CreateInvoice(100m, confirm: true);
        var draft = CreateInvoice(50m, confirm: false);
        var cancelled = CreateInvoice(30m, confirm: true, cancel: true);

        var f = new Fixture();
        f.Repo.Setup(r =>
                r.GetForSupplierReportAsync(
                    TenantId,
                    It.IsAny<DateOnly>(),
                    It.IsAny<DateOnly>(),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { confirmed, draft, cancelled });

        var result = await f.BuildHandler()
            .Handle(new GetPurchasesBySupplierReportQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Items.Should().HaveCount(3, "todas las compras del rango siguen visibles para auditoría");
        result.Value.Totals.Count.Should().Be(1);
        result.Value.Totals.GrandTotal.Should().Be(confirmed.GrandTotal);
    }

    [Fact]
    public async Task Sin_compras_confirmadas_los_totales_son_cero()
    {
        var draft = CreateInvoice(50m, confirm: false);
        var f = new Fixture();
        f.Repo.Setup(r =>
                r.GetForSupplierReportAsync(
                    TenantId,
                    It.IsAny<DateOnly>(),
                    It.IsAny<DateOnly>(),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new[] { draft });

        var result = await f.BuildHandler()
            .Handle(new GetPurchasesBySupplierReportQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Totals.Count.Should().Be(0);
        result.Value.Totals.GrandTotal.Should().Be(0m);
    }
}
