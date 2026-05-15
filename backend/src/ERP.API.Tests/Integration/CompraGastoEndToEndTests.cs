using System.Text;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.UseCases.AprobarCompra;
using ERP.Application.Modules.Purchasing.UseCases.CrearCompra;
using ERP.Application.Modules.Purchasing.UseCases.ValidarCompra;
using ERP.Application.Modules.Expenses.UseCases.AprobarGasto;
using ERP.Application.Modules.Expenses.UseCases.CrearGasto;
using ERP.Application.Modules.Expenses.UseCases.ValidarGasto;
using ERP.Application.Modules.Inventory.UseCases.GetStockActualPorBodega;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

public sealed class CompraGastoEndToEndTests
{
    [Fact]
    public async Task Compra_xml_con_producto_en_asignacion_valida_aprueba_y_actualiza_stock()
    {
        await using var factory = new IntegrationTestWebAppFactory();

        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);

        var xml = IntegrationSeedData.BuildFacturaXml(seed.ClaveAcceso49, seed.ProveedorRuc);

        var crear = await mediator.Send(
            new CrearCompraCommand(
                ModoCreacionCompra.Xml,
                XmlContent: Encoding.UTF8.GetBytes(xml),
                XmlNombreArchivo: "factura.xml",
                ProveedorId: null,
                NumeroFactura: null,
                FechaFactura: null,
                FechaVencimiento: null,
                CondicionPago: null,
                Observaciones: null,
                Detalles: null,
                AsignacionesBodega: new[]
                {
                    new AsignacionBodegaRequest(0, seed.BodegaId, 2m, seed.ProductId),
                }),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);

        var val = await mediator.Send(new ValidarCompraCommand(crear.Value!.Id), CancellationToken.None);
        val.IsSuccess.Should().BeTrue(val.Error);

        var apr = await mediator.Send(new AprobarCompraCommand(crear.Value.Id), CancellationToken.None);
        apr.IsSuccess.Should().BeTrue(apr.Error);

        var stock = await mediator.Send(
            new GetStockActualPorBodegaQuery(seed.BodegaId, seed.ProductId),
            CancellationToken.None);

        stock.IsSuccess.Should().BeTrue();
        stock.Value.Should().NotBeNull();
        stock.Value!.Should().ContainSingle();
        stock.Value[0].Cantidad.Should().Be(2m);
    }

    [Fact]
    public async Task Gasto_manual_bajo_umbral_valida_y_aprueba()
    {
        await using var factory = new IntegrationTestWebAppFactory();

        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);

        var crear = await mediator.Send(
            new CrearGastoCommand(
                ModoCreacionGasto.Manual,
                XmlContent: null,
                XmlNombreArchivo: null,
                ProveedorId: null,
                FechaEmision: DateTime.UtcNow.Date,
                Concepto: "Taxi integración",
                CategoriaGasto: "Viajes",
                Subtotal: 10m,
                Impuesto: 2m,
                Total: 12m,
                Observaciones: null),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);
        crear.Value!.Estado.Should().Be(EstadoGasto.Borrador);

        var val = await mediator.Send(new ValidarGastoCommand(crear.Value.Id), CancellationToken.None);
        val.IsSuccess.Should().BeTrue(val.Error);

        var apr = await mediator.Send(new AprobarGastoCommand(crear.Value.Id), CancellationToken.None);
        apr.IsSuccess.Should().BeTrue(apr.Error);
        apr.Value!.Estado.Should().Be(EstadoGasto.Aprobado);
        apr.Value.AsientoContableId.Should().NotBeNull();
    }
}
