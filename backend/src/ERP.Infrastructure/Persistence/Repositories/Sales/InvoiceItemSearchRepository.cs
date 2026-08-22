using ERP.Application.Modules.Sales;
using ERP.Application.Modules.Sales.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Sales;

/// <summary>
/// Read-model repository for the invoice product search dropdown.
/// Returns raw InvoiceItemMatch records — no tax display strings, no FinalSalePrice.
/// Tax enrichment is performed by SearchItemsForInvoiceHandler via ISriCatalogResolver
/// + SriTaxCalculator to maintain a single source of truth for tax arithmetic.
///
/// Matches by SKU/name/description AND by real product barcode (ItemVariantBarcode.Code) —
/// a POS/retail cashier scans the barcode printed on the product, which is independent from
/// the internal SKU. ItemVariantBarcode does not implement ITenantScopedEntity (unlike Item),
/// so it has no automatic global tenant query filter — every subquery below filters TenantId
/// explicitly (fail-closed, see CLAUDE.md multi-tenant rule).
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
        var trimmedQuery = query.Trim();
        var pattern = $"%{trimmedQuery}%";

        // Warehouse name resolved once — constant for all rows in this search
        var warehouseName = warehouseId.HasValue
            ? await _db
                .Warehouses.Where(w => w.Id == warehouseId.Value)
                .Select(w => w.Name)
                .FirstOrDefaultAsync(ct)
            : null;

        // Rank 0 = barcode exacto, 1 = SKU exacto, 2 = barcode/SKU parcial, 3 = nombre/descripción —
        // así un escaneo de código de barras real nunca pierde contra una coincidencia parcial de
        // texto en otro producto (riesgo de agregar el ítem equivocado en caja). Se resuelve en una
        // consulta separada, liviana, y luego se re-ordena el resultado final en memoria — evita
        // traducir "ToList().IndexOf(...)" dentro de la consulta pesada de abajo (no traducible a SQL).
        var rankedIds = await _db
            .Items.Where(i => i.TenantId == tenantId && i.IsActive && i.SaleConfig.IsForSale)
            .Select(i => new
            {
                i.Id,
                HasBarcodeExact = _db.ItemVariantBarcodes.Any(b =>
                    b.ItemId == i.Id
                    && b.TenantId == tenantId
                    && b.IsActive
                    && EF.Functions.ILike(b.Code, trimmedQuery)
                ),
                HasBarcodePartial = _db.ItemVariantBarcodes.Any(b =>
                    b.ItemId == i.Id
                    && b.TenantId == tenantId
                    && b.IsActive
                    && EF.Functions.ILike(b.Code, pattern)
                ),
                // SALES-PRESENTATIONS-03: un código de barras de presentación (ej. la caja x12
                // tiene su propio barcode impreso) debe rankear igual que un barcode exacto de
                // ItemVariantBarcode — es igualmente un escaneo real, no una coincidencia de texto.
                HasPackagingBarcodeExact = i.PackagingLevels.Any(p =>
                    p.IsActive
                    && p.Barcode != null
                    && EF.Functions.ILike(p.Barcode, trimmedQuery)
                ),
                SkuExact = EF.Functions.ILike(i.Code.SKU, trimmedQuery),
                SkuPartial = EF.Functions.ILike(i.Code.SKU, pattern),
                NamePartial =
                    EF.Functions.ILike(i.Code.ShortName, pattern)
                    || EF.Functions.ILike(i.Code.Description, pattern),
                ShortNameForSort = i.Code.ShortName,
            })
            .Where(x => x.HasBarcodePartial || x.SkuPartial || x.NamePartial)
            .Select(x => new
            {
                x.Id,
                x.ShortNameForSort,
                Rank = (x.HasBarcodeExact || x.HasPackagingBarcodeExact) ? 0
                    : x.SkuExact ? 1
                    : (x.HasBarcodePartial || x.SkuPartial) ? 2
                    : 3,
            })
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.ShortNameForSort)
            .Take(pageSize)
            .Select(x => x.Id)
            .ToListAsync(ct);

        if (rankedIds.Count == 0)
            return Array.Empty<InvoiceItemMatch>();

        var matches = await _db
            .Items.Where(i => rankedIds.Contains(i.Id))
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
                i.TaxConfig.ExciseTaxCode,
                i.DefaultUomCode,
                i.PackagingLevels.Where(p => p.IsActive)
                    .OrderBy(p => p.Level)
                    .Select(p => new InvoiceItemPackagingLevelDto(
                        p.Id,
                        p.Name,
                        p.UomCode,
                        p.BaseQuantity,
                        p.Barcode,
                        p.IsBaseUnit,
                        p.IsSaleDefault
                    ))
                    .ToList(),
                i.PackagingLevels.Where(p =>
                        p.IsActive && p.Barcode != null && EF.Functions.ILike(p.Barcode, trimmedQuery)
                    )
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefault()
            ))
            .ToListAsync(ct);

        // Contains() no garantiza el orden de rankedIds — se reordena en memoria (pageSize
        // acotado a 20 por SearchItemsForInvoiceHandler, sin costo real).
        var order = rankedIds
            .Select((id, idx) => (id, idx))
            .ToDictionary(t => t.id, t => t.idx);
        matches.Sort((a, b) => order[a.Id].CompareTo(order[b.Id]));
        return matches;
    }
}
