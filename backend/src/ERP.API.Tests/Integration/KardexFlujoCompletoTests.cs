using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Inventory.UseCases.GetKardex;
using ERP.Application.Modules.Purchasing.UseCases.AprobarCompra;
using ERP.Application.Modules.Purchasing.UseCases.CrearCompra;
using ERP.Application.Modules.Purchasing.UseCases.ValidarCompra;
using ERP.Application.Sales.UseCases.CrearVenta;
using ERP.Application.Sales.UseCases.EmitirFacturaElectronica;
using ERP.Application.Sales.UseCases.ValidarVenta;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Test E2E que recorre el flujo real de compras y ventas y verifica el Kardex resultante.
/// Replica exactamente el escenario de prueba manual en Swagger:
///   - Compra 10 uds @ $5 â†’ Compra 10 uds @ $7 â†’ Venta 5 uds
///   - Costo promedio esperado: $6, valor salida: $30, stock final: 15 uds a $90
/// </summary>
public sealed class KardexFlujoCompletoTests
{
    // â”€â”€ Setup â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None);

        // Seed prerrequisitos de ventas (cliente, SRI, cuenta ingresos)
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(
            db, seed, stockInicial: 0m, crearStockActual: false,
            ct: CancellationToken.None);

        var clienteId  = db.Customers.First(c => c.SubscriberId == seed.SubscriberId).Id;
        var sucursalId = db.Branches.First(b => b.SubscriberId == seed.SubscriberId).Id;

        // Crear proveedor de prueba
        var proveedor = Supplier.Create(
            seed.SubscriberId,
            Supplier.TypeLegal,
            "Supplier E2E S.A.",
            seed.ProveedorRuc,
            email:       null,
            phone:     null,
            address:    null,
            paymentTerms: "Contado",
            createdBy:    seed.UserId);
        db.Suppliers.Add(proveedor);
        await db.SaveChangesAsync();

