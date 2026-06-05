using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Fiscal.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Tests.Support;

/// <summary>
/// Helpers de seed compartidos entre los tests de Ventas (end-to-end, stock, HTTP).
/// </summary>
internal static class SalesEndToEndHelpers
{
    private static long _nextSnowflakeId = 9_000;

    private static long NextSnowflakeId() => Interlocked.Increment(ref _nextSnowflakeId);

    internal static Invoice RequireInvoice(ErpDbContext db, Guid publicId) =>
        db.FiscalInvoices
            .Include(i => i.Electronic)
            .Include(i => i.Lines)
            .First(i => i.PublicId == publicId);

    /// <summary>
    /// Crea una salesBill fiscal autorizada (greenfield) para tests de impresiÃ³n/RIDE.
    /// </summary>
    internal static async Task<Guid> SeedAuthorizedInvoiceAsync(
        ErpDbContext db,
        IntegrationSeedData.SeedResult seed,
        Guid businessPartnerId,
        Guid companyId,
        decimal subtotal,
        decimal taxTotal,
        decimal total,
        string sequential,
        string lineDescription,
        CancellationToken ct = default)
    {
        var publicId = Guid.NewGuid();
        var invoiceId = NextSnowflakeId();
        var issueDate = DateTime.UtcNow;
        var accessKey = new string('1', 49);

        var invoice = Invoice.Create(
            invoiceId,
            publicId,
            seed.SubscriberId,
            companyId,
            businessPartnerId,
            seed.WarehouseId,
            docType: "01",
            estabCode: "001",
            emPointCode: "001",
            sequential: sequential,
            accessKey: accessKey,
            issueDate: issueDate,
            buyerIdType: "04",
            buyerIdNumber: "9999999999001",
            buyerNameSnapshot: "Cliente Test S.A.",
            buyerAddressSnapshot: null,
            paymentMethodCode: "01",
            paymentTermDays: 0,
            notes: null,
            createdBy: seed.UserId);

        var lineId = NextSnowflakeId();
        var line = InvoiceDetail.Create(
            lineId,
            invoiceId,
            seed.SubscriberId,
            companyId,
            lineNo: 1,
            seed.ProductId,
            productName: "Producto de prueba",
            sku: "SKU-INT",
            unitName: "UND",
            description: lineDescription,
            quantity: 1m,
            unitPrice: subtotal,
            discountAmount: 0m,
            taxCode: "2",
            taxRate: 12m,
            lineTax: taxTotal,
            issueDate: DateOnly.FromDateTime(issueDate));
        invoice.AddLine(line);

        var electronicId = NextSnowflakeId();
        var electronic = InvoiceElectronic.CreatePending(
            electronicId,
            invoiceId,
            seed.SubscriberId,
            companyId,
            DateOnly.FromDateTime(issueDate));
        invoice.AttachElectronic(electronic);

        invoice.Validate(seed.UserId);
        invoice.Authorize(
            seed.UserId,
            authNumber: sequential == "000000001" ? "AUTH-123" : "AUTH-456",
            authDate: issueDate,
            journalEntryId: Guid.NewGuid());
        electronic.MarkAuthorized(
            sequential == "000000001" ? "AUTH-123" : "AUTH-456",
            issueDate,
            xmlAuthPath: null);

        db.FiscalInvoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        return publicId;
    }
    /// <summary>
    /// Siembra los prerequisitos para tests de ventas:
    /// cuenta Revenue, cliente activo, SriSettings y opcionalmente stock inicial.
    /// </summary>
    internal static async Task SeedVentasPrerequisitesAsync(
        ErpDbContext db,
        IntegrationSeedData.SeedResult seed,
        decimal stockInicial = 10m,
        bool crearStockActual = true,
        CancellationToken ct = default)
    {
        var subscriberId = seed.SubscriberId;
        var userId   = seed.UserId;
        var productId = seed.ProductId;
        var bodegaId  = seed.WarehouseId;

        // Cuenta de ingresos (Revenue) para el asiento contable de venta
        var revenue = Account.Create(subscriberId, seed.CompanyId, "4.1.99", "Ventas pruebas", AccountType.Revenue, AccountNature.Credit, userId);
        db.Accounts.Add(revenue);

        // Cliente activo
        var cliente = BusinessPartner.Create(
            subscriberId:        subscriberId,
            identificationType:  "04",
            identificationNumber: "9999999999001",
            personType:          ERP.Domain.MasterData.Enums.PersonType.Legal,
            legalName:           "Cliente Test S.A.",
            createdBy:           userId);
        db.BusinessPartners.Add(cliente);

        // SriSettings (Environment 1 = pruebas)
        var sri = SriSettings.Create(
            subscriberId: subscriberId,
            companyId:    seed.CompanyId,
            certP12Path:  "simulado.p12",
            certPassword: "test",
            environment:  1,
            emissionType: 1,
            wsdlUrl:      "https://test-sri.example.com/wsdl",
            createdBy:    userId);
        db.SriSettings.Add(sri);

        await db.SaveChangesAsync(ct);

        if (crearStockActual && stockInicial > 0)
        {
            var stock = CurrentStock.Create(subscriberId, productId, bodegaId, userId, companyId: seed.CompanyId);
            stock.ApplyMovement(stockInicial, userId);
            db.CurrentStocks.Add(stock);

            var movInicial = StockMovement.Create(
                subscriberId, productId, bodegaId,
                StockMovementType.PositiveAdjust,
                quantity:         stockInicial,
                previousQuantity: 0,
                reference:       "Stock inicial prueba",
                sourceDocId:   null,
                sourceDocType: null,
                createdBy:           userId,
                companyId:           seed.CompanyId);
            db.StockMovements.Add(movInicial);

            await db.SaveChangesAsync(ct);
        }
    }
}














