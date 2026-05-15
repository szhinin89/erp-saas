using FluentAssertions;
using ERP.Application.Sales.Helpers;
using ERP.Domain.Modules.Sales.Entities;

namespace ERP.API.Tests.Unit;

/// <summary>Pruebas unitarias puras: entidad de dominio y algoritmo de clave de acceso.</summary>
public sealed class VentasDomainTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId   = Guid.NewGuid();

    // â”€â”€ RecalcularTotales â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void VentasFactura_AgregarDetalle_recalcula_totales_correctamente()
    {
        var factura = BuildFactura();

        var d1 = SalesBillLine.Create(TenantId, Guid.NewGuid(), quantity: 2m, unitPrice: 10m, vatTotal: 2.4m, "Prod A", UserId);
        var d2 = SalesBillLine.Create(TenantId, Guid.NewGuid(), quantity: 1m, unitPrice: 50m, vatTotal: 7.5m, "Prod B", UserId);

        d1.AssignBillId(factura.Id);
        d2.AssignBillId(factura.Id);
        factura.AddLine(d1);
        factura.AddLine(d2);

        factura.Subtotal.Should().Be(70m);   // 2*10 + 1*50
        factura.VatTotal.Should().Be(9.9m);  // 2.4 + 7.5
        factura.Total.Should().Be(79.9m);
    }

    [Fact]
    public void VentasFactura_AgregarDetalle_acumula_multiples_veces()
    {
        var factura = BuildFactura();
        var productoId = Guid.NewGuid();

        for (var i = 1; i <= 3; i++)
        {
            var d = SalesBillLine.Create(TenantId, productoId, quantity: i, unitPrice: 10m, vatTotal: 0m, $"Prod {i}", UserId);
            d.AssignBillId(factura.Id);
            factura.AddLine(d);
        }

        factura.Subtotal.Should().Be(60m); // (1+2+3) * 10
        factura.Total.Should().Be(60m);
    }

    // â”€â”€ Transiciones de estado â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void VentasFactura_estadoInicial_es_Borrador()
    {
        var factura = BuildFactura();
        factura.Status.Should().Be("Borrador");
    }

    [Fact]
    public void VentasFactura_Validar_cambia_estado_a_Validado()
    {
        var factura = BuildFactura();
        factura.Validate(UserId);
        factura.Status.Should().Be("Validado");
    }

    [Fact]
    public void VentasFactura_Validar_falla_si_no_es_Borrador()
    {
        var factura = BuildFactura();
        factura.Validate(UserId);

        var acto = () => factura.Validate(UserId);
        acto.Should().Throw<InvalidOperationException>()
            .WithMessage("*Borrador*");
    }

    [Fact]
    public void VentasFactura_Autorizar_cambia_estado_a_Autorizado()
    {
        var factura = BuildFactura();
        factura.Validate(UserId);

        factura.Authorize(UserId, "AUTH001", DateTime.UtcNow, "path/gen.xml", "path/aut.xml", Guid.NewGuid());

        factura.Status.Should().Be("Autorizado");
        factura.AuthNumber.Should().Be("AUTH001");
        factura.JournalEntryId.Should().NotBeNull();
    }

    [Fact]
    public void VentasFactura_Autorizar_falla_si_no_es_Validado()
    {
        var factura = BuildFactura(); // Borrador
        var acto = () => factura.Authorize(UserId, "X", DateTime.UtcNow, null, null, Guid.NewGuid());
        acto.Should().Throw<InvalidOperationException>().WithMessage("*Validada*");
    }

    [Fact]
    public void VentasFactura_Rechazar_guarda_mensaje_de_error()
    {
        var factura = BuildFactura();
        factura.Reject(UserId, "RUC invÃ¡lido segÃºn SRI.");

        factura.Status.Should().Be("Rechazado");
        factura.ErrorMessage.Should().Be("RUC invÃ¡lido segÃºn SRI.");
    }

    [Fact]
    public void VentasFactura_MarcarErrorEnvio_cambia_estado_a_ErrorEnvio()
    {
        var factura = BuildFactura();
        factura.MarkSendError(UserId, "Timeout al conectar con SRI.");
        factura.Status.Should().Be("ErrorEnvio");
    }

    [Fact]
    public void VentasFactura_PrepararReintento_resetea_a_Validado()
    {
        var factura = BuildFactura();
        factura.MarkSendError(UserId, "Timeout");
        factura.PrepareRetry(UserId);

        factura.Status.Should().Be("Validado");
        factura.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void VentasFactura_PrepararReintento_falla_si_no_es_ErrorEnvio_o_Rechazado()
    {
        var factura = BuildFactura(); // Borrador
        var acto = () => factura.PrepareRetry(UserId);
        acto.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void VentasFactura_Anular_cambia_estado_a_Anulado()
    {
        var factura = BuildFactura();
        factura.Void(UserId);
        factura.Status.Should().Be("Anulado");
    }

    [Fact]
    public void VentasFactura_Anular_falla_si_ya_esta_Autorizado()
    {
        var factura = BuildFactura();
        factura.Validate(UserId);
        factura.Authorize(UserId, "NUM", DateTime.UtcNow, null, null, Guid.NewGuid());

        var acto = () => factura.Void(UserId);
        acto.Should().Throw<InvalidOperationException>().WithMessage("*Autorizado*");
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static SalesBill BuildFactura() => SalesBill.Create(
        tenantId:          TenantId,
        branchId:        Guid.NewGuid(),
        customerId:         Guid.NewGuid(),
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
        xmlSignedPath:   null,
        xmlAuthPath: null,
        authNumber:  null,
        authDate:   null,
        errorMessage:        null,
        createdBy:           UserId);
}









