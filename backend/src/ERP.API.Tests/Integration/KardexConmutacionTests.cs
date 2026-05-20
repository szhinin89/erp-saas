using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ERP.API.Tests.Support;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Application.Inventory.UseCases.GetKardex;
using ERP.Application.Modules.Inventory.Services;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Products.Interfaces;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Pruebas de conmutaciÃ³n entre modo simple y escalable del Kardex.
/// Escenarios:
///   1. UseScalableMode=false â†’ calcula sin snapshots (modo original)
///   2. UseScalableMode=true  con snapshot disponible â†’ usa snapshot como saldo inicial
///   3. UseScalableMode=true  con rango > MaxDaysForSync â†’ retorna 202 Accepted + jobId
///   4. UseScalableMode=true  sin snapshots (tabla vacÃ­a) â†’ fallback correcto (mismo que simple)
/// </summary>
public sealed class KardexConmutacionTests
{
    // â”€â”€ Helper: crear un movimiento con fecha controlada â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static void SetCreatedAt(AuditableEntity entity, DateTime utc)
        => typeof(AuditableEntity)
            .GetProperty("CreatedAt", BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, DateTime.SpecifyKind(utc, DateTimeKind.Utc));

    private static StockMovement MovConFecha(
        Guid subscriberId, Guid productoId, Guid bodegaId,
        StockMovementType tipo, decimal quantity, decimal cantAnterior,
        decimal? unitCost, Guid userId, DateTime fecha)
    {
        var movimientoCantidad = tipo switch
        {
            StockMovementType.SaleExit => -Math.Abs(quantity),
            StockMovementType.NegativeAdjust => -Math.Abs(quantity),
            StockMovementType.TransferExit => -Math.Abs(quantity),
            StockMovementType.SaleReturn => -Math.Abs(quantity),
            _ => Math.Abs(quantity),
        };

        var m = StockMovement.Create(
            subscriberId, productoId, bodegaId, tipo,
            movimientoCantidad, cantAnterior, null, null, null, userId, unitCost);
        SetCreatedAt(m, fecha);
        return m;
    }

