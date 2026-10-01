namespace ERP.Domain.Modules.Items.Interfaces;

public record ItemReportFilter(
    string? Search = null,
    string? Sku = null,
    bool? IsActive = null,
    bool? IsForSale = null,
    bool? IsFavorite = null,
    bool? IsEcommerce = null,
    Guid? ItemTypeId = null,
    Guid? CategoryNodeId = null,
    Guid? BrandId = null,
    string? Barcode = null,
    // ZH-INVENTORY-STOCK-ITEM-LOOKUP-01 — null = sin filtro (comportamiento previo); true/false =
    // solo ítems con/sin control de stock (StockConfig.TracksStock), antes del orden y la paginación.
    bool? TracksStock = null
);
