using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;

using MediatR;

namespace ERP.Application.Products.UseCases.DisableProductCategory;

public class DisableProductCategoryHandler : IRequestHandler<DisableProductCategoryCommand, Result<ProductCategoryDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public DisableProductCategoryHandler(
        IProductCatalogRepository repo,
        IUserActivityRepository activity,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _activity = activity;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<ProductCategoryDto>> Handle(DisableProductCategoryCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;

        var entity = await _repo.GetProductCategoryByIdAsync(tenantId, command.Id, ct);
        if (entity is null)
            return Result<ProductCategoryDto>.Failure("Categoría no encontrada.");

        if (!entity.IsActive)
            return Result<ProductCategoryDto>.Failure("La categoría ya está deshabilitada.");

        var activeChildren = await _repo.CountActiveSubcategoriesByCategoryAsync(tenantId, command.Id, ct);
        if (activeChildren > 0)
            return Result<ProductCategoryDto>.Failure("No se puede deshabilitar la categoría mientras tenga subcategorías activas.");

        entity.Disable(userId);
        await _activity.AddAsync(UserActivity.Create(
            tenantId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "productCategory.disable",
            entityType: "ProductCategory",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<ProductCategoryDto>.Success(
            new ProductCategoryDto(entity.Id, entity.Code, entity.Name, entity.LineId, entity.IsActive));
    }
}
