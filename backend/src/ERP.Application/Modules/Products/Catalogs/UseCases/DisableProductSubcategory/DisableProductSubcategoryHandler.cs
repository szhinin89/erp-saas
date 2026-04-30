using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.DisableProductSubcategory;

public class DisableProductSubcategoryHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public DisableProductSubcategoryHandler(
        IProductCatalogRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<ProductSubcategoryDto>> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;

        var entity = await _repo.GetProductSubcategoryByIdAsync(tenantId, id, ct);
        if (entity is null)
            return Result<ProductSubcategoryDto>.Failure("Subcategoría no encontrada.");

        if (!entity.IsActive)
            return Result<ProductSubcategoryDto>.Failure("La subcategoría ya está deshabilitada.");

        entity.Disable(userId);
        await _repo.SaveChangesAsync(ct);

        return Result<ProductSubcategoryDto>.Success(
            new ProductSubcategoryDto(entity.Id, entity.Code, entity.Name, entity.CategoryId, entity.IsActive));
    }
}
