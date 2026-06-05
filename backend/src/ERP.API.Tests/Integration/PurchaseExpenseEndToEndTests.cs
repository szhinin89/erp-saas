using System.Text;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.UseCases.ApprovePurchase;
using ERP.Application.Modules.Purchasing.UseCases.CreatePurchase;
using ERP.Application.Modules.Purchasing.UseCases.ValidatePurchase;
using ERP.Application.Modules.Expenses.UseCases.ApproveExpense;
using ERP.Application.Modules.Expenses.UseCases.CreateExpense;
using ERP.Application.Modules.Expenses.UseCases.ValidateExpense;
using ERP.Application.Modules.Inventory.UseCases.GetCurrentStockByWarehouse;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

public sealed class PurchaseExpenseEndToEndTests
{
    [Fact]
    public async Task Compra_xml_con_producto_en_asignacion_valida_aprueba_y_actualiza_stock()
    {
        await using var factory = new IntegrationTestWebAppFactory();

        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        var xml = IntegrationSeedData.BuildFacturaXml(seed.ClaveAcceso49, seed.ProveedorRuc);

        var crear = await mediator.Send(
            new CreatePurchaseCommand(
                PurchaseCreationMode.Xml,
                XmlContent: Encoding.UTF8.GetBytes(xml),
                XmlFileName: "salesBill.xml",
                BusinessPartnerId: null,
                InvoiceNumber: null,
                InvoiceDate: null,
                DueDate: null,
                PaymentTerms: null,
                Notes: null,
                Lines: null,
                WarehouseAllocations: new[]
                {
                    new WarehouseAllocationRequest(0, seed.WarehouseId, 2m, seed.ProductId),
                }),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);

        var val = await mediator.Send(new ValidatePurchaseCommand(crear.Value!.Id), CancellationToken.None);
        val.IsSuccess.Should().BeTrue(val.Error);

        var apr = await mediator.Send(new ApprovePurchaseCommand(crear.Value.Id), CancellationToken.None);
        apr.IsSuccess.Should().BeTrue(apr.Error);

        var stock = await mediator.Send(
            new GetCurrentStockByWarehouseQuery(seed.WarehouseId, seed.ProductId),
            CancellationToken.None);

        stock.IsSuccess.Should().BeTrue();
        stock.Value.Should().NotBeNull();
        stock.Value!.Should().ContainSingle();
        stock.Value[0].Quantity.Should().Be(2m);
    }

    [Fact]
    public async Task Gasto_manual_bajo_umbral_valida_y_aprueba()
    {
        await using var factory = new IntegrationTestWebAppFactory();

        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        var crear = await mediator.Send(
            new CreateExpenseCommand(
                ExpenseCreationMode.Manual,
                XmlContent: null,
                XmlFileName: null,
                BusinessPartnerId: null,
                IssueDate: DateTime.UtcNow.Date,
                Concept: "Taxi integraciÃƒÂ³n",
                Category: "Viajes",
                Subtotal: 10m,
                VatTotal: 2m,
                Total: 12m,
                Notes: null),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);
        crear.Value!.Status.Should().Be(ExpenseStatus.Draft);

        var val = await mediator.Send(new ValidateExpenseCommand(crear.Value.Id), CancellationToken.None);
        val.IsSuccess.Should().BeTrue(val.Error);

        var apr = await mediator.Send(new ApproveExpenseCommand(crear.Value.Id), CancellationToken.None);
        apr.IsSuccess.Should().BeTrue(apr.Error);
        apr.Value!.Status.Should().Be(ExpenseStatus.Approved);
        apr.Value.JournalEntryId.Should().NotBeNull();
    }
}








