using System.Reflection;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Inventory.UseCases.GetKardex;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Common;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Products.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Pruebas de integraciÃ³n del Kardex valorizado.
/// Verifican el algoritmo de promedio ponderado mÃ³vil, el cÃ¡lculo del saldo inicial,
/// el aislamiento por bodega/producto y el resumen de totales.
/// </summary>
public sealed class KardexInventarioTests
{
    // â”€â”€ Helpers de datos de prueba â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Modifica CreatedAt vÃ­a reflexiÃ³n (propiedad con private set).</summary>
    private static void SetCreatedAt(AuditableEntity entity, DateTime utc)
        => typeof(AuditableEntity)
            .GetProperty("CreatedAt", BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, DateTime.SpecifyKind(utc, DateTimeKind.Utc));

    private static StockMovement Entrada(
        Guid subscriberId, Guid productoId, Guid bodegaId,
        decimal quantity, decimal cantAnterior, decimal unitCost, Guid userId,
        Guid? companyId,
        string? referencia = null,
        DateTime? fecha = null,
        StockMovementType tipo = StockMovementType.PurchaseEntry)
    {
        var m = StockMovement.Create(
            subscriberId, productoId, bodegaId, tipo,
            quantity, cantAnterior, referencia, null, null, userId, unitCost,
            companyId: companyId);
        if (fecha.HasValue) SetCreatedAt(m, fecha.Value);
        return m;
    }

    private static StockMovement Salida(
        Guid subscriberId, Guid productoId, Guid bodegaId,
        decimal quantity, decimal cantAnterior, decimal averageCost, Guid userId,
        Guid? companyId,
        string? referencia = null,
        DateTime? fecha = null,
        StockMovementType tipo = StockMovementType.SaleExit)
    {
        var m = StockMovement.Create(
            subscriberId, productoId, bodegaId, tipo,
            -quantity, cantAnterior, referencia, null, null, userId, averageCost,
            companyId: companyId);
        if (fecha.HasValue) SetCreatedAt(m, fecha.Value);
        return m;
    }

