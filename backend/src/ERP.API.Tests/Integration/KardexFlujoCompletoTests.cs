using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Inventory.UseCases.GetKardex;
using ERP.Application.Modules.Purchasing.UseCases.ApprovePurchase;
using ERP.Application.Modules.Purchasing.UseCases.CreatePurchase;
using ERP.Application.Modules.Purchasing.UseCases.ValidatePurchase;
using ERP.Application.Sales.UseCases.CreateSale;
using ERP.Application.Sales.UseCases.IssueElectronicInvoice;
using ERP.Application.Sales.UseCases.ValidateSale;
using ERP.Domain.MasterData.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Test E2E que recorre el flujo real de compras y ventas y verifica el Kardex resultante.
/// Replica exactamente el escenario de prueba manual en Swagger:
///   - Compra 10 uds @ $5 Ã¢â€ â€™ Compra 10 uds @ $7 Ã¢â€ â€™ Venta 5 uds
///   - Costo promedio esperado: $6, valor salida: $30, stock final: 15 uds a $90
/// </summary>
public sealed class KardexFlujoCompletoTests
{
    // Ã¢â€â‚¬Ã¢â€â‚¬ Setup Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

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
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None, factory.MutableCompany);

        // Seed prerrequisitos de ventas (cliente, SRI, cuenta ingresos)
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(
            db, seed, stockInicial: 0m, crearStockActual: false,
            ct: CancellationToken.None);

        var clienteId  = db.BusinessPartners.First(c => c.SubscriberId == seed.SubscriberId).Id;
        var sucursalId = db.Branches.First(b => b.SubscriberId == seed.SubscriberId).Id;

        // Crear proveedor de prueba
        var proveedor = BusinessPartner.Create(
            subscriberId:        seed.SubscriberId,
            identificationType:  "04",
            identificationNumber: seed.ProveedorRuc,
            legalName:           "Supplier E2E S.A.",
            createdBy:           seed.UserId);
        db.BusinessPartners.Add(proveedor);
        await db.SaveChangesAsync();

        return (mediator, db, seed, clienteId, sucursalId, proveedor.Id);
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Helper para crear una PurchBill completa (BorradorÃ¢â€ â€™ValidadoÃ¢â€ â€™IsApproved) Ã¢â€â‚¬Ã¢â€â‚¬

    private static async Task<Guid> CrearYApprovePurchaseAsync(
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
            new CreatePurchaseCommand(
                Modo: PurchaseCreationMode.Manual,
                XmlContent: null, XmlFileName: null,
                BusinessPartnerId:      proveedorId,
                InvoiceNumber:    invoiceNumber,
                InvoiceDate:     DateTime.Today,
                DueDate: null,
                PaymentTerms:    "Contado",
                Notes:    null,
                Lines: [new PurchaseLineInput(
                    "Producto test kardex", null, productoId,
                    quantity, unitPrice, 0m, 0m)],
                WarehouseAllocations: [new WarehouseAllocationRequest(0, bodegaId, quantity, productoId)]),
            CancellationToken.None);

        crear.IsSuccess.Should().BeTrue($"CreatePurchase {invoiceNumber} fallÃƒÂ³: {crear.Error}");
        var compraId = crear.Value!.Id;

        // 2. Validar (Borrador Ã¢â€ â€™ Validado)
        var validar = await mediator.Send(
            new ValidatePurchaseCommand(compraId), CancellationToken.None);
        validar.IsSuccess.Should().BeTrue($"ValidatePurchase {invoiceNumber} fallÃƒÂ³: {validar.Error}");

        // 3. Aprobar (Validado Ã¢â€ â€™ IsApproved): registra movimiento de inventario con CostoUnitario
        var aprobar = await mediator.Send(
            new ApprovePurchaseCommand(compraId), CancellationToken.None);
        aprobar.IsSuccess.Should().BeTrue($"ApprovePurchase {invoiceNumber} fallÃƒÂ³: {aprobar.Error}");

        return compraId;
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Escenario principal (escenario idÃƒÂ©ntico al de la prueba manual en Swagger) Ã¢â€â‚¬Ã¢â€â‚¬

    [Fact]
    public async Task Flujo_completo_dos_compras_y_una_venta_genera_kardex_correcto()
    {
        /*
         * Escenario:
         *   Compra 1: 10 uds @ $5.00 Ã¢â€ â€™ saldo=10, val=$50,  avg=$5.000
         *   Compra 2: 10 uds @ $7.00 Ã¢â€ â€™ saldo=20, val=$120, avg=$6.000  Ã¢â€ Â (50+70)/20
         *   Venta:     5 uds @ avg   Ã¢â€ â€™ saldo=15, val=$90,  avg=$6.000
         *   Costo salida = 5 Ãƒâ€” $6 = $30, saldo valor = $120 - $30 = $90
         *
         * Verificamos: saldo, valor, promedio en cada fila del kardex y el resumen.
         */
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId, proveedorId) = await SetupAsync(factory);

        var tid = seed.SubscriberId;
        var pid = seed.ProductId;
        var bid = seed.WarehouseId;

        // Ã¢â€â‚¬Ã¢â€â‚¬ COMPRA 1: 10 uds @ $5 Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
        await CrearYApprovePurchaseAsync(
            mediator, proveedorId, pid, bid,
            invoiceNumber: "001-001-000000001",
            quantity:       10m,
            unitPrice: 5m);

        // VerificaciÃƒÂ³n intermedia: stock=10, valor=$50
        var stock1 = await db.CurrentStocks.FirstOrDefaultAsync(
            s => s.SubscriberId == tid && s.ProductId == pid && s.WarehouseId == bid);
        stock1.Should().NotBeNull();
        stock1!.Quantity.Should().Be(10m);
        stock1.TotalStockValue.Should().Be(50m);
        stock1.AverageCost.Should().Be(5m);

        // Ã¢â€â‚¬Ã¢â€â‚¬ COMPRA 2: 10 uds @ $7 Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
        await CrearYApprovePurchaseAsync(
            mediator, proveedorId, pid, bid,
            invoiceNumber: "001-001-000000002",
            quantity:       10m,
            unitPrice: 7m);

        // VerificaciÃƒÂ³n intermedia: stock=20, valor=$120, avg=$6
        db.Entry(stock1).Reload();
        stock1.Quantity.Should().Be(20m);
        stock1.TotalStockValue.Should().Be(120m);
        stock1.AverageCost.Should().Be(6m);

        // Ã¢â€â‚¬Ã¢â€â‚¬ VENTA: 5 uds Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
        var crearVenta = await mediator.Send(
            new CreateSaleCommand(clienteId, bid, sucursalId,
                new List<SaleItemDto> { new(pid, 5m, 10m) }),   // precio venta $10 (irrelevante para el costo)
            CancellationToken.None);
        crearVenta.IsSuccess.Should().BeTrue(crearVenta.Error);

        await mediator.Send(new ValidateSaleCommand(crearVenta.Value), CancellationToken.None);
        var emitir = await mediator.Send(
            new IssueElectronicInvoiceCommand(crearVenta.Value), CancellationToken.None);
        emitir.IsSuccess.Should().BeTrue(emitir.Error);

        // VerificaciÃƒÂ³n intermedia: stock=15, valor=$90, avg=$6
        db.Entry(stock1).Reload();
        stock1.Quantity.Should().Be(15m);
        stock1.TotalStockValue.Should().BeApproximately(90m, 0.001m);
        stock1.AverageCost.Should().BeApproximately(6m, 0.001m);

        // Ã¢â€â‚¬Ã¢â€â‚¬ KARDEX: verificaciÃƒÂ³n completa Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
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

        // Fila 2: Compra 10 @ $7 Ã¢â€ â€™ promedio sube a $6
        var f2 = k.Rows[1];
        f2.MovementType.Should().Be("Compra");
        f2.InboundQuantity.Should().Be(10m);
        f2.InboundValue.Should().Be(70m);        // 10 Ãƒâ€” $7
        f2.BalanceQuantity.Should().Be(20m);
        f2.BalanceValue.Should().Be(120m);         // $50 + $70
        f2.AverageUnitCost.Should().Be(6m);  // ($50+$70) / 20 = $6

        // Fila 3: Venta 5 uds al costo promedio $6
        var f3 = k.Rows[2];
        f3.MovementType.Should().Be("Venta");
        f3.OutboundQuantity.Should().Be(5m);
        f3.OutboundValue.Should().BeApproximately(30m, 0.001m);  // 5 Ãƒâ€” $6
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
        r.TotalOutboundValue.Should().BeApproximately(30m, 0.001m); // 5 Ãƒâ€” $6
        r.ClosingQuantity.Should().Be(15m);
        r.ClosingValue.Should().BeApproximately(90m, 0.001m);
        r.FinalAverageCost.Should().BeApproximately(6m, 0.001m);
    }

    [Fact]
    public async Task Flujo_tres_compras_con_promedios_mixtos_kardex_correcto()
    {
        /*
         * Compra 1:  5 uds @ $10  Ã¢â€ â€™ saldo=5,  val=$50,  avg=$10.00
         * Compra 2:  5 uds @ $20  Ã¢â€ â€™ saldo=10, val=$150, avg=$15.00
         * Venta 1:   4 uds @ $15  Ã¢â€ â€™ saldo=6,  val=$90,  avg=$15.00
         * Compra 3:  6 uds @ $12  Ã¢â€ â€™ saldo=12, val=$162, avg=$13.50
         * Venta 2:   6 uds @ $13.50 Ã¢â€ â€™ saldo=6, val=$81, avg=$13.50
         */
        await using var factory = new IntegrationTestWebAppFactory();
        var (mediator, db, seed, clienteId, sucursalId, proveedorId) = await SetupAsync(factory);

        var pid = seed.ProductId;
        var bid = seed.WarehouseId;

        await CrearYApprovePurchaseAsync(mediator, proveedorId, pid, bid, "001-001-000000010", 5m, 10m);
        await CrearYApprovePurchaseAsync(mediator, proveedorId, pid, bid, "001-001-000000011", 5m, 20m);

        var v1 = await mediator.Send(
            new CreateSaleCommand(clienteId, bid, sucursalId,
                new List<SaleItemDto> { new(pid, 4m, 25m) }),
            CancellationToken.None);
        await mediator.Send(new ValidateSaleCommand(v1.Value), CancellationToken.None);
        await mediator.Send(new IssueElectronicInvoiceCommand(v1.Value), CancellationToken.None);

        await CrearYApprovePurchaseAsync(mediator, proveedorId, pid, bid, "001-001-000000012", 6m, 12m);

        var v2 = await mediator.Send(
            new CreateSaleCommand(clienteId, bid, sucursalId,
                new List<SaleItemDto> { new(pid, 6m, 25m) }),
            CancellationToken.None);
        await mediator.Send(new ValidateSaleCommand(v2.Value), CancellationToken.None);
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










