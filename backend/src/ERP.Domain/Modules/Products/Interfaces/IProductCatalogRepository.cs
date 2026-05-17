using ERP.Domain.Products.Entities;

namespace ERP.Domain.Products.Interfaces;

public interface IProductCatalogRepository
{
    // Tax rates
    Task AddTaxRateAsync(TaxRate taxRate, CancellationToken ct = default);
    Task<IReadOnlyList<TaxRate>> GetTaxRatesAsync(Guid tenantId, TaxRateType? type = null, bool onlyActive = true, CancellationToken ct = default);

    // Catalogs
    Task AddBrandAsync(Brand brand, CancellationToken ct = default);
    Task<Brand?> GetBrandByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Brand>> GetBrandsAsync(Guid tenantId, bool onlyActive = true, CancellationToken ct = default);

    Task AddProductTypeAsync(ProductType type, CancellationToken ct = default);
    Task<ProductType?> GetProductTypeByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ProductType>> GetProductTypesAsync(Guid tenantId, bool onlyActive = true, CancellationToken ct = default);

    Task AddUnitOfMeasureAsync(UnitOfMeasure unit, CancellationToken ct = default);
    Task<IReadOnlyList<UnitOfMeasure>> GetUnitsOfMeasureAsync(Guid tenantId, bool onlyActive = true, CancellationToken ct = default);

    Task AddTariffAsync(Tariff tariff, CancellationToken ct = default);
    Task<IReadOnlyList<Tariff>> GetTariffsAsync(Guid tenantId, bool onlyActive = true, CancellationToken ct = default);

    Task AddProductLineAsync(ProductLine line, CancellationToken ct = default);
    /// <param name="activeFilter">null = todos; true = solo activos; false = solo inactivos.</param>
    Task<IReadOnlyList<ProductLine>> GetProductLinesAsync(Guid tenantId, bool? activeFilter = true, string? search = null, CancellationToken ct = default);
    Task<ProductLine?> GetProductLineByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> ProductLineCodeExistsAsync(Guid tenantId, string code, Guid? excludeId = null, CancellationToken ct = default);
    Task<int> CountActiveCategoriesByLineAsync(Guid tenantId, Guid lineId, CancellationToken ct = default);

    Task AddProductCategoryAsync(ProductCategory category, CancellationToken ct = default);
    Task<IReadOnlyList<ProductCategoryListRow>> GetProductCategoryListRowsAsync(
        Guid tenantId, Guid? lineId = null, bool? activeFilter = true, string? search = null, CancellationToken ct = default);
    Task<ProductCategory?> GetProductCategoryByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> ProductCategoryCodeExistsAsync(Guid tenantId, Guid lineId, string code, Guid? excludeId = null, CancellationToken ct = default);
    Task<int> CountActiveSubcategoriesByCategoryAsync(Guid tenantId, Guid categoryId, CancellationToken ct = default);

    Task AddProductSubcategoryAsync(ProductSubcategory subcategory, CancellationToken ct = default);
    Task<IReadOnlyList<ProductSubcategoryListRow>> GetProductSubcategoryListRowsAsync(
        Guid tenantId, Guid? lineId = null, Guid? categoryId = null, bool? activeFilter = true, string? search = null, CancellationToken ct = default);
    Task<ProductSubcategory?> GetProductSubcategoryByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> ProductSubcategoryCodeExistsAsync(Guid tenantId, Guid categoryId, string code, Guid? excludeId = null, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

