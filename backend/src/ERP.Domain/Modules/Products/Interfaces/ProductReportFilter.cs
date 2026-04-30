namespace ERP.Domain.Products.Interfaces;

public record ProductReportFilter(
    string? Search = null,
    string? SaleCode = null,
    string? PurchaseCode = null,
    string? Barcode = null,
    bool? IsFavorite = null,
    bool? IsForSale = null,
    bool? IsActive = null,
    bool? IsEcommerceActive = null,
    bool? IsService = null,
    Guid? LineId = null,
    Guid? CategoryId = null,
    Guid? SubcategoryId = null,
    Guid? BrandId = null,
    Guid? ProductTypeId = null
);