    private static async Task<IntegrationSeedData.SeedResult> SeedBaseAsync(
        ErpDbContext db, IntegrationTestWebAppFactory factory)
    {
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);
        JobCompanyContext.Current = seed.CompanyId;
        return seed;
    }

    // â”€â”€ Escenario 1: Errores bÃ¡sicos â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Kardex_producto_inexistente_retorna_failure()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);

        var result = await mediator.Send(
            new GetKardexQuery(Guid.NewGuid(), seed.WarehouseId, null, null),
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
        result.Error.Should().Contain("Warehouse");
    }

    // â”€â”€ Escenario 2: Sin movimientos â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Kardex_sin_movimientos_retorna_resumen_en_cero()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);

        var result = await mediator.Send(
            new GetKardexQuery(seed.ProductId, seed.WarehouseId, null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;
        k.Rows.Should().BeEmpty();
        k.Resumen.ClosingQuantity.Should().Be(0);
        k.Resumen.ClosingValue.Should().Be(0);
        k.Resumen.FinalAverageCost.Should().Be(0);
        k.Producto.Id.Should().Be(seed.ProductId);
        k.Warehouse.Id.Should().Be(seed.WarehouseId);
    }

    // â”€â”€ Escenario 3: Una sola entrada â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Kardex_una_sola_entrada_calcula_saldo_y_promedio_correctos()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, pid, bid, uid, cid) = (seed.SubscriberId, seed.ProductId, seed.WarehouseId, seed.UserId, seed.CompanyId);

        db.StockMovements.Add(
            Entrada(tid, pid, bid, 10m, 0m, 50m, uid, cid, referencia: "FAC-001"));
        await db.SaveChangesAsync();

        var result = await mediator.Send(
            new GetKardexQuery(pid, bid, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;
        k.Rows.Should().HaveCount(1);

        var fila = k.Rows[0];
        fila.InboundQuantity.Should().Be(10m);
        fila.InboundValue.Should().Be(500m);       // 10 Ã— $50
        fila.OutboundQuantity.Should().Be(0m);
        fila.OutboundValue.Should().Be(0m);
        fila.BalanceQuantity.Should().Be(10m);
        fila.BalanceValue.Should().Be(500m);
        fila.AverageUnitCost.Should().Be(50m);
        fila.Referencia.Should().Be("FAC-001");
        fila.MovementType.Should().Be("Compra");

        var r = k.Resumen;
        r.OpeningQuantity.Should().Be(0m);
        r.OpeningValue.Should().Be(0m);
        r.TotalInboundQuantity.Should().Be(10m);
        r.TotalInboundValue.Should().Be(500m);
        r.TotalOutboundQuantity.Should().Be(0m);
        r.TotalOutboundValue.Should().Be(0m);
        r.ClosingQuantity.Should().Be(10m);
        r.ClosingValue.Should().Be(500m);
        r.FinalAverageCost.Should().Be(50m);
    }

    // â”€â”€ Escenario 4: Promedio ponderado mÃ³vil completo â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Kardex_multiples_entradas_y_salidas_calcula_promedio_ponderado_movil()
    {
        /*
         * CÃ¡lculo esperado (promedio ponderado mÃ³vil):
         *
         * M1 E=10 @$50.00  â†’ saldo=10 val=$500.000  avg=$50.0000
         * M2 E= 5 @$60.00  â†’ saldo=15 val=$800.000  avg=$53.3333  (800/15)
         * M3 S= 8 @avg     â†’ salida=8Ã—(800/15)=$426.667
         *                    saldo=7  val=$373.333  avg=$53.3333  (promedio no cambia en salidas)
         * M4 E= 3 @$55.00  â†’ saldo=10 val=$538.333  avg=$53.8333  (8075/150)
         * M5 S= 3 @avg     â†’ salida=3Ã—(8075/150)=$161.500
         *                    saldo=7  val=$376.833  avg=$53.8333
         */
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, pid, bid, uid, cid) = (seed.SubscriberId, seed.ProductId, seed.WarehouseId, seed.UserId, seed.CompanyId);

        const decimal avg2 = 800m  / 15m;   // 53.3333...
        const decimal avg4 = 8075m / 150m;  // 53.8333...

        db.StockMovements.AddRange(
            Entrada(tid, pid, bid, 10m, 0m,  50m,  uid, cid, referencia: "C-001"),
            Entrada(tid, pid, bid,  5m, 10m, 60m,  uid, cid, referencia: "C-002"),
            Salida( tid, pid, bid,  8m, 15m, avg2, uid, cid, referencia: "V-001"),
            Entrada(tid, pid, bid,  3m,  7m, 55m,  uid, cid, referencia: "C-003"),
            Salida( tid, pid, bid,  3m, 10m, avg4, uid, cid, referencia: "V-002"));
        await db.SaveChangesAsync();

        var result = await mediator.Send(
            new GetKardexQuery(pid, bid, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;
        k.Rows.Should().HaveCount(5);

        // M1: entrada pura
        var m1 = k.Rows[0];
        m1.InboundQuantity.Should().Be(10m);
        m1.InboundValue.Should().BeApproximately(500m, 0.001m);
        m1.OutboundQuantity.Should().Be(0m);
        m1.BalanceQuantity.Should().Be(10m);
        m1.BalanceValue.Should().BeApproximately(500m, 0.001m);
        m1.AverageUnitCost.Should().BeApproximately(50m, 0.001m);

        // M2: nueva entrada recalcula el promedio
        var m2 = k.Rows[1];
        m2.InboundQuantity.Should().Be(5m);
        m2.InboundValue.Should().BeApproximately(300m, 0.001m);
        m2.BalanceQuantity.Should().Be(15m);
        m2.BalanceValue.Should().BeApproximately(800m, 0.001m);
        m2.AverageUnitCost.Should().BeApproximately(53.333m, 0.001m);

        // M3: salida al promedio vigente (el promedio NO cambia)
        var m3 = k.Rows[2];
        m3.InboundQuantity.Should().Be(0m);
        m3.OutboundQuantity.Should().Be(8m);
        m3.OutboundValue.Should().BeApproximately(426.667m, 0.001m);  // 8 Ã— 53.333
        m3.BalanceQuantity.Should().Be(7m);
        m3.BalanceValue.Should().BeApproximately(373.333m, 0.001m);
        m3.AverageUnitCost.Should().BeApproximately(53.333m, 0.001m);

        // M4: nueva entrada recalcula el promedio
        var m4 = k.Rows[3];
        m4.InboundQuantity.Should().Be(3m);
        m4.InboundValue.Should().BeApproximately(165m, 0.001m);
        m4.BalanceQuantity.Should().Be(10m);
        m4.BalanceValue.Should().BeApproximately(538.333m, 0.001m);
        m4.AverageUnitCost.Should().BeApproximately(53.833m, 0.001m);

        // M5: salida al nuevo promedio (el promedio permanece igual)
        var m5 = k.Rows[4];
        m5.OutboundQuantity.Should().Be(3m);
        m5.OutboundValue.Should().BeApproximately(161.5m, 0.001m);    // 3 Ã— 53.833
        m5.BalanceQuantity.Should().Be(7m);
        m5.BalanceValue.Should().BeApproximately(376.833m, 0.001m);
        m5.AverageUnitCost.Should().BeApproximately(53.833m, 0.001m);

        // Resumen global
        var r = k.Resumen;
        r.OpeningQuantity.Should().Be(0m);
        r.OpeningValue.Should().Be(0m);
        r.TotalInboundQuantity.Should().Be(18m);                          // 10+5+3
        r.TotalInboundValue.Should().BeApproximately(965m, 0.001m);       // 500+300+165
        r.TotalOutboundQuantity.Should().Be(11m);                           // 8+3
        r.TotalOutboundValue.Should().BeApproximately(588.167m, 0.001m);    // 426.667+161.5
        r.ClosingQuantity.Should().Be(7m);
        r.ClosingValue.Should().BeApproximately(376.833m, 0.001m);
        r.FinalAverageCost.Should().BeApproximately(53.833m, 0.001m);
    }

    // â”€â”€ Escenario 5: Saldo inicial con filtro de fecha â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Kardex_filtro_fecha_inicio_calcula_saldo_inicial_del_periodo_previo()
    {
        /*
         * Ayer: E=10@$50, E=5@$60  â†’ saldo previo=15, val=$800, avg=$53.333
         * Hoy:  E=4@$70            â†’ avg=(800+280)/19=1080/19â‰ˆ$56.842
         * Hoy:  S=6@56.842         â†’ saldoFinal=13, val=1080-6Ã—(1080/19)â‰ˆ$739.579
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
        var (tid, pid, bid, uid, cid) = (seed.SubscriberId, seed.ProductId, seed.WarehouseId, seed.UserId, seed.CompanyId);

        // Usar fechas UTC explÃ­citas para evitar problemas de zona horaria.
        // "ayer" = inicio del dÃ­a anterior en UTC, "hoy" = inicio del dÃ­a actual en UTC + 1 hora.
        var hoyUtc  = DateTime.UtcNow.Date;                         // 2026-05-10 00:00:00 UTC
        var ayerUtc = hoyUtc.AddDays(-1).AddHours(12);              // 2026-05-09 12:00:00 UTC (mediodÃ­a de ayer)
        var hogUtcNow = hoyUtc.AddHours(1);                         // 2026-05-10 01:00:00 UTC

        // Movimientos de ayer
        var m1 = Entrada(tid, pid, bid, 10m, 0m,  50m, uid, cid, fecha: ayerUtc);
        var m2 = Entrada(tid, pid, bid,  5m, 10m, 60m, uid, cid, fecha: ayerUtc.AddMinutes(5));
        db.StockMovements.AddRange(m1, m2);
        await db.SaveChangesAsync();

        // Movimientos de hoy
        const decimal avgTrasM3 = 1080m / 19m;  // tras entrada de hoy

        var m3 = Entrada(tid, pid, bid, 4m, 15m, 70m,      uid, cid, fecha: hogUtcNow);
        var m4 = Salida( tid, pid, bid, 6m, 19m, avgTrasM3, uid, cid, fecha: hogUtcNow.AddMinutes(5));
        db.StockMovements.AddRange(m3, m4);
        await db.SaveChangesAsync();

        // Query desde hoy en UTC (el handler hace SpecifyKind a UTC)
        var result = await mediator.Send(
            new GetKardexQuery(pid, bid, DateFrom: hoyUtc, DateTo: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;

        // Saldo inicial viene de M1+M2 (ayer)
        k.Resumen.OpeningQuantity.Should().Be(15m);
        k.Resumen.OpeningValue.Should().BeApproximately(800m, 0.01m);

        // SÃ³lo dos movimientos en el perÃ­odo
        k.Rows.Should().HaveCount(2);

        // Fila M3: parte de un saldo inicial de 15@avg=53.333
        var r3 = k.Rows[0];
        r3.InboundQuantity.Should().Be(4m);
        r3.InboundValue.Should().BeApproximately(280m, 0.01m);   // 4Ã—$70
        r3.BalanceQuantity.Should().Be(19m);
        r3.BalanceValue.Should().BeApproximately(1080m, 0.01m);
        r3.AverageUnitCost.Should().BeApproximately(1080m / 19m, 0.001m);

        // Fila M4: salida al promedio vigente del perÃ­odo
        var r4 = k.Rows[1];
        r4.OutboundQuantity.Should().Be(6m);
        r4.OutboundValue.Should().BeApproximately(6m * (1080m / 19m), 0.01m);
        r4.BalanceQuantity.Should().Be(13m);
    }

    // â”€â”€ Escenario 6: Dos bodegas independientes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Kardex_dos_bodegas_son_completamente_independientes()
    {
        /*
         * Warehouse A: E=20@$40, S=5 (TransferenciaSalida)
         * Warehouse B: E=5@$40  (TransferenciaEntrada)
         *
         * Kardex A â†’ 2 filas, saldo=15, val=$600
         * Kardex B â†’ 1 fila,  saldo= 5, val=$200
         */
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, pid, uid, cid) = (seed.SubscriberId, seed.ProductId, seed.UserId, seed.CompanyId);
        var bidA = seed.WarehouseId;

        // Crear segunda bodega
        var bodegaA   = await db.Warehouses.FirstAsync(b => b.Id == bidA);
        var bodegaB = WarehouseTestHelpers.CreateSecondary(
            tid, bodegaA.BranchId, "Warehouse Destino", "WBD", uid, seed.CompanyId);
        db.Warehouses.Add(bodegaB);
        await db.SaveChangesAsync();
        var bidB = bodegaB.Id;

        db.StockMovements.AddRange(
            Entrada(tid, pid, bidA, 20m,  0m, 40m, uid, cid),
            Salida( tid, pid, bidA,  5m, 20m, 40m, uid, cid, tipo: StockMovementType.TransferExit),
            Entrada(tid, pid, bidB,  5m,  0m, 40m, uid, cid, tipo: StockMovementType.TransferEntry));
        await db.SaveChangesAsync();

        // Kardex bodega A
        var resA = await mediator.Send(
            new GetKardexQuery(pid, bidA, null, null), CancellationToken.None);

        resA.IsSuccess.Should().BeTrue();
        resA.Value!.Rows.Should().HaveCount(2);
        resA.Value!.Rows[0].MovementType.Should().Be("Compra");
        resA.Value!.Rows[1].MovementType.Should().Be("transfer salida");
        resA.Value!.Resumen.ClosingQuantity.Should().Be(15m);
        resA.Value!.Resumen.ClosingValue.Should().Be(600m); // 15 Ã— $40

        // Kardex bodega B â€” solo muestra su propia entrada
        var resB = await mediator.Send(
            new GetKardexQuery(pid, bidB, null, null), CancellationToken.None);

        resB.IsSuccess.Should().BeTrue();
        resB.Value!.Rows.Should().HaveCount(1);
        resB.Value!.Rows[0].MovementType.Should().Be("transfer entrada");
        resB.Value!.Rows[0].InboundQuantity.Should().Be(5m);
        resB.Value!.Resumen.ClosingQuantity.Should().Be(5m);
        resB.Value!.Resumen.ClosingValue.Should().Be(200m); // 5 Ã— $40
    }

    // â”€â”€ Escenario 7: Ajuste positivo sin costo â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Kardex_ajuste_positivo_sin_costo_agrega_cantidad_sin_alterar_valor()
    {
        /*
         * M1: E=10@$50 â†’ saldo=10, val=$500, avg=$50
         * M2: AjustePositivo=5 (sin costo) â†’ saldo=15, val=$500 (sin cambio en valor)
         *     El nuevo promedio baja: $500/15 = $33.333
         *     (comportamiento esperado: ajuste sin valorizaciÃ³n no infla el inventario)
         */
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, pid, bid, uid, cid) = (seed.SubscriberId, seed.ProductId, seed.WarehouseId, seed.UserId, seed.CompanyId);

        db.StockMovements.Add(
            Entrada(tid, pid, bid, 10m, 0m, 50m, uid, cid));
        await db.SaveChangesAsync();

        // Ajuste positivo sin costo unitario conocido
        var ajuste = StockMovement.Create(
            tid, pid, bid,
            StockMovementType.PositiveAdjust,
            quantity: 5m, previousQuantity: 10m,
            reference: "AJ-001",
            sourceDocId: null, sourceDocType: null,
            createdBy: uid,
            unitCost: null,
            companyId: seed.CompanyId);
        db.StockMovements.Add(ajuste);
        await db.SaveChangesAsync();

        var result = await mediator.Send(
            new GetKardexQuery(pid, bid, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;
        k.Rows.Should().HaveCount(2);

        var m2 = k.Rows[1];
        m2.MovementType.Should().Be("Ajuste (+)");
        m2.InboundQuantity.Should().Be(5m);
        m2.InboundValue.Should().Be(0m);           // $0 porque no hay costo
        m2.BalanceQuantity.Should().Be(15m);
        m2.BalanceValue.Should().BeApproximately(500m, 0.001m); // sin cambio en valor
        m2.AverageUnitCost.Should().BeApproximately(500m / 15m, 0.001m); // $33.333
    }

    // â”€â”€ Escenario 8: Ajuste negativo al costo promedio vigente â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Kardex_ajuste_negativo_usa_costo_promedio_ponderado_vigente()
    {
        /*
         * M1: E= 6@$30 â†’ saldo=6,  val=$180, avg=$30.00
         * M2: E= 4@$50 â†’ saldo=10, val=$380, avg=$38.00  (6Ã—30+4Ã—50)/10
         * M3: AjusteNegativo=3@$38 (costo promedio actual)
         *      â†’ saldo=7, val=$380-3Ã—$38=$266, avg=$38.00
         */
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, pid, bid, uid, cid) = (seed.SubscriberId, seed.ProductId, seed.WarehouseId, seed.UserId, seed.CompanyId);

        db.StockMovements.AddRange(
            Entrada(tid, pid, bid, 6m, 0m, 30m, uid, cid),
            Entrada(tid, pid, bid, 4m, 6m, 50m, uid, cid));
        await db.SaveChangesAsync();

        // El promedio tras las dos entradas es $38
        const decimal averageCost = 38m;

        var ajusteNeg = StockMovement.Create(
            tid, pid, bid,
            StockMovementType.NegativeAdjust,
            quantity: -3m, previousQuantity: 10m,
            reference: "AJ-002",
            sourceDocId: null, sourceDocType: null,
            createdBy: uid,
            unitCost: averageCost,
            companyId: seed.CompanyId); // valorizado al promedio actual
        db.StockMovements.Add(ajusteNeg);
        await db.SaveChangesAsync();

        var result = await mediator.Send(
            new GetKardexQuery(pid, bid, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var k = result.Value!;
        k.Rows.Should().HaveCount(3);

        // Verificar las dos entradas y el ajuste
        k.Rows[0].AverageUnitCost.Should().Be(30m);
        k.Rows[1].AverageUnitCost.Should().Be(38m);   // (180+200)/10

        var m3 = k.Rows[2];
        m3.MovementType.Should().Be("Ajuste (-)");
        m3.OutboundQuantity.Should().Be(3m);
        m3.OutboundValue.Should().BeApproximately(114m, 0.001m);     // 3 Ã— $38
        m3.BalanceQuantity.Should().Be(7m);
        m3.BalanceValue.Should().BeApproximately(266m, 0.001m);      // $380 - $114
        m3.AverageUnitCost.Should().BeApproximately(38m, 0.001m); // no cambia

        k.Resumen.ClosingQuantity.Should().Be(7m);
        k.Resumen.ClosingValue.Should().BeApproximately(266m, 0.001m);
    }

    // â”€â”€ Escenario 9: Etiquetas legibles de tipo movimiento â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Kardex_tipos_de_movimiento_tienen_etiquetas_legibles()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, pid, bid, uid, cid) = (seed.SubscriberId, seed.ProductId, seed.WarehouseId, seed.UserId, seed.CompanyId);

        db.StockMovements.AddRange(
            Entrada(tid, pid, bid, 20m, 0m,  50m, uid, cid, tipo: StockMovementType.PurchaseEntry),
            Salida( tid, pid, bid,  2m, 20m, 50m, uid, cid, tipo: StockMovementType.SaleExit),
            Entrada(tid, pid, bid,  5m, 18m, 50m, uid, cid, tipo: StockMovementType.TransferEntry),
            Salida( tid, pid, bid,  3m, 23m, 50m, uid, cid, tipo: StockMovementType.TransferExit),
            Entrada(tid, pid, bid,  2m, 20m, 0m,  uid, cid, tipo: StockMovementType.PositiveAdjust),
            Salida( tid, pid, bid,  1m, 22m, 50m, uid, cid, tipo: StockMovementType.NegativeAdjust),
            Entrada(tid, pid, bid,  1m, 21m, 50m, uid, cid, tipo: StockMovementType.PurchaseReturn),
            Salida( tid, pid, bid,  1m, 22m, 50m, uid, cid, tipo: StockMovementType.SaleReturn));
        await db.SaveChangesAsync();

        var result = await mediator.Send(
            new GetKardexQuery(pid, bid, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var tipos = result.Value!.Rows.Select(m => m.MovementType).ToList();

        tipos.Should().Contain("Compra");
        tipos.Should().Contain("Venta");
        tipos.Should().Contain("transfer entrada");
        tipos.Should().Contain("transfer salida");
        tipos.Should().Contain("Ajuste (+)");
        tipos.Should().Contain("Ajuste (-)");
        tipos.Should().Contain("Devolución compra");
        tipos.Should().Contain("Devolución venta");
    }

    // â”€â”€ Escenario 10: MÃºltiples productos son independientes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task Kardex_dos_productos_son_completamente_independientes()
    {
        /*
         * Producto A: 10 uds @ $50 â†’ val=$500
         * Producto B: 20 uds @ $30 â†’ val=$600
         * Kardex de A no debe mezclar movimientos de B y viceversa.
         */
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await SeedBaseAsync(db, factory);
        var (tid, uid, bid, cid) = (seed.SubscriberId, seed.UserId, seed.WarehouseId, seed.CompanyId);
        var pidA = seed.ProductId;

        // Crear segundo producto con los mismos catÃ¡logos del primero
        var prodA = await db.Products.FirstAsync(p => p.Id == pidA);
        var prodB = Product.Create(
            tid,
            "SKU-INT-B",
            "Prod INT B",
            "Segundo producto de integraciÃ³n",
            prodA.LineId, prodA.CategoryId, prodA.SubcategoryId,
            prodA.UomCode, prodA.BrandId, prodA.ProductTypeId, prodA.TariffId,
            appliesVatOnSale: false, saleVatCode: null, saleVatAccountId: null,
            appliesVatOnPurchase: false, purchaseVatCode: null, purchaseVatAccountId: null,
            uid,
            purchaseCode: "SKU-INT-B",
            isService: false,
            tracksStock: true,
            companyId: seed.CompanyId);
        db.Products.Add(prodB);
        await db.SaveChangesAsync();
        var pidB = prodB.Id;

        db.StockMovements.AddRange(
            Entrada(tid, pidA, bid, 10m, 0m, 50m, uid, cid),
            Entrada(tid, pidB, bid, 20m, 0m, 30m, uid, cid));
        await db.SaveChangesAsync();

        // Kardex de A
        var resA = await mediator.Send(
            new GetKardexQuery(pidA, bid, null, null), CancellationToken.None);
        resA.IsSuccess.Should().BeTrue();
        resA.Value!.Rows.Should().HaveCount(1);
        resA.Value!.Resumen.TotalInboundQuantity.Should().Be(10m);
        resA.Value!.Resumen.TotalInboundValue.Should().Be(500m);
        resA.Value!.Resumen.FinalAverageCost.Should().Be(50m);

        // Kardex de B
        var resB = await mediator.Send(
            new GetKardexQuery(pidB, bid, null, null), CancellationToken.None);
        resB.IsSuccess.Should().BeTrue();
        resB.Value!.Rows.Should().HaveCount(1);
        resB.Value!.Resumen.TotalInboundQuantity.Should().Be(20m);
        resB.Value!.Resumen.TotalInboundValue.Should().Be(600m);
        resB.Value!.Resumen.FinalAverageCost.Should().Be(30m);
    }
}












