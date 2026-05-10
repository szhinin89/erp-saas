using System.Reflection;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Inventario.UseCases.GetKardex;
using ERP.Domain.Bodegas.Entities;
using ERP.Domain.Common;
using ERP.Domain.Inventario.Entities;
using ERP.Domain.Inventario.Enums;
using ERP.Domain.Products.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Pruebas de integración del Kardex valorizado.
/// Verifican el algoritmo de promedio ponderado móvil, el cálculo del saldo inicial,
/// el aislamiento por bodega/producto y el resumen de totales.
/// </summary>
public sealed class KardexInventarioTests
{
    // ── Helpers de datos de prueba ────────────────────────────────────────────

    /// <summary>Modifica CreatedAt vía reflexión (propiedad con private set).</summary>
    private static void SetCreatedAt(AuditableEntity entity, DateTime utc)
        => typeof(AuditableEntity)
            .GetProperty("CreatedAt", BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, DateTime.SpecifyKind(utc, DateTimeKind.Utc));

    private static InventarioMovimiento Entrada(
        Guid tenantId, Guid productoId, Guid bodegaId,
        decimal cantidad, decimal cantAnterior, decimal costoUnitario, Guid userId,
        string? referencia = null,
        DateTime? fecha = null,
        TipoMovimientoInventario tipo = TipoMovimientoInventario.EntradaCompra)
    {
        var m = InventarioMovimiento.Create(
            tenantId, productoId, bodegaId, tipo,
            cantidad, cantAnterior, referencia, null, null, userId, costoUnitario);
        if (fecha.HasValue) SetCreatedAt(m, fecha.Value);
        return m;
    }

    private static InventarioMovimiento Salida(
        Guid tenantId, Guid productoId, Guid bodegaId,
        decimal cantidad, decimal cantAnterior, decimal costoPromedio, Guid userId,
        string? referencia = null,
        DateTime? fecha = null,
        TipoMovimientoInventario tipo = TipoMovimientoInventario.SalidaVenta)
    {
        var m = InventarioMovimiento.Create(
            tenantId, productoId, bodegaId, tipo,
            -cantidad, cantAnterior, referencia, null, null, userId, costoPromedio);
        if (fecha.HasValue) SetCreatedAt(m, fecha.Value);
        return m;
    }

    private static async Task<IntegrationSeedData.SeedResult> SeedBaseAsync(
        ErpDbContext db, IntegrationTestWebAppFactory factory)
        => await IntegrationSeedData.SeedAsync(
            db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);

    // ── Escenario 1: Errores básicos ──────────────────────────────────────────

