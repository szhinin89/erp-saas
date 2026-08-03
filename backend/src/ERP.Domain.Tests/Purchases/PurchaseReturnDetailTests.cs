using ERP.Domain.Modules.Purchases.Entities;
using FluentAssertions;

namespace ERP.Domain.Tests.Purchases;

public sealed class PurchaseReturnDetailTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid PurchaseReturnId = Guid.NewGuid();
    private static readonly Guid OriginalInvoiceDetailId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();
    private static readonly Guid WarehouseId = Guid.NewGuid();

    private static PurchaseReturnDetail Create(decimal quantity = 3m) =>
        PurchaseReturnDetail.Create(
            PurchaseReturnId,
            TenantId,
            OriginalInvoiceDetailId,
            ItemId,
            quantity,
            WarehouseId
        );

    [Fact]
    public void Create_con_datos_validos_no_esta_congelada()
    {
        var line = Create();

        line.IsFrozen.Should().BeFalse();
        line.Quantity.Should().Be(3m);
        line.WarehouseId.Should().Be(WarehouseId);
        line.ItemId.Should().Be(ItemId);
        line.UnitCost.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rechaza_cantidad_no_positiva(decimal quantity)
    {
        var act = () => Create(quantity);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_linea_original_vacia()
    {
        var act = () =>
            PurchaseReturnDetail.Create(
                PurchaseReturnId,
                TenantId,
                Guid.Empty,
                ItemId,
                3m,
                WarehouseId
            );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_rechaza_bodega_vacia()
    {
        var act = () =>
            PurchaseReturnDetail.Create(
                PurchaseReturnId,
                TenantId,
                OriginalInvoiceDetailId,
                ItemId,
                3m,
                Guid.Empty
            );

        act.Should().Throw<ArgumentException>();
    }

    // ── Freeze — internal, invocado únicamente desde PurchaseReturn.Authorize(): cubierto por
    // PurchaseReturnTests.Authorize_feliz_congela_lineas_y_calcula_totales (congela el snapshot
    // financiero completo) y Authorize_ya_autorizada_no_se_puede_volver_a_autorizar (idempotencia
    // de la congelación) — no se invoca directamente aquí porque el método es intencionalmente
    // internal (única API pública es PurchaseReturn.Authorize).
}
