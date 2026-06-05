using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Sales.UseCases.VoidInvoice;
using ERP.Application.Sales.UseCases.CreateSale;
using ERP.Application.Sales.UseCases.IssueElectronicInvoice;
using ERP.Application.Sales.UseCases.GetSaleById;
using ERP.Application.Sales.UseCases.ValidateSale;
using ERP.Domain.Modules.Fiscal.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

public sealed class SalesEndToEndTests
{
    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static CreateSaleCommand BuildCreateSale(
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
        await SalesEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockInicial, ct: CancellationToken.None);

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
            BuildCreateSale(clienteId, seed.WarehouseId, sucursalId, seed.ProductId, quantity: 2m),
            CancellationToken.None);
        crear.IsSuccess.Should().BeTrue(crear.Error);

        var SaleId = crear.Value;
        var facturaInicial = SalesEndToEndHelpers.RequireInvoice(db, SaleId);
        facturaInicial.Status.Should().Be(Invoice.Statuses.Draft);
        facturaInicial.AccessKey.Should().HaveLength(49);
        facturaInicial.Sequential.Should().Be("000000001");

        // 2. Validar
        var validar = await mediator.Send(new ValidateSaleCommand(SaleId), CancellationToken.None);
        validar.IsSuccess.Should().BeTrue(validar.Error);
        SalesEndToEndHelpers.RequireInvoice(db, SaleId).Status.Should().Be(Invoice.Statuses.Validated);

        // 3. Emitir al SRI (simulado)
        var emitir = await mediator.Send(new IssueElectronicInvoiceCommand(SaleId), CancellationToken.None);
        emitir.IsSuccess.Should().BeTrue(emitir.Error);

        db.ChangeTracker.Clear();
        var salesBill = SalesEndToEndHelpers.RequireInvoice(db, SaleId);
        salesBill.Status.Should().Be(Invoice.Statuses.Authorized);
        salesBill.Electronic!.AuthorizationNumber.Should().NotBeNullOrEmpty();
        salesBill.Electronic.AuthorizationDate.Should().NotBeNull();
        salesBill.JournalEntryId.Should().NotBeNull("el asiento contable debe haberse creado");

        // 4. Consultar line via query
        var query = await mediator.Send(new GetSaleByIdQuery(SaleId), CancellationToken.None);
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
        movSalida[0].SourceDocId.Should().Be(SaleId);
    }

    [Fact]
    public async Task Crear_venta_con_stock_insuficiente_retorna_error()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId) = await SetupAsync(factory, stockInicial: 1m);

        var crear = await mediator.Send(
            BuildCreateSale(clienteId, seed.WarehouseId, sucursalId, seed.ProductId, quantity: 5m),
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
            BuildCreateSale(clienteId, seed.WarehouseId, sucursalId, seed.ProductId, 2m),
            CancellationToken.None);
        crear.IsSuccess.Should().BeTrue(crear.Error);
        var SaleId = crear.Value;

        await mediator.Send(new ValidateSaleCommand(SaleId), CancellationToken.None);
        var emitir1 = await mediator.Send(new IssueElectronicInvoiceCommand(SaleId), CancellationToken.None);
        emitir1.IsSuccess.Should().BeTrue(emitir1.Error);

        // Segunda emisiÃ³n debe fallar (ya estÃ¡ Autorizado)
        var emitir2 = await mediator.Send(new IssueElectronicInvoiceCommand(SaleId), CancellationToken.None);
        emitir2.IsSuccess.Should().BeFalse();
        emitir2.Error.Should().Contain("Validada");
    }

    [Fact]
    public async Task Anular_factura_borrador_cambia_estado()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId) = await SetupAsync(factory);

        var crear = await mediator.Send(
            BuildCreateSale(clienteId, seed.WarehouseId, sucursalId, seed.ProductId, 2m),
            CancellationToken.None);
        crear.IsSuccess.Should().BeTrue(crear.Error);

        var anular = await mediator.Send(new VoidInvoiceCommand(crear.Value), CancellationToken.None);
        anular.IsSuccess.Should().BeTrue(anular.Error);
        SalesEndToEndHelpers.RequireInvoice(db, crear.Value).Status.Should().Be(Invoice.Statuses.Voided);

        // Doble anulaciÃ³n debe fallar
        var anular2 = await mediator.Send(new VoidInvoiceCommand(crear.Value), CancellationToken.None);
        anular2.IsSuccess.Should().BeFalse();
    }
}









