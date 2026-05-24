using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Sales.UseCases.AnularFactura;
using ERP.Application.Sales.UseCases.CrearVenta;
using ERP.Application.Sales.UseCases.EmitirFacturaElectronica;
using ERP.Application.Sales.UseCases.GetVentaById;
using ERP.Application.Sales.UseCases.ValidarVenta;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

public sealed class VentasEndToEndTests
{
    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static CreateSaleCommand BuildCrearVenta(
        Guid clienteId, Guid bodegaId, Guid sucursalId, Guid productoId, decimal quantity)
        => new(clienteId, bodegaId, sucursalId,
               new List<SaleItemDto> { new(productoId, quantity, 25.00m) });

    private static async Task<(IMediator Mediator, ErpDbContext Db, IntegrationSeedData.SeedResult Seed, Guid ClienteId, Guid SucursalId)>
        SetupAsync(IntegrationTestWebAppFactory factory, decimal stockInicial = 10m)
    {
        var scope    = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var seed     = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockInicial, ct: CancellationToken.None);

        var clienteId  = db.BusinessPartners.First(c => c.SubscriberId == seed.SubscriberId).Id;
        var sucursalId = db.Branches.First(b => b.SubscriberId == seed.SubscriberId).Id;
        return (mediator, db, seed, clienteId, sucursalId);
    }

    // â”€â”€ Tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Venta_completa_crea_valida_emite_y_descuenta_stock()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId) = await SetupAsync(factory, stockInicial: 10m);

        // 1. Crear venta (2 unidades)
        var crear = await mediator.Send(
            BuildCrearVenta(clienteId, seed.WarehouseId, sucursalId, seed.ProductId, quantity: 2m),
            CancellationToken.None);
        crear.IsSuccess.Should().BeTrue(crear.Error);

        var ventaId = crear.Value;
        var facturaInicial = db.SalesBills.Find(ventaId)!;
        facturaInicial.Status.Should().Be("Borrador");
        facturaInicial.AccessKey.Should().HaveLength(49);
        facturaInicial.Sequential.Should().Be("000000001");

        // 2. Validar
        var validar = await mediator.Send(new ValidateSaleCommand(ventaId), CancellationToken.None);
        validar.IsSuccess.Should().BeTrue(validar.Error);
        db.SalesBills.Find(ventaId)!.Status.Should().Be("Validado");

        // 3. Emitir al SRI (simulado)
        var emitir = await mediator.Send(new IssueElectronicInvoiceCommand(ventaId), CancellationToken.None);
        emitir.IsSuccess.Should().BeTrue(emitir.Error);

        db.Entry(db.SalesBills.Find(ventaId)!).Reload();
        var factura = db.SalesBills.Find(ventaId)!;
        factura.Status.Should().Be("Autorizado");
        factura.AuthNumber.Should().NotBeNullOrEmpty();
        factura.AuthDate.Should().NotBeNull();
        factura.JournalEntryId.Should().NotBeNull("el asiento contable debe haberse creado");

        // 4. Consultar detalle via query
        var query = await mediator.Send(new GetSaleByIdQuery(ventaId), CancellationToken.None);
        query.IsSuccess.Should().BeTrue();
        query.Value!.Lines.Should().HaveCount(1);
        query.Value.Total.Should().Be(50.00m); // 2 Ã— 25.00, sin IVA

        // 5. Stock reducido: 10 - 2 = 8
        var stockFinal = db.CurrentStocks.First(s =>
            s.SubscriberId == seed.SubscriberId && s.ProductId == seed.ProductId && s.WarehouseId == seed.WarehouseId);
        stockFinal.Quantity.Should().Be(8m);

        // 6. Movimiento SalidaVenta registrado
        var movSalida = db.StockMovements
            .Where(m => m.SubscriberId == seed.SubscriberId && m.MovementType == StockMovementType.SaleExit)
            .ToList();
        movSalida.Should().HaveCount(1);
        movSalida[0].Quantity.Should().Be(-2m);
        movSalida[0].ResultQuantity.Should().Be(8m);
        movSalida[0].SourceDocId.Should().Be(ventaId);
    }

    [Fact]
    public async Task Crear_venta_con_stock_insuficiente_retorna_error()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId) = await SetupAsync(factory, stockInicial: 1m);

        var crear = await mediator.Send(
            BuildCrearVenta(clienteId, seed.WarehouseId, sucursalId, seed.ProductId, quantity: 5m),
            CancellationToken.None);

        crear.IsSuccess.Should().BeFalse();
        crear.Error.Should().Contain("Stock insuficiente");
    }

    [Fact]
    public async Task Emitir_factura_ya_autorizada_retorna_error()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId) = await SetupAsync(factory);

        var crear = await mediator.Send(
            BuildCrearVenta(clienteId, seed.WarehouseId, sucursalId, seed.ProductId, 2m),
            CancellationToken.None);
        crear.IsSuccess.Should().BeTrue(crear.Error);
        var ventaId = crear.Value;

        await mediator.Send(new ValidateSaleCommand(ventaId), CancellationToken.None);
        var emitir1 = await mediator.Send(new IssueElectronicInvoiceCommand(ventaId), CancellationToken.None);
        emitir1.IsSuccess.Should().BeTrue(emitir1.Error);

        // Segunda emisiÃ³n debe fallar (ya estÃ¡ Autorizado)
        var emitir2 = await mediator.Send(new IssueElectronicInvoiceCommand(ventaId), CancellationToken.None);
        emitir2.IsSuccess.Should().BeFalse();
        emitir2.Error.Should().Contain("Validada");
    }

    [Fact]
    public async Task Anular_factura_borrador_cambia_estado()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId) = await SetupAsync(factory);

        var crear = await mediator.Send(
            BuildCrearVenta(clienteId, seed.WarehouseId, sucursalId, seed.ProductId, 2m),
            CancellationToken.None);
        crear.IsSuccess.Should().BeTrue(crear.Error);

        var anular = await mediator.Send(new VoidInvoiceCommand(crear.Value), CancellationToken.None);
        anular.IsSuccess.Should().BeTrue(anular.Error);
        db.SalesBills.Find(crear.Value)!.Status.Should().Be("Anulado");

        // Doble anulaciÃ³n debe fallar
        var anular2 = await mediator.Send(new VoidInvoiceCommand(crear.Value), CancellationToken.None);
        anular2.IsSuccess.Should().BeFalse();
    }
}









