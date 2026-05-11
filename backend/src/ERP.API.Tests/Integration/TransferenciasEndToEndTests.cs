using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Inventario.UseCases.CancelarTransferencia;
using ERP.Application.Inventario.UseCases.ConfirmarTransferencia;
using ERP.Application.Inventario.UseCases.CrearTransferencia;
using ERP.Application.Modules.Inventario.UseCases.GetStockActualPorBodega;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Pruebas de integración del flujo completo de Transferencias entre bodegas.
/// Verifican que el stock se mueva correctamente al confirmar y que los
/// movimientos de inventario queden registrados.
/// </summary>
public sealed class TransferenciasEndToEndTests
{
    [Fact]
    public async Task ConfirmarTransferencia_mueve_stock_de_origen_a_destino_y_registra_movimientos()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (origenId, destinoId, productoId) = await SeedAsync(db, factory, stockOrigen: 10m);

        // 1. Crear en Borrador
        var crear = await mediator.Send(new CrearTransferenciaCommand(
            origenId, destinoId,
            Motivo: "Reposición bodega destino",
            Observaciones: null,
            Items: [new(productoId, Cantidad: 4m)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);
        var transferenciaId = crear.Value!.Id;
        crear.Value.Estado.Should().Be("Borrador");

        // 2. Confirmar — mueve el stock
        var confirmar = await mediator.Send(
            new ConfirmarTransferenciaCommand(transferenciaId),
            CancellationToken.None);

        confirmar.IsSuccess.Should().BeTrue(confirmar.Error);
        confirmar.Value!.Estado.Should().Be("Confirmado");
        confirmar.Value.FechaConfirmacion.Should().NotBeNull();

        // 3. Verificar stock en bodega origen: 10 - 4 = 6
        var stockOrigenResult = await mediator.Send(
            new GetStockActualPorBodegaQuery(origenId, productoId), CancellationToken.None);
        stockOrigenResult.IsSuccess.Should().BeTrue();
        var itemOrigen = stockOrigenResult.Value!.FirstOrDefault(i => i.ProductoId == productoId);
        itemOrigen.Should().NotBeNull("el producto debe seguir teniendo stock en bodega origen");
        itemOrigen!.CantidadDisponible.Should().Be(6m);

        // 4. Verificar stock en bodega destino: 0 + 4 = 4
        var stockDestinoResult = await mediator.Send(
            new GetStockActualPorBodegaQuery(destinoId, productoId), CancellationToken.None);
        stockDestinoResult.IsSuccess.Should().BeTrue();
        var itemDestino = stockDestinoResult.Value!.FirstOrDefault(i => i.ProductoId == productoId);
        itemDestino.Should().NotBeNull("el producto debe tener stock en bodega destino tras la transferencia");
        itemDestino!.CantidadDisponible.Should().Be(4m);

        // 5. Verificar que se crearon exactamente 2 movimientos para esta transferencia
        var tenantId = factory.MutableTenant.TenantId;
        var movimientos = await db.InventarioMovimientos
            .Where(m => m.TenantId == tenantId
                     && m.DocumentoOrigenId == transferenciaId)
            .ToListAsync(CancellationToken.None);

        movimientos.Should().HaveCount(2, "debe haber un movimiento de salida y uno de entrada");
        movimientos.Should().ContainSingle(m => m.TipoMovimiento == TipoMovimientoInventario.TransferenciaSalida
                                             && m.Cantidad == -4m);
        movimientos.Should().ContainSingle(m => m.TipoMovimiento == TipoMovimientoInventario.TransferenciaEntrada
                                             && m.Cantidad == 4m);
    }

    [Fact]
    public async Task ConfirmarTransferencia_con_stock_insuficiente_retorna_failure_sin_mover_stock()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (origenId, destinoId, productoId) = await SeedAsync(db, factory, stockOrigen: 3m);

        // Crear con 3 unidades (OK para Borrador — valida en Crear)
        var crear = await mediator.Send(new CrearTransferenciaCommand(
            origenId, destinoId,
            Motivo: null,
            Observaciones: null,
            Items: [new(productoId, Cantidad: 3m)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);

        // Consumir el stock manualmente para simular una venta concurrente
        var tenantId = factory.MutableTenant.TenantId;
        var userId   = factory.MutableUser.UserId;
        var stockEntity = db.StockActual.First(s => s.TenantId == tenantId
                                                  && s.BodegaId == origenId
                                                  && s.ProductoId == productoId);
        stockEntity.AplicarMovimiento(-3m, userId); // agota el stock
        await db.SaveChangesAsync(CancellationToken.None);

        // Confirmar ahora debe fallar
        var confirmar = await mediator.Send(
            new ConfirmarTransferenciaCommand(crear.Value!.Id),
            CancellationToken.None);

        confirmar.IsSuccess.Should().BeFalse();
        confirmar.Error.Should().Contain("Stock insuficiente");

        // El stock no debe haberse modificado (rollback efectivo)
        var stockPost = db.StockActual.First(s => s.TenantId == tenantId
                                               && s.BodegaId == origenId
                                               && s.ProductoId == productoId);
        stockPost.CantidadDisponible.Should().Be(0m);

        var movs = db.InventarioMovimientos
            .Where(m => m.TenantId == tenantId && m.DocumentoOrigenId == crear.Value.Id)
            .ToList();
        movs.Should().BeEmpty("no se deben registrar movimientos si la confirmación falló");
    }

    [Fact]
    public async Task CancelarTransferencia_en_borrador_retorna_cancelado_sin_afectar_stock()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (origenId, destinoId, productoId) = await SeedAsync(db, factory, stockOrigen: 10m);

        var crear = await mediator.Send(new CrearTransferenciaCommand(
            origenId, destinoId,
            Motivo: null,
            Observaciones: null,
            Items: [new(productoId, Cantidad: 7m)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);

        var cancelar = await mediator.Send(
            new CancelarTransferenciaCommand(crear.Value!.Id),
            CancellationToken.None);

        cancelar.IsSuccess.Should().BeTrue(cancelar.Error);
        cancelar.Value!.Estado.Should().Be("Cancelado");

        // El stock en la bodega origen debe permanecer intacto
        var tenantId = factory.MutableTenant.TenantId;
        var stock = db.StockActual.First(s => s.TenantId == tenantId
                                           && s.BodegaId == origenId
                                           && s.ProductoId == productoId);
        stock.CantidadDisponible.Should().Be(10m, "cancelar no debe afectar el stock");
    }

    [Fact]
    public async Task ConfirmarTransferencia_ya_confirmada_retorna_failure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (origenId, destinoId, productoId) = await SeedAsync(db, factory, stockOrigen: 10m);

        var crear = await mediator.Send(new CrearTransferenciaCommand(
            origenId, destinoId,
            Motivo: null,
            Observaciones: null,
            Items: [new(productoId, Cantidad: 2m)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);
        var id = crear.Value!.Id;

        var primera = await mediator.Send(new ConfirmarTransferenciaCommand(id), CancellationToken.None);
        primera.IsSuccess.Should().BeTrue(primera.Error);

        var segunda = await mediator.Send(new ConfirmarTransferenciaCommand(id), CancellationToken.None);
        segunda.IsSuccess.Should().BeFalse();
        segunda.Error.Should().Contain("Borrador");
    }

    [Fact]
    public async Task CancelarTransferencia_ya_confirmada_retorna_failure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db           = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator     = scope.ServiceProvider.GetRequiredService<IMediator>();

        var (origenId, destinoId, productoId) = await SeedAsync(db, factory, stockOrigen: 10m);

        var crear = await mediator.Send(new CrearTransferenciaCommand(
            origenId, destinoId,
            Motivo: null,
            Observaciones: null,
            Items: [new(productoId, Cantidad: 2m)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue(crear.Error);
        var id = crear.Value!.Id;

        await mediator.Send(new ConfirmarTransferenciaCommand(id), CancellationToken.None);

        var cancelar = await mediator.Send(new CancelarTransferenciaCommand(id), CancellationToken.None);
        cancelar.IsSuccess.Should().BeFalse();
        cancelar.Error.Should().Contain("Borrador");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static async Task<(Guid OrigenId, Guid DestinoId, Guid ProductoId)>
        SeedAsync(
            ErpDbContext db,
            IntegrationTestWebAppFactory factory,
            decimal stockOrigen)
    {
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);

        var tenantId   = seed.TenantId;
        var userId     = seed.UserId;
        var productoId = seed.ProductId;

        var branchId = db.Branches.First(b => b.TenantId == tenantId).Id;
        var destino  = Bodega.Create(tenantId, branchId, "Bodega Destino E2E", null, null, userId);
        db.Bodegas.Add(destino);
        await db.SaveChangesAsync(CancellationToken.None);

        if (stockOrigen > 0)
        {
            var stock = StockActual.Create(tenantId, productoId, seed.BodegaId, userId);
            stock.AplicarMovimiento(stockOrigen, userId);
            db.StockActual.Add(stock);

            var mov = InventarioMovimiento.Create(
                tenantId, productoId, seed.BodegaId,
                TipoMovimientoInventario.AjustePositivo,
                cantidad:            stockOrigen,
                cantidadAnterior:    0,
                referencia:          "Stock inicial E2E transferencia",
                documentoOrigenId:   null,
                documentoOrigenTipo: null,
                createdBy:           userId);
            db.InventarioMovimientos.Add(mov);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        return (seed.BodegaId, destino.Id, productoId);
    }
}
