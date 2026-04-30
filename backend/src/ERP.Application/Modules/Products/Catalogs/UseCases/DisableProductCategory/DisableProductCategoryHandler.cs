using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.DisableProductCategory;

public class DisableProductCategoryHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public DisableProductCategoryHandler(
        IProductCatalogRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<ProductCategoryDto>> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;

        var entity = await _repo.GetProductCategoryByIdAsync(tenantId, id, ct);
        if (entity is null)
            return Result<ProductCategoryDto>.Failure("Categoría no encontrada.");

        if (!entity.IsActive)
            return Result<ProductCategoryDto>.Failure("La categoría ya está deshabilitada.");

        var activeChildren = await _repo.CountActiveSubcategoriesByCategoryAsync(tenantId, id, ct);
        if (activeChildren > 0)
            return Result<ProductCategoryDto>.Failure("No se puede deshabilitar la categoría mientras tenga subcategorías activas.");

        entity.Disable(userId);
        await _repo.SaveChangesAsync(ct);

        return Result<ProductCategoryDto>.Success(
            new ProductCategoryDto(entity.Id, entity.Code, entity.Name, entity.LineId, entity.IsActive));
    }
}
