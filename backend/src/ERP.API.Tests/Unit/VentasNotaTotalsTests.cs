using FluentAssertions;
using ERP.API.Tests.Support;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.API.Tests.Unit;

public sealed class VentasNotaTotalsTests
{
    [Fact]
    public void VentasNotaCreditoDebito_totales_coinciden_con_suma_de_detalles()
    {
        var tenantId  = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var facturaId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var clave     = ClaveAcceso49TestFactory.FromPrefix48(new string('7', 48));

        var nota = VentasNotaCreditoDebito.Create(
            tenantId,
            facturaId,
            "CREDITO",
            "Devolución",
            "04",
            "001",
            "001",
            "000000002",
            clave,
            DateTime.UtcNow,
            userId);

        var d1 = VentasNotaDetalle.Create(tenantId, productId, 2m, 10m, 1.20m, "Línea A", userId);
        var d2 = VentasNotaDetalle.Create(tenantId, productId, 1m, 5m, 0m, "Línea B", userId);
        d1.AsignarNotaId(nota.Id);
        d2.AsignarNotaId(nota.Id);
        nota.AgregarDetalle(d1);
        nota.AgregarDetalle(d2);

        var sumSub = nota.Detalles.Sum(d => d.Subtotal);
        var sumIva = nota.Detalles.Sum(d => d.Impuesto);
        var sumTot = nota.Detalles.Sum(d => d.Total);

        nota.Subtotal.Should().Be(sumSub);
        nota.Impuesto.Should().Be(sumIva);
        nota.Total.Should().Be(sumTot);
        nota.Subtotal.Should().Be(25m);
        nota.Impuesto.Should().Be(1.20m);
        nota.Total.Should().Be(26.20m);
    }
}
