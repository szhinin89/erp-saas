using ERP.Domain.Modules.Items.Enums;

namespace ERP.Domain.Modules.Items.Interfaces;

public record ItemReportFilter(
    string? Search        = null,
    string? Sku           = null,
    bool?   IsActive      = null,
    bool?   IsForSale     = null,
    bool?   IsFavorite    = null,
    bool?   IsEcommerce   = null,
    ItemType? ItemType    = null,
    Guid?   CategoryNodeId = null,
    Guid?   BrandId       = null,
    string? Barcode       = null
);
