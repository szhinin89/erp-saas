using ERP.Domain.Modules.Contabilidad.Entities;
using ERP.Domain.Modules.Contabilidad.Enums;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Modules.Ventas.Entities;
using ERP.Domain.Modules.Inventario.Entities;
using ERP.Domain.Modules.Inventario.Enums;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Support;

/// <summary>
/// Helpers de seed compartidos entre los tests de Ventas (end-to-end, stock, HTTP).
/// </summary>
internal static class VentasEndToEndHelpers
{
    /// <summary>
    /// Siembra los prerequisitos para tests de ventas:
    /// cuenta Revenue, cliente activo, ConfiguracionSRI y opcionalmente stock inicial.
    /// </summary>
    internal static async Task SeedVentasPrerequisitesAsync(
        ErpDbContext db,
        IntegrationSeedData.SeedResult seed,
        decimal stockInicial = 10m,
        bool crearStockActual = true,
        CancellationToken ct = default)
    {
        var tenantId = seed.TenantId;
        var userId   = seed.UserId;
        var productId = seed.ProductId;
        var bodegaId  = seed.BodegaId;

        // Cuenta de ingresos (Revenue) para el asiento contable de venta
        var revenue = Account.Create(
            tenantId, "4.1.99", "Ventas pruebas", AccountType.Revenue, AccountNature.Credit, userId);
        db.Accounts.Add(revenue);

        // Cliente activo
        var cliente = Customer.Create(
            tenantId,
            identificationType:   "RUC",
            identificationNumber: "9999999999001",
            legalName:            "Cliente Test S.A.",
            tradeName:            null,
            addressLine:          "Av. Test 123",
            phone:                null,
            email:                null,
            notes:                null,
            createdBy:            userId);
        db.Customers.Add(cliente);

        // ConfiguracionSRI (Ambiente 1 = pruebas)
        var sri = ConfiguracionSRI.Create(
            tenantId:              tenantId,
            rucEmpresa:            "9999999999999",
            razonSocial:           "Empresa Test SRL",
            nombreComercial:       null,
            direccionMatriz:       "Av. Integracion 001",
            obligadoContabilidad:  false,
            contribuyenteEspecial: null,
            establecimiento:       "001",
            puntoEmision:          "001",
            secuencialActual:      1,
            certificadoP12Path:    "simulado.p12",
            certificadoPassword:   "test",
            ambiente:              1,
            tipoEmision:           1,
            urlSriAutorizacion:    "https://test-sri.example.com/wsdl",
            createdBy:             userId);
        db.ConfiguracionSRIs.Add(sri);

        await db.SaveChangesAsync(ct);

        if (crearStockActual && stockInicial > 0)
        {
            var stock = StockActual.Create(tenantId, productId, bodegaId, userId);
            stock.AplicarMovimiento(stockInicial, userId);
            db.StockActual.Add(stock);

            var movInicial = InventarioMovimiento.Create(
                tenantId, productId, bodegaId,
                TipoMovimientoInventario.AjustePositivo,
                cantidad:         stockInicial,
                cantidadAnterior: 0,
                referencia:       "Stock inicial prueba",
                documentoOrigenId:   null,
                documentoOrigenTipo: null,
                createdBy:           userId);
            db.InventarioMovimientos.Add(movInicial);

            await db.SaveChangesAsync(ct);
        }
    }
}