    // â”€â”€ Helper: instanciar el handler con opciones especÃ­ficas â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static GetKardexQueryHandler BuildHandler(
        IServiceScope scope, Guid subscriberId, KardexOptions opts)
    {
        var kardex = new KardexService(
            scope.ServiceProvider.GetRequiredService<IStockRepository>(),
            scope.ServiceProvider.GetRequiredService<IKardexSnapshotRepository>(),
            scope.ServiceProvider.GetRequiredService<IProductRepository>(),
            scope.ServiceProvider.GetRequiredService<IWarehouseRepository>(),
            new ManualCurrentSubscriber(subscriberId),
            Options.Create(opts),
            scope.ServiceProvider.GetRequiredService<IKardexMaterializedDailySummariesReader>());
        return new GetKardexQueryHandler(kardex);
    }

    // â”€â”€ Helper: seed base + ManualCurrentSubscriber en el scope â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static async Task<(ErpDbContext Db, IServiceScope Scope, IntegrationSeedData.SeedResult Seed)>
        SeedAsync(IntegrationTestWebAppFactory factory)
    {
        var scope = factory.Services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var seed  = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None);

        // Registrar un ManualCurrentSubscriber que el handler pueda resolver directamente.
        // (No se pasa por DI convencional â€” se pasa directo al constructor del handler.)
        scope.ServiceProvider.GetRequiredService<IStockRepository>(); // warm up scope

        return (db, scope, seed);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ESCENARIO 1 â€” Modo simple (UseScalableMode = false)
    // El handler NO consulta KardexSnapshot aunque haya snapshots en la tabla.
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task ModoSimple_calcula_sin_usar_snapshots_aunque_existan()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (db, scope, seed) = await SeedAsync(factory);
        var (tid, pid, bid, uid) = (seed.SubscriberId, seed.ProductId, seed.WarehouseId, seed.UserId);

        var ayer = DateTime.UtcNow.AddDays(-1);
        var hoy  = DateTime.UtcNow;

        // Movimiento de ayer (queda fuera del perÃ­odo)
        db.StockMovements.Add(
            MovConFecha(tid, pid, bid, StockMovementType.PurchaseEntry,
                        10m, 0m, 50m, uid, ayer));
        await db.SaveChangesAsync();

        // Snapshot CON DATOS INCORRECTOS para verificar que no se usa
        var snapFalso = KardexSnapshot.Create(tid, pid, bid, ayer.Date, 999m, 99999m, 99m);
        await scope.ServiceProvider.GetRequiredService<IKardexSnapshotRepository>()
                   .UpsertAsync(snapFalso, CancellationToken.None);
        await db.SaveChangesAsync();

        // Movimiento de hoy (perÃ­odo)
        db.StockMovements.Add(
            MovConFecha(tid, pid, bid, StockMovementType.PurchaseEntry,
                        5m, 10m, 60m, uid, hoy));
        await db.SaveChangesAsync();

        // Handler en modo SIMPLE â€” no debe usar el snapshot falso
        var opts    = new KardexOptions { UseScalableMode = false };
        var handler = BuildHandler(scope, tid, opts);

        var result = await handler.Handle(
            new GetKardexQuery(pid, bid, DateFrom: DateTime.Today, DateTo: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;

        // Saldo inicial = movimiento real de ayer (10 uds @ $50 = $500 promedio $50)
        // NO los datos del snapshot falso (999, 99999)
        k.Resumen.OpeningQuantity.Should().Be(10m,
            "el modo simple recorre el historial real, no el snapshot");
        k.Resumen.OpeningValue.Should().BeApproximately(500m, 0.01m);

        // Solo 1 movimiento en el perÃ­odo (hoy)
        k.Rows.Should().HaveCount(1);
        k.Rows[0].InboundQuantity.Should().Be(5m);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ESCENARIO 2 â€” Modo escalable con snapshot disponible
    // El handler DEBE usar el snapshot como saldo inicial (O(perÃ­odo), no O(total)).
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task ModoEscalable_con_snapshot_usa_saldo_del_snapshot_como_punto_de_partida()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (db, scope, seed) = await SeedAsync(factory);
        var (tid, pid, bid, uid) = (seed.SubscriberId, seed.ProductId, seed.WarehouseId, seed.UserId);

        var hoyUtc = DateTime.UtcNow.Date;
        var ayer   = hoyUtc.AddDays(-1);

        // NO creamos movimientos previos al perÃ­odo â€” el saldo inicial debe venir del snapshot.
        // Si el handler no usara el snapshot y no hay movimientos previos, saldo inicial = 0.

        // Snapshot para "ayer" con datos conocidos: qty=10, valor=$500, avg=$50
        var snapshot = KardexSnapshot.Create(tid, pid, bid, ayer, 10m, 500m, 50m);
        await scope.ServiceProvider.GetRequiredService<IKardexSnapshotRepository>()
                   .UpsertAsync(snapshot, CancellationToken.None);
        await db.SaveChangesAsync();

        // Movimiento de hoy (perÃ­odo): E=5@$70
        db.StockMovements.Add(
            MovConFecha(tid, pid, bid, StockMovementType.PurchaseEntry,
                        5m, 10m, 70m, uid, hoyUtc.AddHours(1)));
        await db.SaveChangesAsync();

        // Handler en modo ESCALABLE
        var opts    = new KardexOptions { UseScalableMode = true };
        var handler = BuildHandler(scope, tid, opts);

        var result = await handler.Handle(
            new GetKardexQuery(pid, bid, DateFrom: DateTime.Today, DateTo: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;

        // Saldo inicial PROVIENE DEL SNAPSHOT (10 uds, $500)
        // Si no se usara el snapshot, serÃ­a 0 porque no hay movimientos previos.
        k.Resumen.OpeningQuantity.Should().Be(10m,
            "el modo escalable usa el snapshot como punto de partida");
        k.Resumen.OpeningValue.Should().BeApproximately(500m, 0.01m);

        // Movimiento del perÃ­odo: E=5@$70
        // avg = (500 + 5Ã—70) / 15 = 850/15 = 56.667
        k.Rows.Should().HaveCount(1);
        var fila = k.Rows[0];
        fila.InboundQuantity.Should().Be(5m);
        fila.InboundValue.Should().BeApproximately(350m, 0.01m);
        fila.BalanceQuantity.Should().Be(15m);
        fila.BalanceValue.Should().BeApproximately(850m, 0.01m);
        fila.AverageUnitCost.Should().BeApproximately(850m / 15m, 0.001m);

        k.Resumen.ClosingQuantity.Should().Be(15m);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ESCENARIO 3 â€” Modo escalable con rango > MaxDaysForSync â†’ 202 Accepted
    // El endpoint HTTP retorna 202 con jobId en lugar del resultado inmediato.
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task ModoEscalable_rango_grande_retorna_202_con_jobId()
    {
        await using var factory = new IntegrationTestWebAppFactory
        {
            UseScalableMode = true,
        };
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var seed    = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None);

        var token  = TestJwtFactory.CreateSessionJwt(seed.SubscriberId, seed.UserId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Rango de 200 dÃ­as >> MaxDaysForSync (90) â†’ debe activar el modo async
        var fechaInicio = DateTime.Today.AddDays(-200).ToString("yyyy-MM-dd");
        var fechaFin    = DateTime.Today.ToString("yyyy-MM-dd");
        var url = $"/api/inventario/kardex?productoId={seed.ProductId}&bodegaId={seed.WarehouseId}" +
                  $"&fechaInicio={fechaInicio}&fechaFin={fechaFin}";

        var response = await client.GetAsync(url);

        // El rango supera MaxDaysForSync=90 â†’ debe devolver 202 Accepted
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "un rango de 200 dÃ­as supera el umbral de 90 dÃ­as para procesamiento sÃ­ncrono");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("jobId", "la respuesta debe incluir un identificador del trabajo");
        body.Should().Contain("Pendiente", "el estado inicial del job debe ser Pendiente");

        // Verificar que el reporte fue creado en BD
        var reporteCreado = await db.KardexReports
            .AnyAsync(r => r.SubscriberId == seed.SubscriberId && r.ProductId == seed.ProductId);
        reporteCreado.Should().BeTrue("debe haberse persistido el registro del job en BD");
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ESCENARIO 4 â€” Modo escalable sin snapshots (fallback transparente)
    // Si no hay snapshots disponibles, el resultado debe ser idÃ©ntico al modo simple.
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task ModoEscalable_sin_snapshots_produce_mismo_resultado_que_modo_simple()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (db, scope, seed) = await SeedAsync(factory);
        var (tid, pid, bid, uid) = (seed.SubscriberId, seed.ProductId, seed.WarehouseId, seed.UserId);

        var ayer = DateTime.UtcNow.Date.AddDays(-1).AddHours(12);
        var hoy  = DateTime.UtcNow.Date.AddHours(1);

        // Movimientos previos al perÃ­odo (ayer)
        db.StockMovements.Add(
            MovConFecha(tid, pid, bid, StockMovementType.PurchaseEntry,
                        10m, 0m, 50m, uid, ayer));
        // Movimiento del perÃ­odo (hoy)
        db.StockMovements.Add(
            MovConFecha(tid, pid, bid, StockMovementType.SaleExit,
                        4m, 10m, 50m, uid, hoy));
        await db.SaveChangesAsync();

        // Confirmar que la tabla de snapshots estÃ¡ vacÃ­a
        var haySnapshots = await db.KardexSnapshots.AnyAsync();
        haySnapshots.Should().BeFalse("la tabla de snapshots debe estar vacÃ­a para este escenario");

        // Ejecutar con modo SIMPLE
        var handlerSimple = BuildHandler(scope, tid, new KardexOptions { UseScalableMode = false });

        var resultSimple = await handlerSimple.Handle(
            new GetKardexQuery(pid, bid, DateFrom: DateTime.Today, DateTo: null),
            CancellationToken.None);

        // Ejecutar con modo ESCALABLE (sin snapshots â†’ fallback idÃ©ntico)
        var handlerScalable = BuildHandler(scope, tid, new KardexOptions { UseScalableMode = true });

        var resultScalable = await handlerScalable.Handle(
            new GetKardexQuery(pid, bid, DateFrom: DateTime.Today, DateTo: null),
            CancellationToken.None);

        resultSimple.IsSuccess.Should().BeTrue();
        resultScalable.IsSuccess.Should().BeTrue();

        var s = resultSimple.Value!;
        var e = resultScalable.Value!;

        // Los dos modos deben producir exactamente el mismo resultado
        e.Resumen.OpeningQuantity.Should().Be(s.Resumen.OpeningQuantity,
            "sin snapshots el modo escalable aplica el mismo fallback que el simple");
        e.Resumen.OpeningValue.Should().BeApproximately(
            s.Resumen.OpeningValue, 0.001m);
        e.Rows.Should().HaveCount(s.Rows.Count,
            "ambos modos deben producir el mismo nÃºmero de filas");
        e.Resumen.ClosingQuantity.Should().Be(s.Resumen.ClosingQuantity);
        e.Resumen.ClosingValue.Should().BeApproximately(
            s.Resumen.ClosingValue, 0.001m);
        e.Resumen.FinalAverageCost.Should().BeApproximately(
            s.Resumen.FinalAverageCost, 0.001m);

        // Los valores concretos son: saldo inicial=10@$50, perÃ­odo: -4@$50=$200 salida
        s.Resumen.OpeningQuantity.Should().Be(10m);
        s.Resumen.OpeningValue.Should().BeApproximately(500m, 0.01m);
        s.Rows.Should().HaveCount(1);
        s.Rows[0].OutboundQuantity.Should().Be(4m);
        s.Resumen.ClosingQuantity.Should().Be(6m);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ESCENARIO EXTRA â€” Rango dentro del umbral NO activa async
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [Fact]
    public async Task ModoEscalable_rango_dentro_del_umbral_responde_sincrono()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var seed    = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None);

        var token  = TestJwtFactory.CreateSessionJwt(seed.SubscriberId, seed.UserId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Rango de 7 dÃ­as << MaxDaysForSync (90) â†’ debe responder 200 (sÃ­ncrono)
        var fechaInicio = DateTime.Today.AddDays(-7).ToString("yyyy-MM-dd");
        var fechaFin    = DateTime.Today.ToString("yyyy-MM-dd");
        var url = $"/api/inventario/kardex?productoId={seed.ProductId}&bodegaId={seed.WarehouseId}" +
                  $"&fechaInicio={fechaInicio}&fechaFin={fechaFin}";

        var response = await client.GetAsync(url);

        // 7 dÃ­as < 90 â†’ respuesta sÃ­ncrona 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "un rango de 7 dÃ­as no supera el umbral de 90 dÃ­as, debe responder sincrono");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("movimientos",
            "la respuesta sÃ­ncrona incluye el kardex completo en JSON");
    }
}







