using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Modules.Compras.DTOs;
using ERP.Application.Modules.Compras.UseCases.CrearOrdenCompra;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Verifica que el ValidationBehavior del pipeline MediatR lanza ValidationException
/// antes de llegar al handler, cubriendo las reglas del CrearOrdenCompraCommandValidator.
/// </summary>
public sealed class OrdenCompraValidatorPipelineTests
{
    [Fact]
    public async Task Items_vacio_lanza_ValidationException()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var act = async () => await mediator.Send(new CrearOrdenCompraCommand(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(10),
            null, null, null,
            Items: []), CancellationToken.None);

        await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*ítem*");
    }

    [Fact]
    public async Task Item_cantidad_cero_lanza_ValidationException()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var act = async () => await mediator.Send(new CrearOrdenCompraCommand(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(10),
            null, null, null,
            Items: [new ItemOrdenCompraRequest(Guid.NewGuid(), Cantidad: 0m, PrecioUnitario: 10m, IvaPorcentaje: 15m)]),
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

        var act = async () => await mediator.Send(new CrearOrdenCompraCommand(
            ProveedorId: Guid.Empty,
            DateTime.UtcNow.AddDays(10),
            null, null, null,
            Items: [new ItemOrdenCompraRequest(Guid.NewGuid(), 5m, 10m, 15m)]),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*proveedor*");
    }

    [Fact]
    public async Task Item_precio_negativo_lanza_ValidationException()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var act = async () => await mediator.Send(new CrearOrdenCompraCommand(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(10),
            null, null, null,
            Items: [new ItemOrdenCompraRequest(Guid.NewGuid(), 5m, PrecioUnitario: -1m, IvaPorcentaje: 15m)]),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*negativo*");
    }
}
