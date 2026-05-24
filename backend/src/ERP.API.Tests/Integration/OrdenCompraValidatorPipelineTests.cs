using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Application.Modules.Purchasing.UseCases.CrearOrdenCompra;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Verifica que el ValidationBehavior del pipeline MediatR lanza ValidationException
/// antes de llegar al handler, cubriendo las reglas del CreatePurchaseOrderCommandValidator.
/// </summary>
public sealed class OrdenCompraValidatorPipelineTests
{
    [Fact]
    public async Task Items_vacio_lanza_ValidationException()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var act = async () => await mediator.Send(new CreatePurchaseOrderCommand(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(10),
            null, null, null,
            Items: []), CancellationToken.None);

        await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*Ã­tem*");
    }

    [Fact]
    public async Task Item_cantidad_cero_lanza_ValidationException()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var act = async () => await mediator.Send(new CreatePurchaseOrderCommand(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(10),
            null, null, null,
            Items: [new PurchaseOrderItemRequest(Guid.NewGuid(), Quantity: 0m, UnitPrice: 10m, VatPct: 15m)]),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*cero*");
    }

    [Fact]
    public async Task ProveedorId_vacio_lanza_ValidationException()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var act = async () => await mediator.Send(new CreatePurchaseOrderCommand(
            BusinessPartnerId: Guid.Empty,
            DateTime.UtcNow.AddDays(10),
            null, null, null,
            Items: [new PurchaseOrderItemRequest(Guid.NewGuid(), 5m, 10m, 15m)]),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*Supplier*");
    }

    [Fact]
    public async Task Item_precio_negativo_lanza_ValidationException()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var act = async () => await mediator.Send(new CreatePurchaseOrderCommand(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(10),
            null, null, null,
            Items: [new PurchaseOrderItemRequest(Guid.NewGuid(), 5m, UnitPrice: -1m, VatPct: 15m)]),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*negativo*");
    }
}







