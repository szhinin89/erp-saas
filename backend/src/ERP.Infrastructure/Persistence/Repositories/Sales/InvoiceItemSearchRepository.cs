using ERP.Application.Modules.Sales;
using ERP.Application.Modules.Sales.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Sales;

/// <summary>
/// Read-model repository for the invoice product search dropdown.
/// Returns raw InvoiceItemMatch records — no tax display strings, no FinalSalePrice.
/// Tax enrichment is performed by SearchItemsForInvoiceHandler via ISriCatalogResolver
/// + SriTaxCalculator to maintain a single source of truth for tax arithmetic.
/// </summary>
public sealed class InvoiceItemSearchRepository : IInvoiceItemSearchRepository
{
    private readonly ErpDbContext _db;

    public InvoiceItemSearchRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<InvoiceItemMatch>> SearchAsync(
        Guid tenantId,
        Guid companyId,
        string query,
        Guid? warehouseId,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var pattern = $"%{query}%";

        // Warehouse name resolved once — constant for all rows in this search
        var warehouseName = warehouseId.HasValue
            ? await _db
                .Warehouses.Where(w => w.Id == warehouseId.Value)
                .Select(w => w.Name)
                .FirstOrDefaultAsync(ct)
            : null;

        return await _db
            .Items.Where(i =>
                i.TenantId == tenantId
                && i.IsActive
                && i.SaleConfig.IsForSale
                && (
                    EF.Functions.ILike(i.Code.SKU, pattern)
                    || EF.Functions.ILike(i.Code.ShortName, pattern)
                    || EF.Functions.ILike(i.Code.Description, pattern)
                )
            )
            .OrderBy(i => i.Code.ShortName)
            .Take(pageSize)
            .Select(i => new InvoiceItemMatch(
                i.Id,
                i.Code.SKU,
                i.Code.ShortName,
                i.CategoryNodeId.HasValue
                    ? _db
                        .ItemCategoryNodes.Where(c => c.Id == i.CategoryNodeId)
                        .Select(c => c.Name)
                        .FirstOrDefault()
                    : null,
                _db.SriUoms.Where(u => u.Code == i.DefaultUomCode)
                    .Select(u => u.Abbrev ?? u.Name)
                    .FirstOrDefault()
                    ?? i.DefaultUomCode,
                i.StockConfig.TracksStock,
                warehouseName,
                warehouseId != null
                    ? _db
                        .CurrentStocks.Where(cs =>
                            cs.ProductId == i.Id
                            && cs.WarehouseId == warehouseId.Value
                            && cs.CompanyId == companyId
                            && cs.TenantId == tenantId
                        )
                        .Select(cs => (decimal?)(cs.Quantity - cs.ReservedQuantity))
                        .FirstOrDefault()
                    : (decimal?)null,
                warehouseId != null
                    ? _db
                        .CurrentStocks.Where(cs =>
                            cs.ProductId == i.Id
                            && cs.WarehouseId == warehouseId.Value
                            && cs.CompanyId == companyId
                            && cs.TenantId == tenantId
                            && cs.Quantity > 0
                        )
                        .Select(cs => (decimal?)(cs.TotalStockValue / cs.Quantity))
                        .FirstOrDefault()
                    : (decimal?)null,
                // Precio base (SSOT, Motor de Pricing v2). NOTA: este dropdown de búsqueda
                // muestra el precio base sin resolver reglas de PriceList/PricingRule —
                // la resolución completa vía PricingResolver se aplica en el flujo de
                // guardado de la línea de venta (fuera de alcance de este read-model batch).
                i.BaseSalePrice,
                i.TaxConfig.SaleVatCode,
                i.TaxConfig.ExciseTaxCode
            ))
            .ToListAsync(ct);
    }
}
