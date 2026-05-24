using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Support;

/// <summary>
/// Helpers de seed compartidos entre los tests de Ventas (end-to-end, stock, HTTP).
/// </summary>
internal static class VentasEndToEndHelpers
{
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
        var revenue = Account.Create(
            subscriberId, "4.1.99", "Ventas pruebas", AccountType.Revenue, AccountNature.Credit, userId);
        db.Accounts.Add(revenue);

        // Cliente activo
        var cliente = BusinessPartner.Create(
            subscriberId:        subscriberId,
            identificationType:  "RUC",
            identificationNumber: "9999999999001",
            legalName:           "Cliente Test S.A.",
            createdBy:           userId);
        db.BusinessPartners.Add(cliente);

        // SriSettings (Environment 1 = pruebas)
        var sri = SriSettings.Create(
            subscriberId:              subscriberId,
            ruc:            "9999999999999",
            legalName:           "Empresa Test SRL",
            tradeName:       null,
            mainAddress:       "Av. Integracion 001",
            requiresAccounting:  false,
            specialTaxpayer: null,
            estabCode:       "001",
            emPointCode:          "001",
            currentSequential:      1,
            certP12Path:    "simulado.p12",
            certPassword:   "test",
            environment:              1,
            emissionType:           1,
            wsdlUrl:    "https://test-sri.example.com/wsdl",
            createdBy:             userId);
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














