using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Inventario.UseCases.GetKardex;
using ERP.Application.Modules.Compras.UseCases.AprobarCompra;
using ERP.Application.Modules.Compras.UseCases.CrearCompra;
using ERP.Application.Modules.Compras.UseCases.ValidarCompra;
using ERP.Application.Ventas.UseCases.CrearVenta;
using ERP.Application.Ventas.UseCases.EmitirFacturaElectronica;
using ERP.Application.Ventas.UseCases.ValidarVenta;
using ERP.Domain.Proveedores.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Test E2E que recorre el flujo real de compras y ventas y verifica el Kardex resultante.
/// Replica exactamente el escenario de prueba manual en Swagger:
///   - Compra 10 uds @ $5 → Compra 10 uds @ $7 → Venta 5 uds
///   - Costo promedio esperado: $6, valor salida: $30, stock final: 15 uds a $90
/// </summary>
public sealed class KardexFlujoCompletoTests
{
    // ── Setup ──────────────────────────────────────────────────────────────

    private static async Task<(
        IMediator Mediator,
        ErpDbContext Db,
        IntegrationSeedData.SeedResult Seed,
        Guid ClienteId,
        Guid SucursalId,
        Guid ProveedorId)>
        SetupAsync(IntegrationTestWebAppFactory factory)
    {
        var scope    = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Seed base (tenant, product, bodega, cuentas contables)
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);

        // Seed prerrequisitos de ventas (cliente, SRI, cuenta ingresos)
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(
            db, seed, stockInicial: 0m, crearStockActual: false,
            ct: CancellationToken.None);

        var clienteId  = db.Customers.First(c => c.TenantId == seed.TenantId).Id;
        var sucursalId = db.Branches.First(b => b.TenantId == seed.TenantId).Id;

        // Crear proveedor de prueba
        var proveedor = Proveedor.Create(
            seed.TenantId,
            Proveedor.TipoJuridica,
            "Proveedor E2E S.A.",
            seed.ProveedorRuc,
            correo:       null,
            telefono:     null,
            direccion:    null,
            condicionPago: "Contado",
            createdBy:    seed.UserId);
        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync();

