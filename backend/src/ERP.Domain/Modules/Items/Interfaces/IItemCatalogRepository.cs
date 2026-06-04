using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Products.Entities;

namespace ERP.Domain.Modules.Items.Interfaces;

/// <summary>
/// Repositorio unificado para todos los catálogos auxiliares del módulo Items:
/// ItemFamily, ItemCategory, ItemSubcategory, Brand.
/// </summary>
public interface IItemCatalogRepository
{
    // ── ItemFamily ─────────────────────────────────────────────────────────
    Task<ItemFamily?>                GetFamilyByIdAsync(Guid id, CancellationToken ct = default);
    Task<ItemFamily?>                GetFamilyByCodeAsync(string code, Guid subscriberId, CancellationToken ct = default);
    Task<IReadOnlyList<ItemFamily>>  GetFamiliesAsync(Guid subscriberId, CancellationToken ct = default);
    Task<bool>                       FamilyCodeExistsAsync(string code, Guid subscriberId, CancellationToken ct = default);
    Task                             AddFamilyAsync(ItemFamily family, CancellationToken ct = default);

    // ── ItemCategory ───────────────────────────────────────────────────────
    Task<ItemCategory?>              GetCategoryByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ItemCategory>> GetCategoriesAsync(Guid subscriberId, Guid? familyId = null, CancellationToken ct = default);
    Task<bool>                       CategoryCodeExistsAsync(string code, Guid familyId, Guid subscriberId, CancellationToken ct = default);
    Task<int>                        CountActiveSubcategoriesByCategoryAsync(Guid categoryId, CancellationToken ct = default);
    Task                             AddCategoryAsync(ItemCategory category, CancellationToken ct = default);

    // ── ItemSubcategory ────────────────────────────────────────────────────
    Task<ItemSubcategory?>              GetSubcategoryByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ItemSubcategory>> GetSubcategoriesAsync(Guid subscriberId, Guid? categoryId = null, CancellationToken ct = default);
    Task<bool>                          SubcategoryCodeExistsAsync(string code, Guid categoryId, Guid subscriberId, CancellationToken ct = default);
    Task                                AddSubcategoryAsync(ItemSubcategory sub, CancellationToken ct = default);

    // ── Brand ──────────────────────────────────────────────────────────────
    Task<Brand?>                GetBrandByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Brand>>  GetBrandsAsync(Guid subscriberId, CancellationToken ct = default);
    Task<bool>                  BrandCodeExistsAsync(string code, Guid subscriberId, CancellationToken ct = default);
    Task                        AddBrandAsync(Brand brand, CancellationToken ct = default);

    // ── Shared ─────────────────────────────────────────────────────────────
    Task SaveChangesAsync(CancellationToken ct = default);
}
