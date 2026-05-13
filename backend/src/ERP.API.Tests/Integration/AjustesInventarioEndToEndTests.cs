using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ERP.API.Tests.Support;
using ERP.Application.Inventario.UseCases.CancelarAjuste;
using ERP.Application.Inventario.UseCases.CrearAjuste;
using ERP.Application.Inventario.UseCases.EjecutarAjuste;
using ERP.Application.Modules.Inventario.UseCases.GetStockActualPorBodega;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Enums;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Pruebas de integración del flujo completo de Ajustes de Inventario.
/// Verifican que el stock se actualice correctamente y que el InventarioMovimiento quede registrado.
/// </summary>
public sealed class AjustesInventarioEndToEndTests
{
    [Fact]
    public async Task Ejecutar_incremento_incrementa_stock_y_registra_movimiento_AjustePositivo()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (bodegaId, productoId) = await SeedAsync(db, factory, stockInicial: 100m);

        // Crear ajuste +15 (sobrante)
        var crear = await mediator.Send(new CrearAjusteCommand(
            bodegaId, productoId, CantidadAjuste: +15m,
            Motivo: "Sobrante", Observaciones: null), CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);
        crear.Value!.Estado.Should().Be("Borrador");
        crear.Value.NumeroAjuste.Should().StartWith("AJ-");

        // Ejecutar
        var ejecutar = await mediator.Send(
            new EjecutarAjusteCommand(crear.Value.Id), CancellationToken.None);

        ejecutar.IsSuccess.Should().BeTrue(ejecutar.Error);
        ejecutar.Value!.Estado.Should().Be("Ejecutado");

        // Stock debe ser 115
        var stockResult = await mediator.Send(
            new GetStockActualPorBodegaQuery(bodegaId, productoId), CancellationToken.None);
        stockResult.Value!.First(i => i.ProductoId == productoId).CantidadDisponible.Should().Be(115m);

        // Movimiento registrado
        var tenantId = factory.MutableTenant.TenantId;
        var movs = await db.InventarioMovimientos
            .Where(m => m.TenantId == tenantId && m.DocumentoOrigenId == crear.Value.Id)
            .ToListAsync(CancellationToken.None);

