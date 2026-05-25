using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Application.Modules.Commercial.UseCases.ConfirmSalesOrder;
using ERP.Application.Modules.Commercial.UseCases.CreateSalesOrder;
using ERP.Application.Sales.UseCases.CrearVenta;
using ERP.Domain.Modules.Commercial.Entities;
using ERP.Domain.Modules.Fiscal.Entities;
using ERP.Domain.Modules.Integration.Constants;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

public sealed class SalesOrderInvoiceEndToEndTests
{
    [Fact]
    public async Task Pedido_confirmado_factura_crea_relacion_y_marca_invoiced()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockInicial: 10m, ct: CancellationToken.None);

        var clienteId = db.BusinessPartners.First(c => c.SubscriberId == seed.SubscriberId).Id;
        var sucursalId = db.Branches.First(b => b.SubscriberId == seed.SubscriberId).Id;

        var createOrder = await mediator.Send(
            new CreateSalesOrderCommand(
                clienteId,
                seed.WarehouseId,
                RequiredDate: null,
                PaymentTermDays: 0,
                Notes: null,
                Lines: [new SalesOrderLineInputDto(seed.ProductId, 2m, 25m, 12m)]),
            CancellationToken.None);

        createOrder.IsSuccess.Should().BeTrue(createOrder.Error);
        var orderPublicId = createOrder.Value!.PublicId;

        var confirm = await mediator.Send(new ConfirmSalesOrderCommand(orderPublicId), CancellationToken.None);
        confirm.IsSuccess.Should().BeTrue(confirm.Error);

        db.ChangeTracker.Clear();

        var invoiceResult = await mediator.Send(
            new CreateSaleCommand(
                Guid.Empty,
                Guid.Empty,
                sucursalId,
                [],
                SalesOrderPublicId: orderPublicId),
            CancellationToken.None);

        invoiceResult.IsSuccess.Should().BeTrue(invoiceResult.Error);

        db.ChangeTracker.Clear();

        var order = await db.SalesOrders
            .AsNoTracking()
            .FirstAsync(o => o.PublicId == orderPublicId);
        order.Status.Should().Be(SalesOrder.Statuses.Invoiced);

        var invoice = VentasEndToEndHelpers.RequireInvoice(db, invoiceResult.Value);
        invoice.Status.Should().Be(Invoice.Statuses.Draft);
        invoice.BusinessPartnerId.Should().Be(clienteId);
        invoice.WarehouseId.Should().Be(seed.WarehouseId);
        invoice.Lines.Should().HaveCount(1);
        invoice.Lines.First().Quantity.Should().Be(2m);

        var relation = await db.DocumentRelations
            .AsNoTracking()
            .SingleAsync(r =>
                r.SubscriberId == seed.SubscriberId
                && r.SourceModule == DocumentModules.CommercialSalesOrder
                && r.TargetModule == DocumentModules.FiscalInvoice
                && r.RelationType == DocumentRelationTypes.OrderToInvoice
                && r.TargetId == invoice.Id);

        relation.SourceId.Should().Be(order.Id);
    }

    [Fact]
    public async Task Pedido_ya_facturado_rechaza_segunda_factura()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockInicial: 20m, ct: CancellationToken.None);

        var clienteId = db.BusinessPartners.First(c => c.SubscriberId == seed.SubscriberId).Id;
        var sucursalId = db.Branches.First(b => b.SubscriberId == seed.SubscriberId).Id;

        var createOrder = await mediator.Send(
            new CreateSalesOrderCommand(
                clienteId,
                seed.WarehouseId,
                null,
                0,
                null,
                [new SalesOrderLineInputDto(seed.ProductId, 1m, 10m, 12m)]),
            CancellationToken.None);

        var orderPublicId = createOrder.Value!.PublicId;
        await mediator.Send(new ConfirmSalesOrderCommand(orderPublicId), CancellationToken.None);

        db.ChangeTracker.Clear();

        var first = await mediator.Send(
            new CreateSaleCommand(Guid.Empty, Guid.Empty, sucursalId, [], SalesOrderPublicId: orderPublicId),
            CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        var second = await mediator.Send(
            new CreateSaleCommand(Guid.Empty, Guid.Empty, sucursalId, [], SalesOrderPublicId: orderPublicId),
            CancellationToken.None);

        second.IsSuccess.Should().BeFalse();
        second.Error.Should().Contain("already invoiced");
    }
}
