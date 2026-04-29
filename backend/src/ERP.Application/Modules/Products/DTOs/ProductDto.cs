namespace ERP.Application.Products.DTOs;

public record ProductDto(
    Guid Id,
    string SaleCode,
    string? PurchaseCode,
    string ShortName,
    string Description,
    Guid LineId,
    Guid CategoryId,
    Guid SubcategoryId,
    Guid UnitOfMeasureId,
    Guid BrandId,
    Guid ProductTypeId,
    Guid SaleTaxId,
    Guid PurchaseTaxId,
    Guid? ExciseTaxId,
    bool IsService,
    bool IsActive,
    bool AvailableOnWeb,
    bool AvailableOnMobile,
    bool IsForSale,
    DateTime CreatedAt
);