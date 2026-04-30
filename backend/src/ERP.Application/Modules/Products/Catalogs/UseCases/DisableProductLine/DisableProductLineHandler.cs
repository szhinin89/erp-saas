using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.DisableProductLine;

public class DisableProductLineHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public DisableProductLineHandler(
        IProductCatalogRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<ProductLineDto>> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;

        var entity = await _repo.GetProductLineByIdAsync(tenantId, id, ct);
        if (entity is null)
            return Result<ProductLineDto>.Failure("Línea no encontrada.");

        if (!entity.IsActive)
            return Result<ProductLineDto>.Failure("La línea ya está deshabilitada.");

        var activeChildren = await _repo.CountActiveCategoriesByLineAsync(tenantId, id, ct);
        if (activeChildren > 0)
            return Result<ProductLineDto>.Failure("No se puede deshabilitar la línea mientras tenga categorías activas.");

        entity.Disable(userId);
        await _repo.SaveChangesAsync(ct);

        return Result<ProductLineDto>.Success(new ProductLineDto(entity.Id, entity.Code, entity.Name, entity.IsActive));
    }
}
