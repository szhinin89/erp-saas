using ERP.Domain.Products.Entities;

namespace ERP.Domain.Products.Interfaces;

public interface IProductCatalogRepository
{
    // Tax rates
    Task AddTaxRateAsync(TaxRate taxRate, CancellationToken ct = default);
    Task<IReadOnlyList<TaxRate>> GetTaxRatesAsync(Guid subscriberId, TaxRateType? type = null, bool onlyActive = true, CancellationToken ct = default);

    // Catalogs
    Task AddBrandAsync(Brand brand, CancellationToken ct = default);
    Task<Brand?> GetBrandByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Brand>> GetBrandsAsync(Guid subscriberId, bool onlyActive = true, CancellationToken ct = default);

    Task AddProductTypeAsync(ProductType type, CancellationToken ct = default);
    Task<ProductType?> GetProductTypeByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ProductType>> GetProductTypesAsync(Guid subscriberId, bool onlyActive = true, CancellationToken ct = default);

    Task AddUnitOfMeasureAsync(UnitOfMeasure unit, CancellationToken ct = default);
    Task<UnitOfMeasure?> GetUnitOfMeasureByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<UnitOfMeasure>> GetUnitsOfMeasureAsync(Guid subscriberId, bool onlyActive = true, CancellationToken ct = default);

    Task AddTariffAsync(Tariff tariff, CancellationToken ct = default);
    Task<IReadOnlyList<Tariff>> GetTariffsAsync(Guid subscriberId, bool onlyActive = true, CancellationToken ct = default);

    Task AddProductLineAsync(ProductLine line, CancellationToken ct = default);
    /// <param name="activeFilter">null = todos; true = solo activos; false = solo inactivos.</param>
    Task<IReadOnlyList<ProductLine>> GetProductLinesAsync(Guid subscriberId, bool? activeFilter = true, string? search = null, CancellationToken ct = default);
    Task<ProductLine?> GetProductLineByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default);
    Task<bool> ProductLineCodeExistsAsync(Guid subscriberId, string code, Guid? excludeId = null, CancellationToken ct = default);
    Task<int> CountActiveCategoriesByLineAsync(Guid subscriberId, Guid lineId, CancellationToken ct = default);

    Task AddProductCategoryAsync(ProductCategory category, CancellationToken ct = default);
    Task<IReadOnlyList<ProductCategoryListRow>> GetProductCategoryListRowsAsync(
        Guid subscriberId, Guid? lineId = null, bool? activeFilter = true, string? search = null, CancellationToken ct = default);
    Task<ProductCategory?> GetProductCategoryByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default);
    Task<bool> ProductCategoryCodeExistsAsync(Guid subscriberId, Guid lineId, string code, Guid? excludeId = null, CancellationToken ct = default);
    Task<int> CountActiveSubcategoriesByCategoryAsync(Guid subscriberId, Guid categoryId, CancellationToken ct = default);

    Task AddProductSubcategoryAsync(ProductSubcategory subcategory, CancellationToken ct = default);
    Task<IReadOnlyList<ProductSubcategoryListRow>> GetProductSubcategoryListRowsAsync(
        Guid subscriberId, Guid? lineId = null, Guid? categoryId = null, bool? activeFilter = true, string? search = null, CancellationToken ct = default);
    Task<ProductSubcategory?> GetProductSubcategoryByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default);
    Task<bool> ProductSubcategoryCodeExistsAsync(Guid subscriberId, Guid categoryId, string code, Guid? excludeId = null, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

