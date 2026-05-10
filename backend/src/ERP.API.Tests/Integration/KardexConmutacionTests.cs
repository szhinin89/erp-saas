using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Common.Config;
using ERP.Application.Inventario.UseCases.GetKardex;
using ERP.Domain.Bodegas.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Enums;
using ERP.Domain.Inventario.Interfaces;
using ERP.Domain.Products.Interfaces;
using ERP.Infrastructure.Services;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Pruebas de conmutación entre modo simple y escalable del Kardex.
/// Escenarios:
///   1. UseScalableMode=false → calcula sin snapshots (modo original)
///   2. UseScalableMode=true  con snapshot disponible → usa snapshot como saldo inicial
///   3. UseScalableMode=true  con rango > MaxDaysForSync → retorna 202 Accepted + jobId
///   4. UseScalableMode=true  sin snapshots (tabla vacía) → fallback correcto (mismo que simple)
/// </summary>
public sealed class KardexConmutacionTests
{
    // ── Helper: crear un movimiento con fecha controlada ─────────────────────

    private static void SetCreatedAt(AuditableEntity entity, DateTime utc)
        => typeof(AuditableEntity)
            .GetProperty("CreatedAt", BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, DateTime.SpecifyKind(utc, DateTimeKind.Utc));

    private static InventarioMovimiento MovConFecha(
        Guid tenantId, Guid productoId, Guid bodegaId,
        TipoMovimientoInventario tipo, decimal cantidad, decimal cantAnterior,
        decimal? costoUnitario, Guid userId, DateTime fecha)
    {
        var movimientoCantidad = tipo switch
        {
            TipoMovimientoInventario.SalidaVenta => -Math.Abs(cantidad),
            TipoMovimientoInventario.AjusteNegativo => -Math.Abs(cantidad),
            TipoMovimientoInventario.TransferenciaSalida => -Math.Abs(cantidad),
            TipoMovimientoInventario.DevolucionVenta => -Math.Abs(cantidad),
            _ => Math.Abs(cantidad),
        };

        var m = InventarioMovimiento.Create(
            tenantId, productoId, bodegaId, tipo,
            movimientoCantidad, cantAnterior, null, null, null, userId, costoUnitario);
        SetCreatedAt(m, fecha);
        return m;
    }

    // ── Helper: instanciar el handler con opciones específicas ────────────────

    private static GetKardexQueryHandler BuildHandler(
        IServiceScope scope, Guid tenantId, KardexOptions opts)
    {
        return new GetKardexQueryHandler(
            scope.ServiceProvider.GetRequiredService<IInventarioStockRepository>(),
            scope.ServiceProvider.GetRequiredService<IKardexSnapshotRepository>(),
            scope.ServiceProvider.GetRequiredService<IProductRepository>(),
            scope.ServiceProvider.GetRequiredService<IBodegaRepository>(),
            new ManualCurrentTenant(tenantId),
            opts);
    }

    // ── Helper: seed base + ManualCurrentTenant en el scope ──────────────────