        return (mediator, db, seed, clienteId, sucursalId, proveedor.Id);
    }

    // ── Helper para crear una CompraFactura completa (Borrador→Validado→Aprobado) ──

    private static async Task<Guid> CrearYAprobarCompraAsync(
        IMediator mediator,
        Guid proveedorId,
        Guid productoId,
        Guid bodegaId,
        string numeroFactura,
        decimal cantidad,
        decimal precioUnitario)
    {
        // 1. Crear en estado Borrador (modo Manual, sin XML)
        var crear = await mediator.Send(
            new CrearCompraCommand(
                Modo: ModoCreacionCompra.Manual,
                XmlContent: null, XmlNombreArchivo: null,
                ProveedorId:      proveedorId,
                NumeroFactura:    numeroFactura,
                FechaFactura:     DateTime.Today,
                FechaVencimiento: null,
                CondicionPago:    "Contado",
                Observaciones:    null,
                Detalles: [new DetalleCompraInput(
                    "Producto test kardex", null, productoId,
                    cantidad, precioUnitario, 0m, 0m)],
                AsignacionesBodega: [new AsignacionBodegaRequest(0, bodegaId, cantidad, productoId)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue($"CrearCompra {numeroFactura} falló: {crear.Error}");
        var compraId = crear.Value!.Id;

        // 2. Validar (Borrador → Validado)
        var validar = await mediator.Send(
            new ValidarCompraCommand(compraId), CancellationToken.None);
        validar.IsSuccess.Should().BeTrue($"ValidarCompra {numeroFactura} falló: {validar.Error}");

        // 3. Aprobar (Validado → Aprobado): registra movimiento de inventario con CostoUnitario
        var aprobar = await mediator.Send(
            new AprobarCompraCommand(compraId), CancellationToken.None);
        aprobar.IsSuccess.Should().BeTrue($"AprobarCompra {numeroFactura} falló: {aprobar.Error}");

        return compraId;
    }

    // ── Escenario principal (escenario idéntico al de la prueba manual en Swagger) ──

    [Fact]
    public async Task Flujo_completo_dos_compras_y_una_venta_genera_kardex_correcto()
    {
        /*
         * Escenario:
         *   Compra 1: 10 uds @ $5.00 → saldo=10, val=$50,  avg=$5.000
         *   Compra 2: 10 uds @ $7.00 → saldo=20, val=$120, avg=$6.000  ← (50+70)/20
         *   Venta:     5 uds @ avg   → saldo=15, val=$90,  avg=$6.000
         *   Costo salida = 5 × $6 = $30, saldo valor = $120 - $30 = $90
         *
         * Verificamos: saldo, valor, promedio en cada fila del kardex y el resumen.
         */
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId, proveedorId) = await SetupAsync(factory);

        var tid = seed.TenantId;
        var pid = seed.ProductId;
        var bid = seed.BodegaId;

        // ── COMPRA 1: 10 uds @ $5 ─────────────────────────────────────────
        await CrearYAprobarCompraAsync(
            mediator, proveedorId, pid, bid,
            numeroFactura: "001-001-000000001",
            cantidad:       10m,
            precioUnitario: 5m);

        // Verificación intermedia: stock=10, valor=$50
        var stock1 = await db.StockActual.FirstOrDefaultAsync(
            s => s.TenantId == tid && s.ProductoId == pid && s.BodegaId == bid);
        stock1.Should().NotBeNull();
        stock1!.Cantidad.Should().Be(10m);
        stock1.ValorTotalStock.Should().Be(50m);
        stock1.CostoPromedioActual.Should().Be(5m);

        // ── COMPRA 2: 10 uds @ $7 ─────────────────────────────────────────
        await CrearYAprobarCompraAsync(
            mediator, proveedorId, pid, bid,
            numeroFactura: "001-001-000000002",
            cantidad:       10m,
            precioUnitario: 7m);

        // Verificación intermedia: stock=20, valor=$120, avg=$6
        db.Entry(stock1).Reload();
        stock1.Cantidad.Should().Be(20m);
        stock1.ValorTotalStock.Should().Be(120m);
        stock1.CostoPromedioActual.Should().Be(6m);

        // ── VENTA: 5 uds ──────────────────────────────────────────────────
        var crearVenta = await mediator.Send(
            new CrearVentaCommand(clienteId, bid, sucursalId,
                new List<ItemVentaDto> { new(pid, 5m, 10m) }),   // precio venta $10 (irrelevante para el costo)
            CancellationToken.None);
        crearVenta.IsSuccess.Should().BeTrue(crearVenta.Error);

        await mediator.Send(new ValidarVentaCommand(crearVenta.Value), CancellationToken.None);
        var emitir = await mediator.Send(
            new EmitirFacturaElectronicaCommand(crearVenta.Value), CancellationToken.None);
        emitir.IsSuccess.Should().BeTrue(emitir.Error);

        // Verificación intermedia: stock=15, valor=$90, avg=$6
        db.Entry(stock1).Reload();
        stock1.Cantidad.Should().Be(15m);
        stock1.ValorTotalStock.Should().BeApproximately(90m, 0.001m);
        stock1.CostoPromedioActual.Should().BeApproximately(6m, 0.001m);

        // ── KARDEX: verificación completa ─────────────────────────────────
        var kardex = await mediator.Send(
            new GetKardexQuery(pid, bid, null, null), CancellationToken.None);

        kardex.IsSuccess.Should().BeTrue(kardex.Error);
        var k = kardex.Value!;

        // Debe haber exactamente 3 filas (2 compras + 1 venta)
        k.Movimientos.Should().HaveCount(3, "dos compras + una venta");

        // Fila 1: Compra 10 @ $5
        var f1 = k.Movimientos[0];
        f1.TipoMovimiento.Should().Be("Compra");
        f1.EntradaCantidad.Should().Be(10m);
        f1.EntradaValor.Should().Be(50m);
        f1.SaldoCantidad.Should().Be(10m);
        f1.SaldoValor.Should().Be(50m);
        f1.CostoUnitarioPromedio.Should().Be(5m);

        // Fila 2: Compra 10 @ $7 → promedio sube a $6
        var f2 = k.Movimientos[1];
        f2.TipoMovimiento.Should().Be("Compra");
        f2.EntradaCantidad.Should().Be(10m);
        f2.EntradaValor.Should().Be(70m);        // 10 × $7
        f2.SaldoCantidad.Should().Be(20m);
        f2.SaldoValor.Should().Be(120m);         // $50 + $70
        f2.CostoUnitarioPromedio.Should().Be(6m);  // ($50+$70) / 20 = $6

        // Fila 3: Venta 5 uds al costo promedio $6
        var f3 = k.Movimientos[2];
        f3.TipoMovimiento.Should().Be("Venta");
        f3.SalidaCantidad.Should().Be(5m);
        f3.SalidaValor.Should().BeApproximately(30m, 0.001m);  // 5 × $6
        f3.SaldoCantidad.Should().Be(15m);
        f3.SaldoValor.Should().BeApproximately(90m, 0.001m);   // $120 - $30
        f3.CostoUnitarioPromedio.Should().BeApproximately(6m, 0.001m); // no cambia

        // Resumen
        var r = k.Resumen;
        r.InventarioInicialCantidad.Should().Be(0m);
        r.InventarioInicialValor.Should().Be(0m);
        r.EntradasCantidad.Should().Be(20m);                  // 10 + 10
        r.EntradasValor.Should().Be(120m);                    // $50 + $70
        r.SalidasCantidad.Should().Be(5m);
        r.SalidasValor.Should().BeApproximately(30m, 0.001m); // 5 × $6
        r.InventarioFinalCantidad.Should().Be(15m);
        r.InventarioFinalValor.Should().BeApproximately(90m, 0.001m);
        r.CostoPromedioFinal.Should().BeApproximately(6m, 0.001m);
    }

    [Fact]
    public async Task Flujo_tres_compras_con_promedios_mixtos_kardex_correcto()
    {
        /*
         * Compra 1:  5 uds @ $10  → saldo=5,  val=$50,  avg=$10.00
         * Compra 2:  5 uds @ $20  → saldo=10, val=$150, avg=$15.00
         * Venta 1:   4 uds @ $15  → saldo=6,  val=$90,  avg=$15.00
         * Compra 3:  6 uds @ $12  → saldo=12, val=$162, avg=$13.50
         * Venta 2:   6 uds @ $13.50 → saldo=6, val=$81, avg=$13.50
         */
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId, proveedorId) = await SetupAsync(factory);

        var pid = seed.ProductId;
        var bid = seed.BodegaId;

        await CrearYAprobarCompraAsync(mediator, proveedorId, pid, bid, "001-001-000000010", 5m, 10m);
        await CrearYAprobarCompraAsync(mediator, proveedorId, pid, bid, "001-001-000000011", 5m, 20m);

        var v1 = await mediator.Send(
            new CrearVentaCommand(clienteId, bid, sucursalId,
                new List<ItemVentaDto> { new(pid, 4m, 25m) }),
            CancellationToken.None);
        await mediator.Send(new ValidarVentaCommand(v1.Value), CancellationToken.None);
        await mediator.Send(new EmitirFacturaElectronicaCommand(v1.Value), CancellationToken.None);

        await CrearYAprobarCompraAsync(mediator, proveedorId, pid, bid, "001-001-000000012", 6m, 12m);

        var v2 = await mediator.Send(
            new CrearVentaCommand(clienteId, bid, sucursalId,
                new List<ItemVentaDto> { new(pid, 6m, 25m) }),
            CancellationToken.None);
        await mediator.Send(new ValidarVentaCommand(v2.Value), CancellationToken.None);
        await mediator.Send(new EmitirFacturaElectronicaCommand(v2.Value), CancellationToken.None);

        var kardex = await mediator.Send(
            new GetKardexQuery(pid, bid, null, null), CancellationToken.None);

        kardex.IsSuccess.Should().BeTrue();
        var k = kardex.Value!;
        k.Movimientos.Should().HaveCount(5);

        // C1: saldo=5 @10
        k.Movimientos[0].SaldoCantidad.Should().Be(5m);
        k.Movimientos[0].CostoUnitarioPromedio.Should().Be(10m);

        // C2: saldo=10 avg=(50+100)/10=15
        k.Movimientos[1].SaldoCantidad.Should().Be(10m);
        k.Movimientos[1].CostoUnitarioPromedio.Should().Be(15m);

        // V1: salida 4@15=$60, saldo=6, val=$90
        k.Movimientos[2].SalidaCantidad.Should().Be(4m);
        k.Movimientos[2].SalidaValor.Should().BeApproximately(60m, 0.001m);
        k.Movimientos[2].SaldoCantidad.Should().Be(6m);
        k.Movimientos[2].SaldoValor.Should().BeApproximately(90m, 0.001m);
        k.Movimientos[2].CostoUnitarioPromedio.Should().BeApproximately(15m, 0.001m);

        // C3: saldo=12, val=90+72=162, avg=162/12=13.50
        k.Movimientos[3].SaldoCantidad.Should().Be(12m);
        k.Movimientos[3].SaldoValor.Should().BeApproximately(162m, 0.001m);
        k.Movimientos[3].CostoUnitarioPromedio.Should().BeApproximately(13.5m, 0.001m);

        // V2: salida 6@13.50=$81, saldo=6, val=$81
        k.Movimientos[4].SalidaCantidad.Should().Be(6m);
        k.Movimientos[4].SalidaValor.Should().BeApproximately(81m, 0.001m);
        k.Movimientos[4].SaldoCantidad.Should().Be(6m);
        k.Movimientos[4].SaldoValor.Should().BeApproximately(81m, 0.001m);
        k.Movimientos[4].CostoUnitarioPromedio.Should().BeApproximately(13.5m, 0.001m);

        // Resumen final
        k.Resumen.InventarioFinalCantidad.Should().Be(6m);
        k.Resumen.InventarioFinalValor.Should().BeApproximately(81m, 0.001m);
        k.Resumen.CostoPromedioFinal.Should().BeApproximately(13.5m, 0.001m);
    }
}
