using FluentAssertions;
using ERP.Application.Ventas.Helpers;
using ERP.Domain.Ventas.Entities;

namespace ERP.API.Tests.Unit;

/// <summary>Pruebas unitarias puras: entidad de dominio y algoritmo de clave de acceso.</summary>
public sealed class VentasDomainTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId   = Guid.NewGuid();

    // ── RecalcularTotales ─────────────────────────────────────────────────

    [Fact]
    public void VentasFactura_AgregarDetalle_recalcula_totales_correctamente()
    {
        var factura = BuildFactura();

        var d1 = VentasDetalle.Create(TenantId, Guid.NewGuid(), cantidad: 2m, precioUnitario: 10m, impuesto: 2.4m, "Prod A", UserId);
        var d2 = VentasDetalle.Create(TenantId, Guid.NewGuid(), cantidad: 1m, precioUnitario: 50m, impuesto: 7.5m, "Prod B", UserId);

        d1.AsignarFacturaId(factura.Id);
        d2.AsignarFacturaId(factura.Id);
        factura.AgregarDetalle(d1);
        factura.AgregarDetalle(d2);

        factura.Subtotal.Should().Be(70m);   // 2*10 + 1*50
        factura.Impuesto.Should().Be(9.9m);  // 2.4 + 7.5
        factura.Total.Should().Be(79.9m);
    }

    [Fact]
    public void VentasFactura_AgregarDetalle_acumula_multiples_veces()
    {
        var factura = BuildFactura();
        var productoId = Guid.NewGuid();

        for (var i = 1; i <= 3; i++)
        {
            var d = VentasDetalle.Create(TenantId, productoId, cantidad: i, precioUnitario: 10m, impuesto: 0m, $"Prod {i}", UserId);
            d.AsignarFacturaId(factura.Id);
            factura.AgregarDetalle(d);
        }

        factura.Subtotal.Should().Be(60m); // (1+2+3) * 10
        factura.Total.Should().Be(60m);
    }

    // ── Transiciones de estado ────────────────────────────────────────────

    [Fact]
    public void VentasFactura_estadoInicial_es_Borrador()
    {
        var factura = BuildFactura();
        factura.Estado.Should().Be("Borrador");
    }

    [Fact]
    public void VentasFactura_Validar_cambia_estado_a_Validado()
    {
        var factura = BuildFactura();
        factura.Validar(UserId);
        factura.Estado.Should().Be("Validado");
    }

    [Fact]
    public void VentasFactura_Validar_falla_si_no_es_Borrador()
    {
        var factura = BuildFactura();
        factura.Validar(UserId);

        var acto = () => factura.Validar(UserId);
        acto.Should().Throw<InvalidOperationException>()
            .WithMessage("*Borrador*");
    }

    [Fact]
    public void VentasFactura_Autorizar_cambia_estado_a_Autorizado()
    {
        var factura = BuildFactura();
        factura.Validar(UserId);

        factura.Autorizar(UserId, "AUTH001", DateTime.UtcNow, "path/gen.xml", "path/aut.xml", Guid.NewGuid());

        factura.Estado.Should().Be("Autorizado");
        factura.NumeroAutorizacion.Should().Be("AUTH001");
        factura.AsientoContableId.Should().NotBeNull();
    }

    [Fact]
    public void VentasFactura_Autorizar_falla_si_no_es_Validado()
    {
        var factura = BuildFactura(); // Borrador
        var acto = () => factura.Autorizar(UserId, "X", DateTime.UtcNow, null, null, Guid.NewGuid());
        acto.Should().Throw<InvalidOperationException>().WithMessage("*Validada*");
    }

    [Fact]
    public void VentasFactura_Rechazar_guarda_mensaje_de_error()
    {
        var factura = BuildFactura();
        factura.Rechazar(UserId, "RUC inválido según SRI.");

        factura.Estado.Should().Be("Rechazado");
        factura.MensajeError.Should().Be("RUC inválido según SRI.");
    }

    [Fact]
    public void VentasFactura_MarcarErrorEnvio_cambia_estado_a_ErrorEnvio()
    {
        var factura = BuildFactura();
        factura.MarcarErrorEnvio(UserId, "Timeout al conectar con SRI.");
        factura.Estado.Should().Be("ErrorEnvio");
    }

    [Fact]
    public void VentasFactura_PrepararReintento_resetea_a_Validado()
    {
        var factura = BuildFactura();
        factura.MarcarErrorEnvio(UserId, "Timeout");
        factura.PrepararReintento(UserId);

        factura.Estado.Should().Be("Validado");
        factura.MensajeError.Should().BeNull();
    }

    [Fact]
    public void VentasFactura_PrepararReintento_falla_si_no_es_ErrorEnvio_o_Rechazado()
    {
        var factura = BuildFactura(); // Borrador
        var acto = () => factura.PrepararReintento(UserId);
        acto.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void VentasFactura_Anular_cambia_estado_a_Anulado()
    {
        var factura = BuildFactura();
        factura.Anular(UserId);
        factura.Estado.Should().Be("Anulado");
    }

    [Fact]
    public void VentasFactura_Anular_falla_si_ya_esta_Autorizado()
    {
        var factura = BuildFactura();
        factura.Validar(UserId);
        factura.Autorizar(UserId, "NUM", DateTime.UtcNow, null, null, Guid.NewGuid());

        var acto = () => factura.Anular(UserId);
        acto.Should().Throw<InvalidOperationException>().WithMessage("*Autorizado*");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static VentasFactura BuildFactura() => VentasFactura.Create(
        tenantId:          TenantId,
        sucursalId:        Guid.NewGuid(),
        clienteId:         Guid.NewGuid(),
        bodegaId:          Guid.NewGuid(),
        tipoDocumento:     "01",
        establecimiento:   "001",
        puntoEmision:      "001",
        secuencial:        "000000001",
        claveAcceso:       new string('0', 48) + "0",
        fechaEmision:      DateTime.UtcNow,
        subtotal:          0m,
        impuesto:          0m,
        total:             0m,
        xmlGeneradoPath:   null,
        xmlAutorizacionPath: null,
        numeroAutorizacion:  null,
        fechaAutorizacion:   null,
        mensajeError:        null,
        createdBy:           UserId);
}
