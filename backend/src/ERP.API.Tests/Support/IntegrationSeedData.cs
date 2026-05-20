using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Branches.Entities;
using ERP.Domain.Common.Validators;
using ERP.Domain.Products.Entities;
using ERP.Domain.Subscribers.Entities;
using ERP.Infrastructure.Persistence;

namespace ERP.API.Tests.Support;

internal static class IntegrationSeedData
{
    internal sealed record SeedResult(
        Guid   SubscriberId,
        Guid   UserId,
        Guid   ProductId,
        Guid   WarehouseId,
        string ProveedorRuc,
        string ClaveAcceso49);

    private static string ValidSociedadPrivadaRuc()
    {
        for (var d = 0; d <= 9; d++)
        {
            var r = "179001691" + d + "001";
            if (RucValidator.EsRucValido(r))
                return r;
        }

        throw new InvalidOperationException("RUC de prueba no encontrado.");
    }

    /// <summary>XML mÃ­nimo compatible con <see cref="ERP.Infrastructure.Services.SriFacturaParser"/>.</summary>
    internal static string BuildFacturaXml(string clave49, string rucEmisor) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <factura id="comprobante" version="1.1.0">
          <infoTributaria>
            <razonSocial>PROVEEDOR INTEGRACION S.A.</razonSocial>
            <ruc>{rucEmisor}</ruc>
            <accessKey>{clave49}</accessKey>
            <estab>001</estab>
            <ptoEmi>001</ptoEmi>
            <secuencial>000000042</secuencial>
          </infoTributaria>
          <infoFactura>
            <issueDate>09/05/2026</issueDate>
            <totalSinImpuestos>50.00</totalSinImpuestos>
            <totalConImpuestos>
              <totalImpuesto>
                <valor>6.00</valor>
              </totalImpuesto>
            </totalConImpuestos>
            <importeTotal>56.00</importeTotal>
          </infoFactura>
          <detalles>
            <detalle>
              <codigoPrincipal>P-INT</codigoPrincipal>
              <descripcion>Item integraciÃ³n</descripcion>
              <cantidad>2</cantidad>
              <precioUnitario>25</precioUnitario>
              <descuento>0</descuento>
              <precioTotalSinImpuesto>50.00</precioTotalSinImpuesto>
            </detalle>
          </detalles>
        </factura>
        """;

    internal static async Task<SeedResult> SeedAsync(
        ErpDbContext          db,
        MutableCurrentSubscriber mt,
        MutableCurrentUser   mu,
        CancellationToken    ct)
    {
        var userId = Guid.NewGuid();
        mu.UserId = userId;

        var tenant = Subscriber.Create("Empresa integraciÃ³n", "empresa-int", userId);
        mt.SubscriberId = tenant.Id;
        var tid = tenant.Id;
        db.Subscribers.Add(tenant);
        await db.SaveChangesAsync(ct);

        var branch = Branch.Create(
            tid,
            "Matriz",
            "Av. Test",
            reference: null,
            phones: null,
            countryId: null,
            provinceId: null,
            cantonId: null,
            parishId: null,
            latitude: null,
            longitude: null,
            rechargeOption: null,
            isMainBranch: true,
            createdBy: userId);
        db.Branches.Add(branch);

        var line    = ProductLine.Create(tid, "L-INT", "LÃ­nea INT", userId);
        var category = ProductCategory.Create(tid, "C-INT", "Cat INT", line.Id, userId);
        var sub      = ProductSubcategory.Create(tid, "S-INT", "Sub INT", category.Id, userId);
        var uom      = UnitOfMeasure.Create(tid, "U", "Unidad", userId);
        var brand    = Brand.Create(tid, "B-INT", "Marca INT", userId);
        var ptype    = ProductType.Create(tid, "T-INT", "Tipo INT", userId);
        var tariff   = Tariff.Create(tid, "TR-INT", "Arancel INT", userId);

        db.ProductLines.Add(line);
        db.ProductCategories.Add(category);
        db.ProductSubcategories.Add(sub);
        db.UnitsOfMeasure.Add(uom);
        db.Brands.Add(brand);
        db.ProductTypes.Add(ptype);
        db.Tariffs.Add(tariff);

        var expense = Account.Create(
            tid, "5.1.99", "Gasto pruebas", AccountType.Expense, AccountNature.Debit, userId);
        var liability = Account.Create(
            tid, "2.1.99", "CxP pruebas", AccountType.Liability, AccountNature.Credit, userId);
        var caja = Account.Create(
            tid, "1.1.99", "Caja pruebas", AccountType.Asset, AccountNature.Debit, userId);

        db.Accounts.AddRange(expense, liability, caja);

        await db.SaveChangesAsync(ct);

        var bodega = Warehouse.Create(tid, branch.Id, "Warehouse Central", null, null, userId);
        db.Warehouses.Add(bodega);

        var product = Product.Create(
            tid,
            "SKU-INT-01",
            "Prod INT",
            "Producto integraciÃ³n stock",
            line.Id,
            category.Id,
            sub.Id,
            uom.Id,
            brand.Id,
            ptype.Id,
            tariff.Id,
            appliesVatOnSale: false,
            saleTaxId: null,
            saleVatAccountId: null,
            appliesVatOnPurchase: false,
            purchaseTaxId: null,
            purchaseVatAccountId: null,
            userId,
            purchaseCode: "SKU-INT-01",
            isService: false,
            tracksStock: true);

        db.Products.Add(product);

        await db.SaveChangesAsync(ct);

        var ruc   = ValidSociedadPrivadaRuc();
        var clave = ClaveAcceso49TestFactory.FromPrefix48(new string('6', 48));

        return new SeedResult(tid, userId, product.Id, bodega.Id, ruc, clave);
    }
}