    private static async Task<(ErpDbContext Db, IServiceScope Scope, IntegrationSeedData.SeedResult Seed)>
        SeedAsync(IntegrationTestWebAppFactory factory)
    {
        var scope = factory.Services.CreateScope();
        var db    = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var seed  = await IntegrationSeedData.SeedAsync(
            db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);

        // Registrar un ManualCurrentTenant que el handler pueda resolver directamente.
        // (No se pasa por DI convencional — se pasa directo al constructor del handler.)
        scope.ServiceProvider.GetRequiredService<IInventarioStockRepository>(); // warm up scope

        return (db, scope, seed);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ESCENARIO 1 — Modo simple (UseScalableMode = false)
    // El handler NO consulta KardexSnapshot aunque haya snapshots en la tabla.
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ModoSimple_calcula_sin_usar_snapshots_aunque_existan()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (db, scope, seed) = await SeedAsync(factory);
        var (tid, pid, bid, uid) = (seed.TenantId, seed.ProductId, seed.BodegaId, seed.UserId);

        var ayer = DateTime.UtcNow.AddDays(-1);
        var hoy  = DateTime.UtcNow;

        // Movimiento de ayer (queda fuera del período)
        db.InventarioMovimientos.Add(
            MovConFecha(tid, pid, bid, TipoMovimientoInventario.EntradaCompra,
                        10m, 0m, 50m, uid, ayer));
        await db.SaveChangesAsync();

        // Snapshot CON DATOS INCORRECTOS para verificar que no se usa
        var snapFalso = KardexSnapshot.Create(tid, pid, bid, ayer.Date, 999m, 99999m, 99m);
        await scope.ServiceProvider.GetRequiredService<IKardexSnapshotRepository>()
                   .UpsertAsync(snapFalso, CancellationToken.None);
        await db.SaveChangesAsync();

        // Movimiento de hoy (período)
        db.InventarioMovimientos.Add(
            MovConFecha(tid, pid, bid, TipoMovimientoInventario.EntradaCompra,
                        5m, 10m, 60m, uid, hoy));
        await db.SaveChangesAsync();

        // Handler en modo SIMPLE — no debe usar el snapshot falso
        var opts    = new KardexOptions { UseScalableMode = false };
        var handler = BuildHandler(scope, tid, opts);
        var manual  = new ManualCurrentTenant(tid);

        var handlerConManualTenant = new GetKardexQueryHandler(
            scope.ServiceProvider.GetRequiredService<IInventarioStockRepository>(),
            scope.ServiceProvider.GetRequiredService<IKardexSnapshotRepository>(),
            scope.ServiceProvider.GetRequiredService<IProductRepository>(),
            scope.ServiceProvider.GetRequiredService<IBodegaRepository>(),
            manual, opts);

        var result = await handlerConManualTenant.Handle(
            new GetKardexQuery(pid, bid, FechaInicio: DateTime.Today, FechaFin: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;

        // Saldo inicial = movimiento real de ayer (10 uds @ $50 = $500 promedio $50)
        // NO los datos del snapshot falso (999, 99999)
        k.Resumen.InventarioInicialCantidad.Should().Be(10m,
            "el modo simple recorre el historial real, no el snapshot");
        k.Resumen.InventarioInicialValor.Should().BeApproximately(500m, 0.01m);

        // Solo 1 movimiento en el período (hoy)
        k.Movimientos.Should().HaveCount(1);
        k.Movimientos[0].EntradaCantidad.Should().Be(5m);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ESCENARIO 2 — Modo escalable con snapshot disponible
    // El handler DEBE usar el snapshot como saldo inicial (O(período), no O(total)).
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ModoEscalable_con_snapshot_usa_saldo_del_snapshot_como_punto_de_partida()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (db, scope, seed) = await SeedAsync(factory);
        var (tid, pid, bid, uid) = (seed.TenantId, seed.ProductId, seed.BodegaId, seed.UserId);

        var hoyUtc = DateTime.UtcNow.Date;
        var ayer   = hoyUtc.AddDays(-1);

        // NO creamos movimientos previos al período — el saldo inicial debe venir del snapshot.
        // Si el handler no usara el snapshot y no hay movimientos previos, saldo inicial = 0.

        // Snapshot para "ayer" con datos conocidos: qty=10, valor=$500, avg=$50
        var snapshot = KardexSnapshot.Create(tid, pid, bid, ayer, 10m, 500m, 50m);
        await scope.ServiceProvider.GetRequiredService<IKardexSnapshotRepository>()
                   .UpsertAsync(snapshot, CancellationToken.None);
        await db.SaveChangesAsync();

        // Movimiento de hoy (período): E=5@$70
        db.InventarioMovimientos.Add(
            MovConFecha(tid, pid, bid, TipoMovimientoInventario.EntradaCompra,
                        5m, 10m, 70m, uid, hoyUtc.AddHours(1)));
        await db.SaveChangesAsync();

        // Handler en modo ESCALABLE
        var opts    = new KardexOptions { UseScalableMode = true };
        var manual  = new ManualCurrentTenant(tid);
        var handler = new GetKardexQueryHandler(
            scope.ServiceProvider.GetRequiredService<IInventarioStockRepository>(),
            scope.ServiceProvider.GetRequiredService<IKardexSnapshotRepository>(),
            scope.ServiceProvider.GetRequiredService<IProductRepository>(),
            scope.ServiceProvider.GetRequiredService<IBodegaRepository>(),
            manual, opts);

        var result = await handler.Handle(
            new GetKardexQuery(pid, bid, FechaInicio: DateTime.Today, FechaFin: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;

        // Saldo inicial PROVIENE DEL SNAPSHOT (10 uds, $500)
        // Si no se usara el snapshot, sería 0 porque no hay movimientos previos.
        k.Resumen.InventarioInicialCantidad.Should().Be(10m,
            "el modo escalable usa el snapshot como punto de partida");
        k.Resumen.InventarioInicialValor.Should().BeApproximately(500m, 0.01m);

        // Movimiento del período: E=5@$70
        // avg = (500 + 5×70) / 15 = 850/15 = 56.667
        k.Movimientos.Should().HaveCount(1);
        var fila = k.Movimientos[0];
        fila.EntradaCantidad.Should().Be(5m);
        fila.EntradaValor.Should().BeApproximately(350m, 0.01m);
        fila.SaldoCantidad.Should().Be(15m);
        fila.SaldoValor.Should().BeApproximately(850m, 0.01m);
        fila.CostoUnitarioPromedio.Should().BeApproximately(850m / 15m, 0.001m);

        k.Resumen.InventarioFinalCantidad.Should().Be(15m);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ESCENARIO 3 — Modo escalable con rango > MaxDaysForSync → 202 Accepted
    // El endpoint HTTP retorna 202 con jobId en lugar del resultado inmediato.
    // ══════════════════════════════════════════════════════════════════════════

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
            db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);

        var token  = TestJwtFactory.CreateSessionJwt(seed.TenantId, seed.UserId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Rango de 200 días >> MaxDaysForSync (90) → debe activar el modo async
        var fechaInicio = DateTime.Today.AddDays(-200).ToString("yyyy-MM-dd");
        var fechaFin    = DateTime.Today.ToString("yyyy-MM-dd");
        var url = $"/api/inventario/kardex?productoId={seed.ProductId}&bodegaId={seed.BodegaId}" +
                  $"&fechaInicio={fechaInicio}&fechaFin={fechaFin}";

        var response = await client.GetAsync(url);

        // El rango supera MaxDaysForSync=90 → debe devolver 202 Accepted
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "un rango de 200 días supera el umbral de 90 días para procesamiento síncrono");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("jobId", "la respuesta debe incluir un identificador del trabajo");
        body.Should().Contain("Pendiente", "el estado inicial del job debe ser Pendiente");

        // Verificar que el reporte fue creado en BD
        var reporteCreado = await db.KardexReportes
            .AnyAsync(r => r.TenantId == seed.TenantId && r.ProductoId == seed.ProductId);
        reporteCreado.Should().BeTrue("debe haberse persistido el registro del job en BD");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ESCENARIO 4 — Modo escalable sin snapshots (fallback transparente)
    // Si no hay snapshots disponibles, el resultado debe ser idéntico al modo simple.
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ModoEscalable_sin_snapshots_produce_mismo_resultado_que_modo_simple()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (db, scope, seed) = await SeedAsync(factory);
        var (tid, pid, bid, uid) = (seed.TenantId, seed.ProductId, seed.BodegaId, seed.UserId);

        var ayer = DateTime.UtcNow.Date.AddDays(-1).AddHours(12);
        var hoy  = DateTime.UtcNow.Date.AddHours(1);

        // Movimientos previos al período (ayer)
        db.InventarioMovimientos.Add(
            MovConFecha(tid, pid, bid, TipoMovimientoInventario.EntradaCompra,
                        10m, 0m, 50m, uid, ayer));
        // Movimiento del período (hoy)
        db.InventarioMovimientos.Add(
            MovConFecha(tid, pid, bid, TipoMovimientoInventario.SalidaVenta,
                        4m, 10m, 50m, uid, hoy));
        await db.SaveChangesAsync();

        // Confirmar que la tabla de snapshots está vacía
        var haySnapshots = await db.KardexSnapshots.AnyAsync();
        haySnapshots.Should().BeFalse("la tabla de snapshots debe estar vacía para este escenario");

        var manual = new ManualCurrentTenant(tid);

        // Ejecutar con modo SIMPLE
        var handlerSimple = new GetKardexQueryHandler(
            scope.ServiceProvider.GetRequiredService<IInventarioStockRepository>(),
            scope.ServiceProvider.GetRequiredService<IKardexSnapshotRepository>(),
            scope.ServiceProvider.GetRequiredService<IProductRepository>(),
            scope.ServiceProvider.GetRequiredService<IBodegaRepository>(),
            manual, new KardexOptions { UseScalableMode = false });

        var resultSimple = await handlerSimple.Handle(
            new GetKardexQuery(pid, bid, FechaInicio: DateTime.Today, FechaFin: null),
            CancellationToken.None);

        // Ejecutar con modo ESCALABLE (sin snapshots → fallback idéntico)
        var handlerScalable = new GetKardexQueryHandler(
            scope.ServiceProvider.GetRequiredService<IInventarioStockRepository>(),
            scope.ServiceProvider.GetRequiredService<IKardexSnapshotRepository>(),
            scope.ServiceProvider.GetRequiredService<IProductRepository>(),
            scope.ServiceProvider.GetRequiredService<IBodegaRepository>(),
            manual, new KardexOptions { UseScalableMode = true });

        var resultScalable = await handlerScalable.Handle(
            new GetKardexQuery(pid, bid, FechaInicio: DateTime.Today, FechaFin: null),
            CancellationToken.None);

        resultSimple.IsSuccess.Should().BeTrue();
        resultScalable.IsSuccess.Should().BeTrue();

        var s = resultSimple.Value!;
        var e = resultScalable.Value!;

        // Los dos modos deben producir exactamente el mismo resultado
        e.Resumen.InventarioInicialCantidad.Should().Be(s.Resumen.InventarioInicialCantidad,
            "sin snapshots el modo escalable aplica el mismo fallback que el simple");
        e.Resumen.InventarioInicialValor.Should().BeApproximately(
            s.Resumen.InventarioInicialValor, 0.001m);
        e.Movimientos.Should().HaveCount(s.Movimientos.Count,
            "ambos modos deben producir el mismo número de filas");
        e.Resumen.InventarioFinalCantidad.Should().Be(s.Resumen.InventarioFinalCantidad);
        e.Resumen.InventarioFinalValor.Should().BeApproximately(
            s.Resumen.InventarioFinalValor, 0.001m);
        e.Resumen.CostoPromedioFinal.Should().BeApproximately(
            s.Resumen.CostoPromedioFinal, 0.001m);

        // Los valores concretos son: saldo inicial=10@$50, período: -4@$50=$200 salida
        s.Resumen.InventarioInicialCantidad.Should().Be(10m);
        s.Resumen.InventarioInicialValor.Should().BeApproximately(500m, 0.01m);
        s.Movimientos.Should().HaveCount(1);
        s.Movimientos[0].SalidaCantidad.Should().Be(4m);
        s.Resumen.InventarioFinalCantidad.Should().Be(6m);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ESCENARIO EXTRA — Rango dentro del umbral NO activa async
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ModoEscalable_rango_dentro_del_umbral_responde_sincrono()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var seed    = await IntegrationSeedData.SeedAsync(
            db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);

        var token  = TestJwtFactory.CreateSessionJwt(seed.TenantId, seed.UserId);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // Rango de 7 días << MaxDaysForSync (90) → debe responder 200 (síncrono)
        var fechaInicio = DateTime.Today.AddDays(-7).ToString("yyyy-MM-dd");
        var fechaFin    = DateTime.Today.ToString("yyyy-MM-dd");
        var url = $"/api/inventario/kardex?productoId={seed.ProductId}&bodegaId={seed.BodegaId}" +
                  $"&fechaInicio={fechaInicio}&fechaFin={fechaFin}";

        var response = await client.GetAsync(url);

        // 7 días < 90 → respuesta síncrona 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "un rango de 7 días no supera el umbral de 90 días, debe responder sincrono");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("movimientos",
            "la respuesta síncrona incluye el kardex completo en JSON");
    }
}