    [Fact]
    public async Task Kardex_producto_inexistente_retorna_failure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);

        var result = await mediator.Send(
            new GetKardexQuery(Guid.NewGuid(), seed.BodegaId, null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Producto");
    }

    [Fact]
    public async Task Kardex_bodega_inexistente_retorna_failure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);

        var result = await mediator.Send(
            new GetKardexQuery(seed.ProductId, Guid.NewGuid(), null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Bodega");
    }

    // ── Escenario 2: Sin movimientos ──────────────────────────────────────────

    [Fact]
    public async Task Kardex_sin_movimientos_retorna_resumen_en_cero()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);

        var result = await mediator.Send(
            new GetKardexQuery(seed.ProductId, seed.BodegaId, null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;
        k.Movimientos.Should().BeEmpty();
        k.Resumen.InventarioFinalCantidad.Should().Be(0);
        k.Resumen.InventarioFinalValor.Should().Be(0);
        k.Resumen.CostoPromedioFinal.Should().Be(0);
        k.Producto.Id.Should().Be(seed.ProductId);
        k.Bodega.Id.Should().Be(seed.BodegaId);
    }

    // ── Escenario 3: Una sola entrada ─────────────────────────────────────────

    [Fact]
    public async Task Kardex_una_sola_entrada_calcula_saldo_y_promedio_correctos()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, pid, bid, uid) = (seed.TenantId, seed.ProductId, seed.BodegaId, seed.UserId);

        db.InventarioMovimientos.Add(
            Entrada(tid, pid, bid, 10m, 0m, 50m, uid, referencia: "FAC-001"));
        await db.SaveChangesAsync();

        var result = await mediator.Send(
            new GetKardexQuery(pid, bid, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;
        k.Movimientos.Should().HaveCount(1);

        var fila = k.Movimientos[0];
        fila.EntradaCantidad.Should().Be(10m);
        fila.EntradaValor.Should().Be(500m);       // 10 × $50
        fila.SalidaCantidad.Should().Be(0m);
        fila.SalidaValor.Should().Be(0m);
        fila.SaldoCantidad.Should().Be(10m);
        fila.SaldoValor.Should().Be(500m);
        fila.CostoUnitarioPromedio.Should().Be(50m);
        fila.Referencia.Should().Be("FAC-001");
        fila.TipoMovimiento.Should().Be("Compra");

        var r = k.Resumen;
        r.InventarioInicialCantidad.Should().Be(0m);
        r.InventarioInicialValor.Should().Be(0m);
        r.EntradasCantidad.Should().Be(10m);
        r.EntradasValor.Should().Be(500m);
        r.SalidasCantidad.Should().Be(0m);
        r.SalidasValor.Should().Be(0m);
        r.InventarioFinalCantidad.Should().Be(10m);
        r.InventarioFinalValor.Should().Be(500m);
        r.CostoPromedioFinal.Should().Be(50m);
    }

    // ── Escenario 4: Promedio ponderado móvil completo ────────────────────────

    [Fact]
    public async Task Kardex_multiples_entradas_y_salidas_calcula_promedio_ponderado_movil()
    {
        /*
         * Cálculo esperado (promedio ponderado móvil):
         *
         * M1 E=10 @$50.00  → saldo=10 val=$500.000  avg=$50.0000
         * M2 E= 5 @$60.00  → saldo=15 val=$800.000  avg=$53.3333  (800/15)
         * M3 S= 8 @avg     → salida=8×(800/15)=$426.667
         *                    saldo=7  val=$373.333  avg=$53.3333  (promedio no cambia en salidas)
         * M4 E= 3 @$55.00  → saldo=10 val=$538.333  avg=$53.8333  (8075/150)
         * M5 S= 3 @avg     → salida=3×(8075/150)=$161.500
         *                    saldo=7  val=$376.833  avg=$53.8333
         */
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, pid, bid, uid) = (seed.TenantId, seed.ProductId, seed.BodegaId, seed.UserId);

        const decimal avg2 = 800m  / 15m;   // 53.3333...
        const decimal avg4 = 8075m / 150m;  // 53.8333...

        db.InventarioMovimientos.AddRange(
            Entrada(tid, pid, bid, 10m, 0m,  50m,  uid, referencia: "C-001"),
            Entrada(tid, pid, bid,  5m, 10m, 60m,  uid, referencia: "C-002"),
            Salida( tid, pid, bid,  8m, 15m, avg2, uid, referencia: "V-001"),
            Entrada(tid, pid, bid,  3m,  7m, 55m,  uid, referencia: "C-003"),
            Salida( tid, pid, bid,  3m, 10m, avg4, uid, referencia: "V-002"));
        await db.SaveChangesAsync();

        var result = await mediator.Send(
            new GetKardexQuery(pid, bid, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;
        k.Movimientos.Should().HaveCount(5);

        // M1: entrada pura
        var m1 = k.Movimientos[0];
        m1.EntradaCantidad.Should().Be(10m);
        m1.EntradaValor.Should().BeApproximately(500m, 0.001m);
        m1.SalidaCantidad.Should().Be(0m);
        m1.SaldoCantidad.Should().Be(10m);
        m1.SaldoValor.Should().BeApproximately(500m, 0.001m);
        m1.CostoUnitarioPromedio.Should().BeApproximately(50m, 0.001m);

        // M2: nueva entrada recalcula el promedio
        var m2 = k.Movimientos[1];
        m2.EntradaCantidad.Should().Be(5m);
        m2.EntradaValor.Should().BeApproximately(300m, 0.001m);
        m2.SaldoCantidad.Should().Be(15m);
        m2.SaldoValor.Should().BeApproximately(800m, 0.001m);
        m2.CostoUnitarioPromedio.Should().BeApproximately(53.333m, 0.001m);

        // M3: salida al promedio vigente (el promedio NO cambia)
        var m3 = k.Movimientos[2];
        m3.EntradaCantidad.Should().Be(0m);
        m3.SalidaCantidad.Should().Be(8m);
        m3.SalidaValor.Should().BeApproximately(426.667m, 0.001m);  // 8 × 53.333
        m3.SaldoCantidad.Should().Be(7m);
        m3.SaldoValor.Should().BeApproximately(373.333m, 0.001m);
        m3.CostoUnitarioPromedio.Should().BeApproximately(53.333m, 0.001m);

        // M4: nueva entrada recalcula el promedio
        var m4 = k.Movimientos[3];
        m4.EntradaCantidad.Should().Be(3m);
        m4.EntradaValor.Should().BeApproximately(165m, 0.001m);
        m4.SaldoCantidad.Should().Be(10m);
        m4.SaldoValor.Should().BeApproximately(538.333m, 0.001m);
        m4.CostoUnitarioPromedio.Should().BeApproximately(53.833m, 0.001m);

        // M5: salida al nuevo promedio (el promedio permanece igual)
        var m5 = k.Movimientos[4];
        m5.SalidaCantidad.Should().Be(3m);
        m5.SalidaValor.Should().BeApproximately(161.5m, 0.001m);    // 3 × 53.833
        m5.SaldoCantidad.Should().Be(7m);
        m5.SaldoValor.Should().BeApproximately(376.833m, 0.001m);
        m5.CostoUnitarioPromedio.Should().BeApproximately(53.833m, 0.001m);

        // Resumen global
        var r = k.Resumen;
        r.InventarioInicialCantidad.Should().Be(0m);
        r.InventarioInicialValor.Should().Be(0m);
        r.EntradasCantidad.Should().Be(18m);                          // 10+5+3
        r.EntradasValor.Should().BeApproximately(965m, 0.001m);       // 500+300+165
        r.SalidasCantidad.Should().Be(11m);                           // 8+3
        r.SalidasValor.Should().BeApproximately(588.167m, 0.001m);    // 426.667+161.5
        r.InventarioFinalCantidad.Should().Be(7m);
        r.InventarioFinalValor.Should().BeApproximately(376.833m, 0.001m);
        r.CostoPromedioFinal.Should().BeApproximately(53.833m, 0.001m);
    }

    // ── Escenario 5: Saldo inicial con filtro de fecha ────────────────────────

    [Fact]
    public async Task Kardex_filtro_fecha_inicio_calcula_saldo_inicial_del_periodo_previo()
    {
        /*
         * Ayer: E=10@$50, E=5@$60  → saldo previo=15, val=$800, avg=$53.333
         * Hoy:  E=4@$70            → avg=(800+280)/19=1080/19≈$56.842
         * Hoy:  S=6@56.842         → saldoFinal=13, val=1080-6×(1080/19)≈$739.579
         *
         * Query desde hoy:
         *  - InventarioInicial = saldo de ayer = 15 uds, $800
         *  - Period rows = M3 y M4 (hoy)
         */
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, pid, bid, uid) = (seed.TenantId, seed.ProductId, seed.BodegaId, seed.UserId);

        // Usar fechas UTC explícitas para evitar problemas de zona horaria.
        // "ayer" = inicio del día anterior en UTC, "hoy" = inicio del día actual en UTC + 1 hora.
        var hoyUtc  = DateTime.UtcNow.Date;                         // 2026-05-10 00:00:00 UTC
        var ayerUtc = hoyUtc.AddDays(-1).AddHours(12);              // 2026-05-09 12:00:00 UTC (mediodía de ayer)
        var hogUtcNow = hoyUtc.AddHours(1);                         // 2026-05-10 01:00:00 UTC

        // Movimientos de ayer
        var m1 = Entrada(tid, pid, bid, 10m, 0m,  50m, uid, fecha: ayerUtc);
        var m2 = Entrada(tid, pid, bid,  5m, 10m, 60m, uid, fecha: ayerUtc.AddMinutes(5));
        db.InventarioMovimientos.AddRange(m1, m2);
        await db.SaveChangesAsync();

        // Movimientos de hoy
        const decimal avgTrasM3 = 1080m / 19m;  // tras entrada de hoy

        var m3 = Entrada(tid, pid, bid, 4m, 15m, 70m,      uid, fecha: hogUtcNow);
        var m4 = Salida( tid, pid, bid, 6m, 19m, avgTrasM3, uid, fecha: hogUtcNow.AddMinutes(5));
        db.InventarioMovimientos.AddRange(m3, m4);
        await db.SaveChangesAsync();

        // Query desde hoy en UTC (el handler hace SpecifyKind a UTC)
        var result = await mediator.Send(
            new GetKardexQuery(pid, bid, FechaInicio: hoyUtc, FechaFin: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;

        // Saldo inicial viene de M1+M2 (ayer)
        k.Resumen.InventarioInicialCantidad.Should().Be(15m);
        k.Resumen.InventarioInicialValor.Should().BeApproximately(800m, 0.01m);

        // Sólo dos movimientos en el período
        k.Movimientos.Should().HaveCount(2);

        // Fila M3: parte de un saldo inicial de 15@avg=53.333
        var r3 = k.Movimientos[0];
        r3.EntradaCantidad.Should().Be(4m);
        r3.EntradaValor.Should().BeApproximately(280m, 0.01m);   // 4×$70
        r3.SaldoCantidad.Should().Be(19m);
        r3.SaldoValor.Should().BeApproximately(1080m, 0.01m);
        r3.CostoUnitarioPromedio.Should().BeApproximately(1080m / 19m, 0.001m);

        // Fila M4: salida al promedio vigente del período
        var r4 = k.Movimientos[1];
        r4.SalidaCantidad.Should().Be(6m);
        r4.SalidaValor.Should().BeApproximately(6m * (1080m / 19m), 0.01m);
        r4.SaldoCantidad.Should().Be(13m);
    }

    // ── Escenario 6: Dos bodegas independientes ───────────────────────────────

    [Fact]
    public async Task Kardex_dos_bodegas_son_completamente_independientes()
    {
        /*
         * Bodega A: E=20@$40, S=5 (TransferenciaSalida)
         * Bodega B: E=5@$40  (TransferenciaEntrada)
         *
         * Kardex A → 2 filas, saldo=15, val=$600
         * Kardex B → 1 fila,  saldo= 5, val=$200
         */
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, pid, uid) = (seed.TenantId, seed.ProductId, seed.UserId);
        var bidA = seed.BodegaId;

        // Crear segunda bodega
        var bodegaA   = await db.Bodegas.FirstAsync(b => b.Id == bidA);
        var bodegaB   = Bodega.Create(tid, bodegaA.SucursalId, "Bodega Destino", null, null, uid);
        db.Bodegas.Add(bodegaB);
        await db.SaveChangesAsync();
        var bidB = bodegaB.Id;

        db.InventarioMovimientos.AddRange(
            Entrada(tid, pid, bidA, 20m,  0m, 40m, uid),
            Salida( tid, pid, bidA,  5m, 20m, 40m, uid, tipo: TipoMovimientoInventario.TransferenciaSalida),
            Entrada(tid, pid, bidB,  5m,  0m, 40m, uid, tipo: TipoMovimientoInventario.TransferenciaEntrada));
        await db.SaveChangesAsync();

        // Kardex bodega A
        var resA = await mediator.Send(
            new GetKardexQuery(pid, bidA, null, null), CancellationToken.None);

        resA.IsSuccess.Should().BeTrue();
        resA.Value!.Movimientos.Should().HaveCount(2);
        resA.Value!.Movimientos[0].TipoMovimiento.Should().Be("Compra");
        resA.Value!.Movimientos[1].TipoMovimiento.Should().Be("Transferencia salida");
        resA.Value!.Resumen.InventarioFinalCantidad.Should().Be(15m);
        resA.Value!.Resumen.InventarioFinalValor.Should().Be(600m); // 15 × $40

        // Kardex bodega B — solo muestra su propia entrada
        var resB = await mediator.Send(
            new GetKardexQuery(pid, bidB, null, null), CancellationToken.None);

        resB.IsSuccess.Should().BeTrue();
        resB.Value!.Movimientos.Should().HaveCount(1);
        resB.Value!.Movimientos[0].TipoMovimiento.Should().Be("Transferencia entrada");
        resB.Value!.Movimientos[0].EntradaCantidad.Should().Be(5m);
        resB.Value!.Resumen.InventarioFinalCantidad.Should().Be(5m);
        resB.Value!.Resumen.InventarioFinalValor.Should().Be(200m); // 5 × $40
    }

    // ── Escenario 7: Ajuste positivo sin costo ───────────────────────────────

    [Fact]
    public async Task Kardex_ajuste_positivo_sin_costo_agrega_cantidad_sin_alterar_valor()
    {
        /*
         * M1: E=10@$50 → saldo=10, val=$500, avg=$50
         * M2: AjustePositivo=5 (sin costo) → saldo=15, val=$500 (sin cambio en valor)
         *     El nuevo promedio baja: $500/15 = $33.333
         *     (comportamiento esperado: ajuste sin valorización no infla el inventario)
         */
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, pid, bid, uid) = (seed.TenantId, seed.ProductId, seed.BodegaId, seed.UserId);

        db.InventarioMovimientos.Add(
            Entrada(tid, pid, bid, 10m, 0m, 50m, uid));
        await db.SaveChangesAsync();

        // Ajuste positivo sin costo unitario conocido
        var ajuste = InventarioMovimiento.Create(
            tid, pid, bid,
            TipoMovimientoInventario.AjustePositivo,
            cantidad: 5m, cantidadAnterior: 10m,
            referencia: "AJ-001",
            documentoOrigenId: null, documentoOrigenTipo: null,
            createdBy: uid,
            costoUnitario: null);   // sin costo → trata como $0
        db.InventarioMovimientos.Add(ajuste);
        await db.SaveChangesAsync();

        var result = await mediator.Send(
            new GetKardexQuery(pid, bid, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;
        k.Movimientos.Should().HaveCount(2);

        var m2 = k.Movimientos[1];
        m2.TipoMovimiento.Should().Be("Ajuste (+)");
        m2.EntradaCantidad.Should().Be(5m);
        m2.EntradaValor.Should().Be(0m);           // $0 porque no hay costo
        m2.SaldoCantidad.Should().Be(15m);
        m2.SaldoValor.Should().BeApproximately(500m, 0.001m); // sin cambio en valor
        m2.CostoUnitarioPromedio.Should().BeApproximately(500m / 15m, 0.001m); // $33.333
    }

    // ── Escenario 8: Ajuste negativo al costo promedio vigente ───────────────

    [Fact]
    public async Task Kardex_ajuste_negativo_usa_costo_promedio_ponderado_vigente()
    {
        /*
         * M1: E= 6@$30 → saldo=6,  val=$180, avg=$30.00
         * M2: E= 4@$50 → saldo=10, val=$380, avg=$38.00  (6×30+4×50)/10
         * M3: AjusteNegativo=3@$38 (costo promedio actual)
         *      → saldo=7, val=$380-3×$38=$266, avg=$38.00
         */
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, pid, bid, uid) = (seed.TenantId, seed.ProductId, seed.BodegaId, seed.UserId);

        db.InventarioMovimientos.AddRange(
            Entrada(tid, pid, bid, 6m, 0m, 30m, uid),
            Entrada(tid, pid, bid, 4m, 6m, 50m, uid));
        await db.SaveChangesAsync();

        // El promedio tras las dos entradas es $38
        const decimal costoPromedio = 38m;

        var ajusteNeg = InventarioMovimiento.Create(
            tid, pid, bid,
            TipoMovimientoInventario.AjusteNegativo,
            cantidad: -3m, cantidadAnterior: 10m,
            referencia: "AJ-002",
            documentoOrigenId: null, documentoOrigenTipo: null,
            createdBy: uid,
            costoUnitario: costoPromedio); // valorizado al promedio actual
        db.InventarioMovimientos.Add(ajusteNeg);
        await db.SaveChangesAsync();

        var result = await mediator.Send(
            new GetKardexQuery(pid, bid, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;
        k.Movimientos.Should().HaveCount(3);

        // Verificar las dos entradas y el ajuste
        k.Movimientos[0].CostoUnitarioPromedio.Should().Be(30m);
        k.Movimientos[1].CostoUnitarioPromedio.Should().Be(38m);   // (180+200)/10

        var m3 = k.Movimientos[2];
        m3.TipoMovimiento.Should().Be("Ajuste (-)");
        m3.SalidaCantidad.Should().Be(3m);
        m3.SalidaValor.Should().BeApproximately(114m, 0.001m);     // 3 × $38
        m3.SaldoCantidad.Should().Be(7m);
        m3.SaldoValor.Should().BeApproximately(266m, 0.001m);      // $380 - $114
        m3.CostoUnitarioPromedio.Should().BeApproximately(38m, 0.001m); // no cambia

        k.Resumen.InventarioFinalCantidad.Should().Be(7m);
        k.Resumen.InventarioFinalValor.Should().BeApproximately(266m, 0.001m);
    }

    // ── Escenario 9: Etiquetas legibles de tipo movimiento ───────────────────

    [Fact]
    public async Task Kardex_tipos_de_movimiento_tienen_etiquetas_legibles()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, pid, bid, uid) = (seed.TenantId, seed.ProductId, seed.BodegaId, seed.UserId);

        db.InventarioMovimientos.AddRange(
            Entrada(tid, pid, bid, 20m, 0m,  50m, uid, tipo: TipoMovimientoInventario.EntradaCompra),
            Salida( tid, pid, bid,  2m, 20m, 50m, uid, tipo: TipoMovimientoInventario.SalidaVenta),
            Entrada(tid, pid, bid,  5m, 18m, 50m, uid, tipo: TipoMovimientoInventario.TransferenciaEntrada),
            Salida( tid, pid, bid,  3m, 23m, 50m, uid, tipo: TipoMovimientoInventario.TransferenciaSalida),
            Entrada(tid, pid, bid,  2m, 20m, 0m,  uid, tipo: TipoMovimientoInventario.AjustePositivo),
            Salida( tid, pid, bid,  1m, 22m, 50m, uid, tipo: TipoMovimientoInventario.AjusteNegativo),
            Entrada(tid, pid, bid,  1m, 21m, 50m, uid, tipo: TipoMovimientoInventario.DevolucionCompra),
            Salida( tid, pid, bid,  1m, 22m, 50m, uid, tipo: TipoMovimientoInventario.DevolucionVenta));
        await db.SaveChangesAsync();

        var result = await mediator.Send(
            new GetKardexQuery(pid, bid, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var tipos = result.Value!.Movimientos.Select(m => m.TipoMovimiento).ToList();

        tipos.Should().Contain("Compra");
        tipos.Should().Contain("Venta");
        tipos.Should().Contain("Transferencia entrada");
        tipos.Should().Contain("Transferencia salida");
        tipos.Should().Contain("Ajuste (+)");
        tipos.Should().Contain("Ajuste (-)");
        tipos.Should().Contain("Devolución compra");
        tipos.Should().Contain("Devolución venta");
    }

    // ── Escenario 10: Múltiples productos son independientes ─────────────────

    [Fact]
    public async Task Kardex_dos_productos_son_completamente_independientes()
    {
        /*
         * Producto A: 10 uds @ $50 → val=$500
         * Producto B: 20 uds @ $30 → val=$600
         * Kardex de A no debe mezclar movimientos de B y viceversa.
         */
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, uid, bid) = (seed.TenantId, seed.UserId, seed.BodegaId);
        var pidA = seed.ProductId;

        // Crear segundo producto con los mismos catálogos del primero
        var prodA = await db.Products.FirstAsync(p => p.Id == pidA);
        var prodB = Product.Create(
            tid,
            "SKU-INT-B",
            "Prod INT B",
            "Segundo producto de integración",
            prodA.LineId, prodA.CategoryId, prodA.SubcategoryId,
            prodA.UnitOfMeasureId, prodA.BrandId, prodA.ProductTypeId, prodA.TariffId,
            appliesVatOnSale: false, saleTaxId: null, saleVatAccountId: null,
            appliesVatOnPurchase: false, purchaseTaxId: null, purchaseVatAccountId: null,
            uid,
            purchaseCode: "SKU-INT-B",
            isService: false,
            tracksStock: true);
        db.Products.Add(prodB);
        await db.SaveChangesAsync();
        var pidB = prodB.Id;

        db.InventarioMovimientos.AddRange(
            Entrada(tid, pidA, bid, 10m, 0m, 50m, uid),
            Entrada(tid, pidB, bid, 20m, 0m, 30m, uid));
        await db.SaveChangesAsync();

        // Kardex de A
        var resA = await mediator.Send(
            new GetKardexQuery(pidA, bid, null, null), CancellationToken.None);
        resA.IsSuccess.Should().BeTrue();
        resA.Value!.Movimientos.Should().HaveCount(1);
        resA.Value!.Resumen.EntradasCantidad.Should().Be(10m);
        resA.Value!.Resumen.EntradasValor.Should().Be(500m);
        resA.Value!.Resumen.CostoPromedioFinal.Should().Be(50m);

        // Kardex de B
        var resB = await mediator.Send(
            new GetKardexQuery(pidB, bid, null, null), CancellationToken.None);
        resB.IsSuccess.Should().BeTrue();
        resB.Value!.Movimientos.Should().HaveCount(1);
        resB.Value!.Resumen.EntradasCantidad.Should().Be(20m);
        resB.Value!.Resumen.EntradasValor.Should().Be(600m);
        resB.Value!.Resumen.CostoPromedioFinal.Should().Be(30m);
    }
}
