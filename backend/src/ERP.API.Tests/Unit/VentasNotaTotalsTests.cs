using FluentAssertions;
using ERP.API.Tests.Support;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.API.Tests.Unit;

public sealed class VentasNotaTotalsTests
{
    [Fact]
    public void VentasNotaCreditoDebito_totales_coinciden_con_suma_de_detalles()
    {
        var subscriberId  = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var facturaId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var clave     = ClaveAcceso49TestFactory.FromPrefix48(new string('7', 48));

        var nota = SalesNote.Create(
            subscriberId,
            facturaId,
            "CREDITO",
            "DevoluciÃ³n",
            "04",
            "001",
            "001",
            "000000002",
            clave,
            DateTime.UtcNow,
            userId);

        var d1 = SalesNoteLine.Create(subscriberId, productId, 2m, 10m, 1.20m, "LÃ­nea A", userId);
        var d2 = SalesNoteLine.Create(subscriberId, productId, 1m, 5m, 0m, "LÃ­nea B", userId);
        d1.AssignNoteId(nota.Id);
        d2.AssignNoteId(nota.Id);
        nota.AddLine(d1);
        nota.AddLine(d2);

        var sumSub = nota.Lines.Sum(d => d.Subtotal);
        var sumIva = nota.Lines.Sum(d => d.VatTotal);
        var sumTot = nota.Lines.Sum(d => d.Total);

        nota.Subtotal.Should().Be(sumSub);
        nota.VatTotal.Should().Be(sumIva);
        nota.Total.Should().Be(sumTot);
        nota.Subtotal.Should().Be(25m);
        nota.VatTotal.Should().Be(1.20m);
        nota.Total.Should().Be(26.20m);
    }
}


