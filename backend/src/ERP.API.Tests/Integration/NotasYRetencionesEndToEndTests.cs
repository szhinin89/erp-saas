using System.Globalization;
using System.Text;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Modules.Purchasing.Services;
using ERP.Application.Modules.Purchasing.UseCases.AprobarCompra;
using ERP.Application.Modules.Purchasing.UseCases.CrearCompra;
using ERP.Application.Modules.Purchasing.UseCases.NotasProveedor;
using ERP.Application.Modules.Purchasing.UseCases.Retenciones;
using ERP.Application.Modules.Purchasing.UseCases.ValidarCompra;
using ERP.Application.Modules.Expenses.UseCases.AprobarGasto;
using ERP.Application.Modules.Expenses.UseCases.CrearGasto;
using ERP.Application.Modules.Expenses.UseCases.ValidarGasto;
using ERP.Application.Sales.UseCases.CrearVenta;
using ERP.Application.Sales.UseCases.EmitirFacturaElectronica;
using ERP.Application.Sales.UseCases.Notas;
using ERP.Application.Sales.UseCases.RetencionesRecibidas;
using ERP.Application.Sales.UseCases.ValidarVenta;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Integration;

/// <summary>Â§6.1 escenarios NC/ND, retenciÃ³n emitida (compras) y retenciÃ³n recibida (ventas).</summary>
public sealed class NotasYRetencionesEndToEndTests
{
    private static CrearVentaCommand BuildCrearVenta(
        Guid clienteId, Guid bodegaId, Guid sucursalId, Guid productoId, decimal quantity)
        => new(clienteId, bodegaId, sucursalId, new List<ItemVentaDto> { new(productoId, quantity, 25.00m) });

    private static string BuildRetencionRecibidaXml(string clave49, decimal valorRetenido)
    {
        var v = valorRetenido.ToString(CultureInfo.InvariantCulture);
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <comprobanteRetencion id="comprobante" version="1.0.0">
              <infoTributaria><accessKey>{clave49}</accessKey></infoTributaria>
              <infoCompRetencion><issueDate>10/05/2026</issueDate></infoCompRetencion>
              <impuestos>
                <vatTotal><valorRetenido>{v}</valorRetenido></vatTotal>
              </impuestos>
            </comprobanteRetencion>
            """;
    }

    private static string BuildNotaProveedorXml(
        string tipoNota,
        string clave49,
        string rucProveedor,
        string secuencial,
        decimal quantity,
        decimal unitPrice,
        decimal subtotal,
        decimal vatTotal,
        string codigoPrincipal = "P-INT",
        string motivo = "Ajuste proveedor")
    {
        var tipo = tipoNota.Trim().ToUpperInvariant();
        var root = tipo == "DEBITO" ? "notaDebito" : "notaCredito";
        var info = tipo == "DEBITO" ? "infoNotaDebito" : "infoNotaCredito";
        var total = subtotal + vatTotal;

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <{root} id="comprobante" version="1.0.0">
              <infoTributaria>
                <razonSocial>PROVEEDOR INTEGRACION S.A.</razonSocial>
                <ruc>{rucProveedor}</ruc>
                <accessKey>{clave49}</accessKey>
                <estab>001</estab>
                <ptoEmi>001</ptoEmi>
                <secuencial>{secuencial}</secuencial>
              </infoTributaria>
              <{info}>
                <issueDate>11/05/2026</issueDate>
                <motivo>{motivo}</motivo>
                <totalSinImpuestos>{subtotal.ToString(CultureInfo.InvariantCulture)}</totalSinImpuestos>
                <totalConImpuestos>
                  <totalImpuesto>
                    <valor>{vatTotal.ToString(CultureInfo.InvariantCulture)}</valor>
                  </totalImpuesto>
                </totalConImpuestos>
                <importeTotal>{total.ToString(CultureInfo.InvariantCulture)}</importeTotal>
                <valorModificacion>{total.ToString(CultureInfo.InvariantCulture)}</valorModificacion>
              </{info}>
              <detalles>
                <detalle>
                  <codigoPrincipal>{codigoPrincipal}</codigoPrincipal>
                  <descripcion>Item integraciÃ³n</descripcion>
                  <cantidad>{quantity.ToString(CultureInfo.InvariantCulture)}</cantidad>
                  <precioUnitario>{unitPrice.ToString(CultureInfo.InvariantCulture)}</precioUnitario>
                  <descuento>0</descuento>
                  <precioTotalSinImpuesto>{subtotal.ToString(CultureInfo.InvariantCulture)}</precioTotalSinImpuesto>
                  <impuestos>
                    <vatTotal>
                      <valor>{vatTotal.ToString(CultureInfo.InvariantCulture)}</valor>
                    </vatTotal>
                  </impuestos>
                </detalle>
              </detalles>
            </{root}>
            """;
    }

