using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Application.Modules.Commercial.UseCases.CancelQuote;
using ERP.Application.Modules.Commercial.UseCases.CancelSalesOrder;
using ERP.Application.Modules.Commercial.UseCases.CreateQuote;
using ERP.Application.Modules.Commercial.UseCases.CreateSalesOrder;
using ERP.Domain.Modules.Commercial.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

public sealed class SalesCommercialCancelEndToEndTests
{
    [Fact]
    public async Task Cotizacion_borrador_se_cancela()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);
        await SalesEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockInicial: 10m, ct: CancellationToken.None);

        var clienteId = db.BusinessPartners.First(c => c.SubscriberId == seed.SubscriberId).Id;
        var validUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

        var createQuote = await mediator.Send(
            new CreateQuoteCommand(
                clienteId,
                validUntil,
                PaymentTermDays: 0,
                Notes: null,
                Lines: [new QuoteLineInputDto(seed.ProductId, 2m, 25m, 12m)]),
            CancellationToken.None);

        createQuote.IsSuccess.Should().BeTrue(createQuote.Error);
        var quotePublicId = createQuote.Value!.PublicId;

        var cancel = await mediator.Send(new CancelQuoteCommand(quotePublicId, "Cliente desistió"), CancellationToken.None);
        cancel.IsSuccess.Should().BeTrue(cancel.Error);
        cancel.Value!.Status.Should().Be(Quote.Statuses.Cancelled);

        db.ChangeTracker.Clear();

        var quote = await db.Quotes.AsNoTracking().FirstAsync(q => q.PublicId == quotePublicId);
        quote.Status.Should().Be(Quote.Statuses.Cancelled);
    }

    [Fact]
    public async Task Pedido_borrador_se_cancela()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);
        await SalesEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockInicial: 10m, ct: CancellationToken.None);

        var clienteId = db.BusinessPartners.First(c => c.SubscriberId == seed.SubscriberId).Id;
        var requiredDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        var createOrder = await mediator.Send(
            new CreateSalesOrderCommand(
                clienteId,
                seed.WarehouseId,
                requiredDate,
                PaymentTermDays: 0,
                Notes: null,
                Lines: [new SalesOrderLineInputDto(seed.ProductId, 1m, 30m, 12m)]),
            CancellationToken.None);

        createOrder.IsSuccess.Should().BeTrue(createOrder.Error);
        var orderPublicId = createOrder.Value!.PublicId;

        var cancel = await mediator.Send(new CancelSalesOrderCommand(orderPublicId, "Pedido duplicado"), CancellationToken.None);
        cancel.IsSuccess.Should().BeTrue(cancel.Error);
        cancel.Value!.Status.Should().Be(SalesOrder.Statuses.Cancelled);

        db.ChangeTracker.Clear();

        var order = await db.SalesOrders.AsNoTracking().FirstAsync(o => o.PublicId == orderPublicId);
        order.Status.Should().Be(SalesOrder.Statuses.Cancelled);
    }
}