        return (mediator, db, seed, clienteId, sucursalId, proveedor.Id);
    }

    // â”€â”€ Helper para crear una PurchBill completa (Borradorâ†’Validadoâ†’IsApproved) â”€â”€

    private static async Task<Guid> CrearYAprobarCompraAsync(
        IMediator mediator,
        Guid proveedorId,
        Guid productoId,
        Guid bodegaId,
        string invoiceNumber,
        decimal quantity,
        decimal unitPrice)
    {
        // 1. Crear en estado Borrador (modo Manual, sin XML)
        var crear = await mediator.Send(
            new CrearCompraCommand(
                Modo: ModoCreacionCompra.Manual,
                XmlContent: null, XmlFileName: null,
                SupplierId:      proveedorId,
                InvoiceNumber:    invoiceNumber,
                InvoiceDate:     DateTime.Today,
                DueDate: null,
                PaymentTerms:    "Contado",
                Notes:    null,
                Lines: [new DetalleCompraInput(
                    "Producto test kardex", null, productoId,
                    quantity, unitPrice, 0m, 0m)],
                WarehouseAllocations: [new WarehouseAllocationRequest(0, bodegaId, quantity, productoId)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue($"CrearCompra {invoiceNumber} fallÃ³: {crear.Error}");
        var compraId = crear.Value!.Id;

        // 2. Validar (Borrador â†’ Validado)
        var validar = await mediator.Send(
            new ValidarCompraCommand(compraId), CancellationToken.None);
        validar.IsSuccess.Should().BeTrue($"ValidarCompra {invoiceNumber} fallÃ³: {validar.Error}");

        // 3. Aprobar (Validado â†’ IsApproved): registra movimiento de inventario con CostoUnitario
        var aprobar = await mediator.Send(
            new AprobarCompraCommand(compraId), CancellationToken.None);
        aprobar.IsSuccess.Should().BeTrue($"AprobarCompra {invoiceNumber} fallÃ³: {aprobar.Error}");

        return compraId;
    }

    // â”€â”€ Escenario principal (escenario idÃ©ntico al de la prueba manual en Swagger) â”€â”€

    [Fact]
    public async Task Flujo_completo_dos_compras_y_una_venta_genera_kardex_correcto()
    {
        /*
         * Escenario:
         *   Compra 1: 10 uds @ $5.00 â†’ saldo=10, val=$50,  avg=$5.000
         *   Compra 2: 10 uds @ $7.00 â†’ saldo=20, val=$120, avg=$6.000  â† (50+70)/20
         *   Venta:     5 uds @ avg   â†’ saldo=15, val=$90,  avg=$6.000
         *   Costo salida = 5 Ã— $6 = $30, saldo valor = $120 - $30 = $90
         *
         * Verificamos: saldo, valor, promedio en cada fila del kardex y el resumen.
         */
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId, proveedorId) = await SetupAsync(factory);

        var tid = seed.SubscriberId;
        var pid = seed.ProductId;
        var bid = seed.WarehouseId;

        // â”€â”€ COMPRA 1: 10 uds @ $5 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        await CrearYAprobarCompraAsync(
            mediator, proveedorId, pid, bid,
            invoiceNumber: "001-001-000000001",
            quantity:       10m,
            unitPrice: 5m);

        // VerificaciÃ³n intermedia: stock=10, valor=$50
        var stock1 = await db.CurrentStocks.FirstOrDefaultAsync(
            s => s.SubscriberId == tid && s.ProductId == pid && s.WarehouseId == bid);
        stock1.Should().NotBeNull();
        stock1!.Quantity.Should().Be(10m);
        stock1.TotalStockValue.Should().Be(50m);
        stock1.AverageCost.Should().Be(5m);

        // â”€â”€ COMPRA 2: 10 uds @ $7 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        await CrearYAprobarCompraAsync(
            mediator, proveedorId, pid, bid,
            invoiceNumber: "001-001-000000002",
            quantity:       10m,
            unitPrice: 7m);

        // VerificaciÃ³n intermedia: stock=20, valor=$120, avg=$6
        db.Entry(stock1).Reload();
        stock1.Quantity.Should().Be(20m);
        stock1.TotalStockValue.Should().Be(120m);
        stock1.AverageCost.Should().Be(6m);

        // â”€â”€ VENTA: 5 uds â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var crearVenta = await mediator.Send(
            new CrearVentaCommand(clienteId, bid, sucursalId,
                new List<ItemVentaDto> { new(pid, 5m, 10m) }),   // precio venta $10 (irrelevante para el costo)
            CancellationToken.None);
        crearVenta.IsSuccess.Should().BeTrue(crearVenta.Error);

        await mediator.Send(new ValidarVentaCommand(crearVenta.Value), CancellationToken.None);
        var emitir = await mediator.Send(
            new IssueElectronicInvoiceCommand(crearVenta.Value), CancellationToken.None);
        emitir.IsSuccess.Should().BeTrue(emitir.Error);

        // VerificaciÃ³n intermedia: stock=15, valor=$90, avg=$6
        db.Entry(stock1).Reload();
        stock1.Quantity.Should().Be(15m);
        stock1.TotalStockValue.Should().BeApproximately(90m, 0.001m);
        stock1.AverageCost.Should().BeApproximately(6m, 0.001m);

        // â”€â”€ KARDEX: verificaciÃ³n completa â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var kardex = await mediator.Send(
            new GetKardexQuery(pid, bid, null, null), CancellationToken.None);

        kardex.IsSuccess.Should().BeTrue(kardex.Error);
        var k = kardex.Value!;

        // Debe haber exactamente 3 filas (2 compras + 1 venta)
        k.Rows.Should().HaveCount(3, "dos compras + una venta");

        // Fila 1: Compra 10 @ $5
        var f1 = k.Rows[0];
        f1.MovementType.Should().Be("Compra");
        f1.InboundQuantity.Should().Be(10m);
        f1.InboundValue.Should().Be(50m);
        f1.BalanceQuantity.Should().Be(10m);
        f1.BalanceValue.Should().Be(50m);
        f1.AverageUnitCost.Should().Be(5m);

        // Fila 2: Compra 10 @ $7 â†’ promedio sube a $6
        var f2 = k.Rows[1];
        f2.MovementType.Should().Be("Compra");
        f2.InboundQuantity.Should().Be(10m);
        f2.InboundValue.Should().Be(70m);        // 10 Ã— $7
        f2.BalanceQuantity.Should().Be(20m);
        f2.BalanceValue.Should().Be(120m);         // $50 + $70
        f2.AverageUnitCost.Should().Be(6m);  // ($50+$70) / 20 = $6

        // Fila 3: Venta 5 uds al costo promedio $6
        var f3 = k.Rows[2];
        f3.MovementType.Should().Be("Venta");
        f3.OutboundQuantity.Should().Be(5m);
        f3.OutboundValue.Should().BeApproximately(30m, 0.001m);  // 5 Ã— $6
        f3.BalanceQuantity.Should().Be(15m);
        f3.BalanceValue.Should().BeApproximately(90m, 0.001m);   // $120 - $30
        f3.AverageUnitCost.Should().BeApproximately(6m, 0.001m); // no cambia

        // Resumen
        var r = k.Resumen;
        r.OpeningQuantity.Should().Be(0m);
        r.OpeningValue.Should().Be(0m);
        r.TotalInboundQuantity.Should().Be(20m);                  // 10 + 10
        r.TotalInboundValue.Should().Be(120m);                    // $50 + $70
        r.TotalOutboundQuantity.Should().Be(5m);
        r.TotalOutboundValue.Should().BeApproximately(30m, 0.001m); // 5 Ã— $6
        r.ClosingQuantity.Should().Be(15m);
        r.ClosingValue.Should().BeApproximately(90m, 0.001m);
        r.FinalAverageCost.Should().BeApproximately(6m, 0.001m);
    }

    [Fact]
    public async Task Flujo_tres_compras_con_promedios_mixtos_kardex_correcto()
    {
        /*
         * Compra 1:  5 uds @ $10  â†’ saldo=5,  val=$50,  avg=$10.00
         * Compra 2:  5 uds @ $20  â†’ saldo=10, val=$150, avg=$15.00
         * Venta 1:   4 uds @ $15  â†’ saldo=6,  val=$90,  avg=$15.00
         * Compra 3:  6 uds @ $12  â†’ saldo=12, val=$162, avg=$13.50
         * Venta 2:   6 uds @ $13.50 â†’ saldo=6, val=$81, avg=$13.50
         */
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId, proveedorId) = await SetupAsync(factory);

        var pid = seed.ProductId;
        var bid = seed.WarehouseId;

        await CrearYAprobarCompraAsync(mediator, proveedorId, pid, bid, "001-001-000000010", 5m, 10m);
        await CrearYAprobarCompraAsync(mediator, proveedorId, pid, bid, "001-001-000000011", 5m, 20m);

        var v1 = await mediator.Send(
            new CrearVentaCommand(clienteId, bid, sucursalId,
                new List<ItemVentaDto> { new(pid, 4m, 25m) }),
            CancellationToken.None);
        await mediator.Send(new ValidarVentaCommand(v1.Value), CancellationToken.None);
        await mediator.Send(new IssueElectronicInvoiceCommand(v1.Value), CancellationToken.None);

        await CrearYAprobarCompraAsync(mediator, proveedorId, pid, bid, "001-001-000000012", 6m, 12m);

        var v2 = await mediator.Send(
            new CrearVentaCommand(clienteId, bid, sucursalId,
                new List<ItemVentaDto> { new(pid, 6m, 25m) }),
            CancellationToken.None);
        await mediator.Send(new ValidarVentaCommand(v2.Value), CancellationToken.None);
        await mediator.Send(new IssueElectronicInvoiceCommand(v2.Value), CancellationToken.None);

        var kardex = await mediator.Send(
            new GetKardexQuery(pid, bid, null, null), CancellationToken.None);

        kardex.IsSuccess.Should().BeTrue();
        var k = kardex.Value!;
        k.Rows.Should().HaveCount(5);

        // C1: saldo=5 @10
        k.Rows[0].BalanceQuantity.Should().Be(5m);
        k.Rows[0].AverageUnitCost.Should().Be(10m);

        // C2: saldo=10 avg=(50+100)/10=15
        k.Rows[1].BalanceQuantity.Should().Be(10m);
        k.Rows[1].AverageUnitCost.Should().Be(15m);

        // V1: salida 4@15=$60, saldo=6, val=$90
        k.Rows[2].OutboundQuantity.Should().Be(4m);
        k.Rows[2].OutboundValue.Should().BeApproximately(60m, 0.001m);
        k.Rows[2].BalanceQuantity.Should().Be(6m);
        k.Rows[2].BalanceValue.Should().BeApproximately(90m, 0.001m);
        k.Rows[2].AverageUnitCost.Should().BeApproximately(15m, 0.001m);

        // C3: saldo=12, val=90+72=162, avg=162/12=13.50
        k.Rows[3].BalanceQuantity.Should().Be(12m);
        k.Rows[3].BalanceValue.Should().BeApproximately(162m, 0.001m);
        k.Rows[3].AverageUnitCost.Should().BeApproximately(13.5m, 0.001m);

        // V2: salida 6@13.50=$81, saldo=6, val=$81
        k.Rows[4].OutboundQuantity.Should().Be(6m);
        k.Rows[4].OutboundValue.Should().BeApproximately(81m, 0.001m);
        k.Rows[4].BalanceQuantity.Should().Be(6m);
        k.Rows[4].BalanceValue.Should().BeApproximately(81m, 0.001m);
        k.Rows[4].AverageUnitCost.Should().BeApproximately(13.5m, 0.001m);

        // Resumen final
        k.Resumen.ClosingQuantity.Should().Be(6m);
        k.Resumen.ClosingValue.Should().BeApproximately(81m, 0.001m);
        k.Resumen.FinalAverageCost.Should().BeApproximately(13.5m, 0.001m);
    }
}