    [Fact]
    public async Task Nota_credito_autorizada_genera_xml_simulado_y_reingresa_stock()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockInicial: 10m, ct: CancellationToken.None);

        var clienteId  = db.Customers.First(c => c.TenantId == seed.TenantId).Id;
        var sucursalId = db.Branches.First(b => b.TenantId == seed.TenantId).Id;

        var venta = await mediator.Send(
            BuildCrearVenta(clienteId, seed.WarehouseId, sucursalId, seed.ProductId, 2m),
            CancellationToken.None);
        venta.IsSuccess.Should().BeTrue(venta.Error);
        await mediator.Send(new ValidarVentaCommand(venta.Value), CancellationToken.None);
        var emitir = await mediator.Send(new IssueElectronicInvoiceCommand(venta.Value), CancellationToken.None);
        emitir.IsSuccess.Should().BeTrue(emitir.Error);

        db.CurrentStocks.First(s =>
                s.TenantId == seed.TenantId && s.ProductId == seed.ProductId && s.WarehouseId == seed.WarehouseId)
            .Quantity.Should().Be(8m);

        var crearNota = await mediator.Send(
            new CreateSalesNoteCommand(
                venta.Value,
                "CREDITO",
                "DevoluciÃ³n parcial",
                new[] { new CrearVentasNotaItemDto(seed.ProductId, 1m, 25m) }),
            CancellationToken.None);
        crearNota.IsSuccess.Should().BeTrue(crearNota.Error);

        var enviar = await mediator.Send(new EnviarVentasNotaSriCommand(crearNota.Value), CancellationToken.None);
        enviar.IsSuccess.Should().BeTrue(enviar.Error);

        var nota = await db.SalesNotes.FindAsync(new object[] { crearNota.Value }, CancellationToken.None);
        nota.Should().NotBeNull();
        nota!.Status.Should().Be("Autorizado");
        nota.XmlSignedPath.Should().NotBeNullOrEmpty();

        db.CurrentStocks.First(s =>
                s.TenantId == seed.TenantId && s.ProductId == seed.ProductId && s.WarehouseId == seed.WarehouseId)
            .Quantity.Should().Be(9m);

        db.StockMovements.Count(m =>
                m.TenantId == seed.TenantId
                && m.MovementType == StockMovementType.SaleReturn
                && m.SourceDocId == nota.Id)
            .Should().Be(1);
    }

    [Fact]
    public async Task Nota_debito_autorizada_crea_asiento_con_credito_a_ventas()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockInicial: 10m, ct: CancellationToken.None);

        var clienteId  = db.Customers.First(c => c.TenantId == seed.TenantId).Id;
        var sucursalId = db.Branches.First(b => b.TenantId == seed.TenantId).Id;
        var cuentaVentas = db.Accounts.First(a => a.TenantId == seed.TenantId && a.Code.Value == "4.1.99");

        var venta = await mediator.Send(
            BuildCrearVenta(clienteId, seed.WarehouseId, sucursalId, seed.ProductId, 1m),
            CancellationToken.None);
        venta.IsSuccess.Should().BeTrue(venta.Error);
        await mediator.Send(new ValidarVentaCommand(venta.Value), CancellationToken.None);
        await mediator.Send(new IssueElectronicInvoiceCommand(venta.Value), CancellationToken.None);

        var crearNd = await mediator.Send(
            new CreateSalesNoteCommand(
                venta.Value,
                "DEBITO",
                "Ajuste",
                new[] { new CrearVentasNotaItemDto(seed.ProductId, 1m, 25m) }),
            CancellationToken.None);
        crearNd.IsSuccess.Should().BeTrue(crearNd.Error);

        var enviar = await mediator.Send(new EnviarVentasNotaSriCommand(crearNd.Value), CancellationToken.None);
        enviar.IsSuccess.Should().BeTrue(enviar.Error);

        var nota = await db.SalesNotes.FindAsync(new object[] { crearNd.Value }, CancellationToken.None);
        nota!.JournalEntryId.Should().NotBeNull();

        var lineas = db.JournalEntryLines.Where(l => l.JournalEntryId == nota.JournalEntryId).ToList();
        lineas.Should().NotBeEmpty();
        lineas.Should().Contain(l =>
            l.AccountId == cuentaVentas.Id
            && l.Credit.Amount == nota.Total
            && l.Debit.Amount == 0m);
    }

    [Fact]
    public async Task Nota_credito_proveedor_en_compra_aprobada_reingresa_stock_y_crea_asiento_inverso()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);
        var cuentaPasivo = db.Accounts.First(a => a.TenantId == seed.TenantId && a.Code.Value == "2.1.99");

        var xmlCompra = IntegrationSeedData.BuildFacturaXml(seed.ClaveAcceso49, seed.ProveedorRuc);
        var compraRes = await mediator.Send(
            new CrearCompraCommand(
                ModoCreacionCompra.Xml,
                XmlContent: Encoding.UTF8.GetBytes(xmlCompra),
                XmlFileName: "factura.xml",
                SupplierId: null,
                InvoiceNumber: null,
                InvoiceDate: null,
                DueDate: null,
                PaymentTerms: null,
                Notes: null,
                Lines: null,
                WarehouseAllocations: new[] { new WarehouseAllocationRequest(0, seed.WarehouseId, 2m, seed.ProductId) }),
            CancellationToken.None);
        compraRes.IsSuccess.Should().BeTrue(compraRes.Error);
        await mediator.Send(new ValidarCompraCommand(compraRes.Value!.Id), CancellationToken.None);
        await mediator.Send(new AprobarCompraCommand(compraRes.Value.Id), CancellationToken.None);

        var compra = db.PurchBills.First(c => c.Id == compraRes.Value.Id);
        var lineasCompra = db.JournalEntryLines.Where(l => l.JournalEntryId == compra.JournalEntryId).ToList();
        lineasCompra.Should().Contain(l => l.AccountId == cuentaPasivo.Id && l.Credit.Amount == compra.Total);

        var claveNota = ClaveAcceso49TestFactory.FromPrefix48(new string('1', 48));
        var xmlNota = BuildNotaProveedorXml(
            "CREDITO",
            claveNota,
            seed.ProveedorRuc,
            secuencial: "000000101",
            quantity: 1m,
            unitPrice: 25m,
            subtotal: 25m,
            vatTotal: 3m);

        var importar = await mediator.Send(
            new ImportSupplierNoteCommand(
                Encoding.UTF8.GetBytes(xmlNota),
                "nota-credito-proveedor.xml",
                compraRes.Value.Id,
                ExpenseInvoiceId: null),
            CancellationToken.None);
        importar.IsSuccess.Should().BeTrue(importar.Error);

        var aprobar = await mediator.Send(
            new AprobarCompraNotaProveedorCommand(importar.Value!.Id, "AUTH-NC-PROV", DateTime.UtcNow),
            CancellationToken.None);
        aprobar.IsSuccess.Should().BeTrue(aprobar.Error);

        var nota = db.PurchNotes.First(n => n.Id == importar.Value.Id);
        nota.Status.Should().Be("IsApproved");
        nota.JournalEntryId.Should().NotBeNull();

        db.CurrentStocks.First(s =>
                s.TenantId == seed.TenantId && s.ProductId == seed.ProductId && s.WarehouseId == seed.WarehouseId)
            .Quantity.Should().Be(3m);

        db.StockMovements.Count(m =>
                m.TenantId == seed.TenantId
                && m.MovementType == StockMovementType.SupplierCreditNote
                && m.SourceDocId == nota.Id)
            .Should().Be(1);

        var lineasNota = db.JournalEntryLines.Where(l => l.JournalEntryId == nota.JournalEntryId).ToList();
        lineasNota.Should().Contain(l => l.AccountId == cuentaPasivo.Id && l.Debit.Amount == nota.Total && l.Credit.Amount == 0m);
    }

    [Fact]
    public async Task Nota_debito_proveedor_en_compra_disminuye_stock_y_aumenta_deuda()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);
        var cuentaPasivo = db.Accounts.First(a => a.TenantId == seed.TenantId && a.Code.Value == "2.1.99");

        var xmlCompra = IntegrationSeedData.BuildFacturaXml(seed.ClaveAcceso49, seed.ProveedorRuc);
        var compraRes = await mediator.Send(
            new CrearCompraCommand(
                ModoCreacionCompra.Xml,
                XmlContent: Encoding.UTF8.GetBytes(xmlCompra),
                XmlFileName: "factura.xml",
                SupplierId: null,
                InvoiceNumber: null,
                InvoiceDate: null,
                DueDate: null,
                PaymentTerms: null,
                Notes: null,
                Lines: null,
                WarehouseAllocations: new[] { new WarehouseAllocationRequest(0, seed.WarehouseId, 2m, seed.ProductId) }),
            CancellationToken.None);
        compraRes.IsSuccess.Should().BeTrue(compraRes.Error);
        await mediator.Send(new ValidarCompraCommand(compraRes.Value!.Id), CancellationToken.None);
        await mediator.Send(new AprobarCompraCommand(compraRes.Value.Id), CancellationToken.None);

        var claveNota = ClaveAcceso49TestFactory.FromPrefix48(new string('2', 48));
        var xmlNota = BuildNotaProveedorXml(
            "DEBITO",
            claveNota,
            seed.ProveedorRuc,
            secuencial: "000000102",
            quantity: 1m,
            unitPrice: 25m,
            subtotal: 25m,
            vatTotal: 3m);

        var importar = await mediator.Send(
            new ImportSupplierNoteCommand(
                Encoding.UTF8.GetBytes(xmlNota),
                "nota-debito-proveedor.xml",
                compraRes.Value.Id,
                ExpenseInvoiceId: null),
            CancellationToken.None);
        importar.IsSuccess.Should().BeTrue(importar.Error);

        var aprobar = await mediator.Send(
            new AprobarCompraNotaProveedorCommand(importar.Value!.Id, "AUTH-ND-PROV", DateTime.UtcNow),
            CancellationToken.None);
        aprobar.IsSuccess.Should().BeTrue(aprobar.Error);

        var nota = db.PurchNotes.First(n => n.Id == importar.Value.Id);
        nota.Status.Should().Be("IsApproved");
        nota.JournalEntryId.Should().NotBeNull();

        db.CurrentStocks.First(s =>
                s.TenantId == seed.TenantId && s.ProductId == seed.ProductId && s.WarehouseId == seed.WarehouseId)
            .Quantity.Should().Be(1m);

        db.StockMovements.Count(m =>
                m.TenantId == seed.TenantId
                && m.MovementType == StockMovementType.SupplierDebitNote
                && m.SourceDocId == nota.Id)
            .Should().Be(1);

        var compraActualizada = db.PurchBills.First(c => c.Id == compraRes.Value.Id);
        compraActualizada.TotalNotesApplied.Should().Be(0m);

        var lineasNota = db.JournalEntryLines.Where(l => l.JournalEntryId == nota.JournalEntryId).ToList();
        lineasNota.Should().Contain(l => l.AccountId == cuentaPasivo.Id && l.Credit.Amount == nota.Total && l.Debit.Amount == 0m);
    }

    [Fact]
    public async Task Nota_credito_en_gasto_ajusta_gasto_sin_afectar_inventario()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);

        var xmlCompra = IntegrationSeedData.BuildFacturaXml(seed.ClaveAcceso49, seed.ProveedorRuc);
        var compraRes = await mediator.Send(
            new CrearCompraCommand(
                ModoCreacionCompra.Xml,
                XmlContent: Encoding.UTF8.GetBytes(xmlCompra),
                XmlFileName: "factura.xml",
                SupplierId: null,
                InvoiceNumber: null,
                InvoiceDate: null,
                DueDate: null,
                PaymentTerms: null,
                Notes: null,
                Lines: null,
                WarehouseAllocations: new[] { new WarehouseAllocationRequest(0, seed.WarehouseId, 2m, seed.ProductId) }),
            CancellationToken.None);
        compraRes.IsSuccess.Should().BeTrue(compraRes.Error);
        await mediator.Send(new ValidarCompraCommand(compraRes.Value!.Id), CancellationToken.None);
        await mediator.Send(new AprobarCompraCommand(compraRes.Value.Id), CancellationToken.None);

        var proveedorId = db.PurchBills.First(c => c.Id == compraRes.Value.Id).SupplierId;
        var cuentaPasivo = db.Accounts.First(a => a.TenantId == seed.TenantId && a.Code.Value == "2.1.99");

        var gastoRes = await mediator.Send(
            new CreateExpenseCommand(
                ExpenseCreationMode.Manual,
                XmlContent: null,
                XmlFileName: null,
                SupplierId: proveedorId,
                IssueDate: DateTime.UtcNow.Date,
                Concept: "Servicio de soporte",
                Category: "Viajes",
                Subtotal: 20m,
                VatTotal: 2m,
                Total: 22m,
                Notes: null),
            CancellationToken.None);
        gastoRes.IsSuccess.Should().BeTrue(gastoRes.Error);
        await mediator.Send(new ValidateExpenseCommand(gastoRes.Value!.Id), CancellationToken.None);
        await mediator.Send(new ApproveExpenseCommand(gastoRes.Value.Id), CancellationToken.None);

        var stockAntes = db.CurrentStocks.First(s =>
            s.TenantId == seed.TenantId && s.ProductId == seed.ProductId && s.WarehouseId == seed.WarehouseId).Quantity;

        var claveNota = ClaveAcceso49TestFactory.FromPrefix48(new string('3', 48));
        var xmlNota = BuildNotaProveedorXml(
            "CREDITO",
            claveNota,
            seed.ProveedorRuc,
            secuencial: "000000103",
            quantity: 1m,
            unitPrice: 10m,
            subtotal: 10m,
            vatTotal: 1m);

        var importar = await mediator.Send(
            new ImportSupplierNoteCommand(
                Encoding.UTF8.GetBytes(xmlNota),
                "nota-credito-gasto.xml",
                PurchBillId: null,
                gastoRes.Value.Id),
            CancellationToken.None);
        importar.IsSuccess.Should().BeTrue(importar.Error);

        var aprobar = await mediator.Send(
            new AprobarCompraNotaProveedorCommand(importar.Value!.Id, "AUTH-NC-GASTO", DateTime.UtcNow),
            CancellationToken.None);
        aprobar.IsSuccess.Should().BeTrue(aprobar.Error);

        var nota = db.PurchNotes.First(n => n.Id == importar.Value.Id);
        var gasto = db.ExpenseInvoices.First(g => g.Id == gastoRes.Value.Id);

        gasto.TotalNotesApplied.Should().Be(nota.Total);
        db.CurrentStocks.First(s =>
                s.TenantId == seed.TenantId && s.ProductId == seed.ProductId && s.WarehouseId == seed.WarehouseId)
            .Quantity.Should().Be(stockAntes);

        db.StockMovements.Count(m =>
                m.TenantId == seed.TenantId
                && m.SourceDocType == "CompraNotaProveedor"
                && m.SourceDocId == nota.Id)
            .Should().Be(0);

        var lineasNota = db.JournalEntryLines.Where(l => l.JournalEntryId == nota.JournalEntryId).ToList();
        lineasNota.Should().Contain(l => l.AccountId == cuentaPasivo.Id && l.Debit.Amount == nota.Total && l.Credit.Amount == 0m);
    }

    [Fact]
    public async Task Compra_aprobada_retencion_emitida_enviada_simulada_crea_asiento()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockInicial: 0m, crearStockActual: false, ct: CancellationToken.None);

        var userId = seed.UserId;
        var tid    = seed.TenantId;
        db.Accounts.Add(Account.Create(
            tid, "2.1.88", "PROVEEDORES cuenta CxP", AccountType.Liability, AccountNature.Credit, userId));
        db.Accounts.Add(Account.Create(
            tid, "2.2.77", "Pasivo RETENCIONES fuente", AccountType.Liability, AccountNature.Credit, userId));
        db.RetentionSettings.Add(RetentionSettings.Create(
            tid, "RENTA", "PROVEEDOR", "303", 1.5m, userId));
        await db.SaveChangesAsync(CancellationToken.None);

        var xml = IntegrationSeedData.BuildFacturaXml(seed.ClaveAcceso49, seed.ProveedorRuc);
        var crear = await mediator.Send(
            new CrearCompraCommand(
                ModoCreacionCompra.Xml,
                XmlContent: Encoding.UTF8.GetBytes(xml),
                XmlFileName: "factura.xml",
                SupplierId: null,
                InvoiceNumber: null,
                InvoiceDate: null,
                DueDate: null,
                PaymentTerms: null,
                Notes: null,
                Lines: null,
                WarehouseAllocations: new[] { new WarehouseAllocationRequest(0, seed.WarehouseId, 2m, seed.ProductId) }),
            CancellationToken.None);
        crear.IsSuccess.Should().BeTrue(crear.Error);
        await mediator.Send(new ValidarCompraCommand(crear.Value!.Id), CancellationToken.None);
        await mediator.Send(new AprobarCompraCommand(crear.Value.Id), CancellationToken.None);

        var compra = db.PurchBills.First(c => c.Id == crear.Value.Id);
        compra.Subtotal.Should().BeGreaterThan(0m);

        var gen = await mediator.Send(new GenerateIssuedRetentionCommand(compra.Id), CancellationToken.None);
        gen.IsSuccess.Should().BeTrue(gen.Error);

        var env = await mediator.Send(new SendIssuedRetentionCommand(gen.Value), CancellationToken.None);
        env.IsSuccess.Should().BeTrue(env.Error);

        var ret = await db.IssuedRetentions.FindAsync(new object[] { gen.Value }, CancellationToken.None);
        ret!.Status.Should().Be("Autorizado");
        ret.JournalEntryId.Should().NotBeNull();
        ret.XmlSignedPath.Should().NotBeNullOrEmpty();

        var esperado = CompraRetencionCalculo.CalcularValorRetenido(compra.Subtotal, 1.5m);
        ret.TotalRetained.Should().Be(esperado);

        var lineas = db.JournalEntryLines.Where(l => l.JournalEntryId == ret.JournalEntryId).ToList();
        lineas.Sum(l => l.Debit.Amount).Should().Be(ret.TotalRetained);
        lineas.Sum(l => l.Credit.Amount).Should().Be(ret.TotalRetained);
    }

    [Fact]
    public async Task Retencion_recibida_registrada_con_xml_vincula_factura_y_crea_asiento()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db       = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var seed = await IntegrationSeedData.SeedAsync(db, factory.MutableTenant, factory.MutableUser, CancellationToken.None);
        await VentasEndToEndHelpers.SeedVentasPrerequisitesAsync(db, seed, stockInicial: 10m, ct: CancellationToken.None);

        var userId = seed.UserId;
        var tid    = seed.TenantId;
        db.Accounts.Add(Account.Create(
            tid, "2.3.01", "IVA por pagar pruebas", AccountType.Liability, AccountNature.Credit, userId));
        db.Accounts.Add(Account.Create(
            tid, "1.2.01", "Clientes por COBRAR integraciÃ³n", AccountType.Asset, AccountNature.Debit, userId));
        await db.SaveChangesAsync(CancellationToken.None);

        var clienteId  = db.Customers.First(c => c.TenantId == seed.TenantId).Id;
        var sucursalId = db.Branches.First(b => b.TenantId == seed.TenantId).Id;

        var venta = await mediator.Send(
            BuildCrearVenta(clienteId, seed.WarehouseId, sucursalId, seed.ProductId, 1m),
            CancellationToken.None);
        venta.IsSuccess.Should().BeTrue(venta.Error);
        await mediator.Send(new ValidarVentaCommand(venta.Value), CancellationToken.None);
        await mediator.Send(new IssueElectronicInvoiceCommand(venta.Value), CancellationToken.None);

        var claveRet = ClaveAcceso49TestFactory.FromPrefix48(new string('8', 48));
        var xml      = BuildRetencionRecibidaXml(claveRet, 3.75m);

        var reg = await mediator.Send(
            new RegisterSalesRetentionCommand(venta.Value, xml),
            CancellationToken.None);
        reg.IsSuccess.Should().BeTrue(reg.Error);

        var ret = db.SalesRetentions.First(r => r.Id == reg.Value);
        ret.SalesBillId.Should().Be(venta.Value);
        ret.JournalEntryId.Should().NotBeNull();

        var lineas = db.JournalEntryLines.Where(l => l.JournalEntryId == ret.JournalEntryId).ToList();
        lineas.Sum(l => l.Debit.Amount).Should().Be(3.75m);
        lineas.Sum(l => l.Credit.Amount).Should().Be(3.75m);
    }
}














