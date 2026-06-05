using FluentAssertions;
using ERP.API.Tests.Support;
using ERP.Domain.Common;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.API.Tests.Unit;

public sealed class SalesNoteTotalsTests
{
    [Fact]
    public void VentasNotaCreditoDebito_totales_coinciden_con_suma_de_detalles()
    {
        var subscriberId  = Guid.NewGuid();
        var userId    = Guid.NewGuid();
        var facturaId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var clave     = AccessKey49TestFactory.FromPrefix48(new string('7', 48));

        var note = SalesNote.Create(
            subscriberId,
            facturaId,
            NoteType.Credit,
            "DevoluciÃ³n",
            "04",
            "001",
            "001",
            "000000002",
            clave,
            DateTime.UtcNow,
            userId);

        var d1 = SalesNoteLine.Create(subscriberId, productId, "P1", 2m, 10m, "2", 12m, 1.20m, "Línea A", userId);
        var d2 = SalesNoteLine.Create(subscriberId, productId, "P2", 1m, 5m, "0", 0m, 0m, "Línea B", userId);
        d1.AssignNoteId(note.Id);
        d2.AssignNoteId(note.Id);
        note.AddLine(d1);
        note.AddLine(d2);

        var sumSub = note.Lines.Sum(d => d.Subtotal);
        var sumIva = note.Lines.Sum(d => d.VatTotal);
        var sumTot = note.Lines.Sum(d => d.Total);

        note.Subtotal.Should().Be(sumSub);
        note.VatTotal.Should().Be(sumIva);
        note.Total.Should().Be(sumTot);
        note.Subtotal.Should().Be(25m);
        note.VatTotal.Should().Be(1.20m);
        note.Total.Should().Be(26.20m);
    }
}


