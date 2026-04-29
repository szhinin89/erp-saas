namespace ERP.Application.Products.UseCases.CreateProduct;

public record CreateProductCommand(
    string SaleCode,
    string ShortName,
    string Description,
    Guid LineId,
    Guid CategoryId,
    Guid SubcategoryId,
    Guid UnitOfMeasureId,
    Guid BrandId,
    Guid ProductTypeId,
    Guid TariffId,
    Guid SaleTaxId,
    Guid PurchaseTaxId,
    string? PurchaseCode = null,
    Guid? ExciseTaxId = null,
    bool IsService = false,
    bool TracksLot = false,
    bool TracksSeries = false,
    bool HasRecipe = false,
    bool StockWithDecimal = false,
    bool AvailableOnWeb = false,
    bool AvailableOnMobile = false,
    bool IsForSale = true
);