namespace ERP.Application.Products.DTOs;

public record ProductReportItemDto(
    Guid Id,
    string SaleCode,
    string? PurchaseCode,
    string ShortName,
    string Description,
    bool IsFavorite,
    bool IsForSale,
    bool IsActive,
    bool IsEcommerceActive,
    bool IsService,
    Guid LineId,
    Guid CategoryId,
    Guid SubcategoryId,
    Guid BrandId,
    Guid ProductTypeId,
    DateTime CreatedAt
);