        movs.Should().HaveCount(1);
        movs[0].TipoMovimiento.Should().Be(TipoMovimientoInventario.AjustePositivo);
        movs[0].Cantidad.Should().Be(15m);
        movs[0].CantidadAnterior.Should().Be(100m);
        movs[0].CantidadResultante.Should().Be(115m);
    }

    [Fact]
    public async Task Ejecutar_disminucion_reduce_stock_y_registra_movimiento_AjusteNegativo()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (bodegaId, productoId) = await SeedAsync(db, factory, stockInicial: 50m);

        var crear = await mediator.Send(new CrearAjusteCommand(
            bodegaId, productoId, CantidadAjuste: -10m,
            Motivo: "Merma", Observaciones: "Productos vencidos"), CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);

        var ejecutar = await mediator.Send(
            new EjecutarAjusteCommand(crear.Value!.Id), CancellationToken.None);

        ejecutar.IsSuccess.Should().BeTrue(ejecutar.Error);

        // Stock: 50 - 10 = 40
        var stockResult = await mediator.Send(
            new GetStockActualPorBodegaQuery(bodegaId, productoId), CancellationToken.None);
        stockResult.Value!.First(i => i.ProductoId == productoId).CantidadDisponible.Should().Be(40m);

        var tenantId = factory.MutableTenant.TenantId;
        var mov = (await db.InventarioMovimientos
            .Where(m => m.TenantId == tenantId && m.DocumentoOrigenId == crear.Value.Id)
            .ToListAsync(CancellationToken.None)).Single();

        mov.TipoMovimiento.Should().Be(TipoMovimientoInventario.AjusteNegativo);
        mov.Cantidad.Should().Be(-10m);
    }

    [Fact]
    public async Task Ejecutar_disminucion_sin_stock_suficiente_retorna_failure_sin_mover_stock()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (bodegaId, productoId) = await SeedAsync(db, factory, stockInicial: 5m);

        var crear = await mediator.Send(new CrearAjusteCommand(
            bodegaId, productoId, CantidadAjuste: -5m,
            Motivo: "Ajuste físico", Observaciones: null), CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);

        // Consumir el stock antes de ejecutar (simula concurrencia)
        var tenantId = factory.MutableTenant.TenantId;
        var userId   = factory.MutableUser.UserId;
        var stock = db.StockActual.First(s => s.TenantId == tenantId
                                           && s.BodegaId == bodegaId
                                           && s.ProductoId == productoId);
        stock.AplicarMovimiento(-5m, userId); // agota el stock
        await db.SaveChangesAsync(CancellationToken.None);

        var ejecutar = await mediator.Send(
            new EjecutarAjusteCommand(crear.Value!.Id), CancellationToken.None);

        ejecutar.IsSuccess.Should().BeFalse();
        ejecutar.Error.Should().Contain("Stock insuficiente");

        // No debe haber movimientos registrados
        var movs = await db.InventarioMovimientos
            .Where(m => m.TenantId == tenantId && m.DocumentoOrigenId == crear.Value.Id)
            .ToListAsync(CancellationToken.None);
        movs.Should().BeEmpty("no debe registrar movimientos si falló la ejecución");
    }

    [Fact]
    public async Task CancelarAjuste_en_borrador_no_afecta_stock()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (bodegaId, productoId) = await SeedAsync(db, factory, stockInicial: 30m);

        var crear = await mediator.Send(new CrearAjusteCommand(
            bodegaId, productoId, CantidadAjuste: -10m,
            Motivo: "Robo", Observaciones: null), CancellationToken.None);

        var cancelar = await mediator.Send(
            new CancelarAjusteCommand(crear.Value!.Id), CancellationToken.None);

        cancelar.IsSuccess.Should().BeTrue(cancelar.Error);
        cancelar.Value!.Estado.Should().Be("Cancelado");

        // Stock intacto
        var tenantId = factory.MutableTenant.TenantId;
        var stock = db.StockActual.First(s => s.TenantId == tenantId
                                           && s.BodegaId == bodegaId
                                           && s.ProductoId == productoId);
        stock.CantidadDisponible.Should().Be(30m);
    }

    [Fact]
    public async Task Ejecutar_ajuste_ya_ejecutado_retorna_failure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (bodegaId, productoId) = await SeedAsync(db, factory, stockInicial: 20m);

        var crear = await mediator.Send(new CrearAjusteCommand(
            bodegaId, productoId, +5m, "Ajuste físico", null), CancellationToken.None);

        await mediator.Send(new EjecutarAjusteCommand(crear.Value!.Id), CancellationToken.None);

        // Segundo intento
        var segunda = await mediator.Send(
            new EjecutarAjusteCommand(crear.Value.Id), CancellationToken.None);

        segunda.IsSuccess.Should().BeFalse();
        segunda.Error.Should().Contain("Borrador");
    }

    [Fact]
    public async Task CrearAjuste_con_cantidad_cero_lanza_validacion()
    {
        // FluentValidation lanza ValidationException antes de llegar al handler.
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (bodegaId, productoId) = await SeedAsync(db, factory, stockInicial: 10m);

        var act = async () => await mediator.Send(new CrearAjusteCommand(
            bodegaId, productoId, CantidadAjuste: 0m,
            Motivo: "Ajuste físico", Observaciones: null), CancellationToken.None);

        await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*cero*");
    }

    [Fact]
    public async Task CrearAjuste_con_motivo_vacio_lanza_validacion()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (bodegaId, productoId) = await SeedAsync(db, factory, stockInicial: 10m);

        var act = async () => await mediator.Send(new CrearAjusteCommand(
            bodegaId, productoId, CantidadAjuste: 5m,
            Motivo: "", Observaciones: null), CancellationToken.None);

        await act.Should()
            .ThrowAsync<FluentValidation.ValidationException>()
            .WithMessage("*motivo*");
    }

    // ── Seed ──────────────────────────────────────────────────────────────

    private static async Task<(Guid BodegaId, Guid ProductoId)> SeedAsync(
        ErpDbContext db, IntegrationTestWebAppFactory factory, decimal stockInicial)
    {
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);

        var tenantId   = seed.TenantId;
        var userId     = seed.UserId;
        var productoId = seed.ProductId;
        var bodegaId   = seed.BodegaId;

        if (stockInicial > 0)
        {
            var stock = StockActual.Create(tenantId, productoId, bodegaId, userId);
            stock.AplicarMovimiento(stockInicial, userId);
            db.StockActual.Add(stock);

            db.InventarioMovimientos.Add(InventarioMovimiento.Create(
                tenantId, productoId, bodegaId,
                TipoMovimientoInventario.AjustePositivo,
                cantidad:            stockInicial,
                cantidadAnterior:    0,
                referencia:          "Stock inicial test ajuste",
                documentoOrigenId:   null,
                documentoOrigenTipo: null,
                createdBy:           userId));

            await db.SaveChangesAsync(CancellationToken.None);
        }

        return (bodegaId, productoId);
    }
}
