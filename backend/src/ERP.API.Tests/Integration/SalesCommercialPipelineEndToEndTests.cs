using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Application.Modules.Commercial.UseCases.ApproveQuote;
using ERP.Application.Modules.Commercial.UseCases.ConfirmSalesOrder;
using ERP.Application.Modules.Commercial.UseCases.ConvertQuoteToOrder;
using ERP.Application.Modules.Commercial.UseCases.CreateQuote;
using ERP.Application.Modules.Commercial.UseCases.CreateSalesOrder;
using ERP.Application.Sales.UseCases.CreateSale;
using ERP.Domain.Modules.Commercial.Entities;
using ERP.Domain.Modules.Fiscal.Entities;
using ERP.Domain.Modules.Integration.Constants;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

public sealed class SalesCommercialPipelineEndToEndTests
{
    [Fact]
    public async Task Cotizacion_aprobada_convierte_a_pedido_y_factura_con_relaciones()
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

        var approve = await mediator.Send(new ApproveQuoteCommand(quotePublicId), CancellationToken.None);
        approve.IsSuccess.Should().BeTrue(approve.Error);

        db.ChangeTracker.Clear();

        var convert = await mediator.Send(
            new ConvertQuoteToOrderCommand(quotePublicId, seed.WarehouseId, RequiredDate: validUntil),
            CancellationToken.None);

        convert.IsSuccess.Should().BeTrue(convert.Error);
        var orderPublicId = convert.Value!.PublicId;

        var confirm = await mediator.Send(new ConfirmSalesOrderCommand(orderPublicId), CancellationToken.None);
        confirm.IsSuccess.Should().BeTrue(confirm.Error);

        db.ChangeTracker.Clear();

        var invoiceResult = await mediator.Send(
            new CreateSaleCommand(
                Guid.Empty,
                Guid.Empty,
                seed.CompanyId,
                [],
                SalesOrderPublicId: orderPublicId),
            CancellationToken.None);

        invoiceResult.IsSuccess.Should().BeTrue(invoiceResult.Error);

        db.ChangeTracker.Clear();

        var quote = await db.Quotes.AsNoTracking().FirstAsync(q => q.PublicId == quotePublicId);
        quote.Status.Should().Be(Quote.Statuses.Converted);

        var order = await db.SalesOrders.AsNoTracking().FirstAsync(o => o.PublicId == orderPublicId);
        order.Status.Should().Be(SalesOrder.Statuses.Invoiced);

        var invoice = SalesEndToEndHelpers.RequireInvoice(db, invoiceResult.Value);
        invoice.Status.Should().Be(Invoice.Statuses.Draft);

        var quoteToOrder = await db.DocumentRelations.AsNoTracking()
            .SingleAsync(r =>
                r.SubscriberId == seed.SubscriberId
                && r.SourceModule == DocumentModules.CommercialQuote
                && r.RelationType == DocumentRelationTypes.QuoteToOrder
                && r.SourceId == quote.Id);
        quoteToOrder.TargetId.Should().Be(order.Id);

        var orderToInvoice = await db.DocumentRelations.AsNoTracking()
            .SingleAsync(r =>
                r.SubscriberId == seed.SubscriberId
                && r.SourceModule == DocumentModules.CommercialSalesOrder
                && r.RelationType == DocumentRelationTypes.OrderToInvoice
                && r.SourceId == order.Id);
        orderToInvoice.TargetId.Should().Be(invoice.Id);
    }
}
