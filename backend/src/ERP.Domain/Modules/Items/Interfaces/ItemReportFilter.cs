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
    // solo ítems Product/Service según participación en inventario (Item.Nature), antes del orden y la paginación.
    bool? ParticipatesInInventory = null
);
