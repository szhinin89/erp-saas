using FluentAssertions;
using ERP.Application.Sales.Helpers;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.API.Tests.Unit;

/// <summary>Pruebas unitarias puras: entidad de dominio y algoritmo de clave de acceso.</summary>
public sealed class SalesDomainTests
{
    private static readonly Guid SubscriberId = Guid.NewGuid();
    private static readonly Guid UserId   = Guid.NewGuid();

    // Ã¢â€â‚¬Ã¢â€â‚¬ RecalcularTotales Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    [Fact]
    public void VentasFactura_AgregarDetalle_recalcula_totales_correctamente()
    {
        var salesBill = BuildFactura();

        var d1 = SalesBillLine.Create(SubscriberId, Guid.NewGuid(), "P-A", 2m, 10m, 0m, "2", 12m, 2.4m, "Prod A", UserId);
        var d2 = SalesBillLine.Create(SubscriberId, Guid.NewGuid(), "P-B", 1m, 50m, 0m, "2", 12m, 7.5m, "Prod B", UserId);

        d1.AssignBillId(salesBill.Id);
        d2.AssignBillId(salesBill.Id);
        salesBill.AddLine(d1);
        salesBill.AddLine(d2);

        salesBill.Subtotal.Should().Be(70m);   // 2*10 + 1*50
        salesBill.VatTotal.Should().Be(9.9m);  // 2.4 + 7.5
        salesBill.Total.Should().Be(79.9m);
    }

    [Fact]
    public void VentasFactura_AgregarDetalle_acumula_multiples_veces()
    {
        var salesBill = BuildFactura();
        var productoId = Guid.NewGuid();

        for (var i = 1; i <= 3; i++)
        {
            var d = SalesBillLine.Create(SubscriberId, productoId, $"P{i}", i, 10m, 0m, "0", 0m, 0m, $"Prod {i}", UserId);
            d.AssignBillId(salesBill.Id);
            salesBill.AddLine(d);
        }

        salesBill.Subtotal.Should().Be(60m); // (1+2+3) * 10
        salesBill.Total.Should().Be(60m);
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Transiciones de estado Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    [Fact]
    public void VentasFactura_estadoInicial_es_Borrador()
    {
        var salesBill = BuildFactura();
        salesBill.Status.Should().Be("Borrador");
    }

    [Fact]
    public void VentasFactura_Validar_cambia_estado_a_Validado()
    {
        var salesBill = BuildFactura();
        salesBill.Validate(UserId);
        salesBill.Status.Should().Be("Validado");
    }

    [Fact]
    public void VentasFactura_Validar_falla_si_no_es_Borrador()
    {
        var salesBill = BuildFactura();
        salesBill.Validate(UserId);

        var acto = () => salesBill.Validate(UserId);
        acto.Should().Throw<InvalidOperationException>()
            .WithMessage("*Borrador*");
    }

    [Fact]
    public void VentasFactura_Autorizar_cambia_estado_a_Autorizado()
    {
        var salesBill = BuildFactura();
        salesBill.Validate(UserId);

        salesBill.Authorize(UserId, "AUTH001", DateTime.UtcNow, "path/gen.xml", "path/aut.xml", Guid.NewGuid());

        salesBill.Status.Should().Be("Autorizado");
        salesBill.AuthNumber.Should().Be("AUTH001");
        salesBill.JournalEntryId.Should().NotBeNull();
    }

    [Fact]
    public void VentasFactura_Autorizar_falla_si_no_es_Validado()
    {
        var salesBill = BuildFactura(); // Borrador
        var acto = () => salesBill.Authorize(UserId, "X", DateTime.UtcNow, null, null, Guid.NewGuid());
        acto.Should().Throw<InvalidOperationException>().WithMessage("*Validada*");
    }

    [Fact]
    public void VentasFactura_Rechazar_guarda_mensaje_de_error()
    {
        var salesBill = BuildFactura();
        salesBill.Reject(UserId, "RUC invÃƒÂ¡lido segÃƒÂºn SRI.");

        salesBill.Status.Should().Be("Rechazado");
        salesBill.ErrorMessage.Should().Be("RUC invÃƒÂ¡lido segÃƒÂºn SRI.");
    }

    [Fact]
    public void VentasFactura_MarcarErrorEnvio_cambia_estado_a_ErrorEnvio()
    {
        var salesBill = BuildFactura();
        salesBill.MarkSendError(UserId, "Timeout al conectar con SRI.");
        salesBill.Status.Should().Be("ErrorEnvio");
    }

    [Fact]
    public void VentasFactura_PrepararReintento_resetea_a_Validado()
    {
        var salesBill = BuildFactura();
        salesBill.MarkSendError(UserId, "Timeout");
        salesBill.PrepareRetry(UserId);

        salesBill.Status.Should().Be("Validado");
        salesBill.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void VentasFactura_PrepararReintento_falla_si_no_es_ErrorEnvio_o_Rechazado()
    {
        var salesBill = BuildFactura(); // Borrador
        var acto = () => salesBill.PrepareRetry(UserId);
        acto.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void VentasFactura_Anular_cambia_estado_a_Anulado()
    {
        var salesBill = BuildFactura();
        salesBill.Void(UserId);
        salesBill.Status.Should().Be("Anulado");
    }

    [Fact]
    public void VentasFactura_Anular_falla_si_ya_esta_Autorizado()
    {
        var salesBill = BuildFactura();
        salesBill.Validate(UserId);
        salesBill.Authorize(UserId, "NUM", DateTime.UtcNow, null, null, Guid.NewGuid());

        var acto = () => salesBill.Void(UserId);
        acto.Should().Throw<InvalidOperationException>().WithMessage("*Autorizado*");
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Helpers Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    private static SalesBill BuildFactura() => SalesBill.Create(
        subscriberId:          SubscriberId,
        branchId:        Guid.NewGuid(),
        businessPartnerId:         Guid.NewGuid(),
        warehouseId:          Guid.NewGuid(),
        docType:     "01",
        estabCode:   "001",
        emPointCode:      "001",
        sequential:        "000000001",
        accessKey:       new string('0', 48) + "0",
        issueDate:      DateTime.UtcNow,
        subtotal:          0m,
        vatTotal:          0m,
        total:             0m,
        totalDiscount:     0m,
        paymentMethodCode: "01",
        paymentDays:       0,
        notes:             null,
        xmlSignedPath:   null,
        xmlAuthPath: null,
        authNumber:  null,
        authDate:   null,
        errorMessage:        null,
        createdBy:           UserId);
}









