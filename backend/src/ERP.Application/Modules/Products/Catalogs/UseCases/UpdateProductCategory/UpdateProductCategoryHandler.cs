using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.UpdateProductCategory;

public class UpdateProductCategoryHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public UpdateProductCategoryHandler(
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

    public async Task<Result<ProductCategoryDto>> HandleAsync(UpdateProductCategoryCommand command, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;

        if (command.LineId == Guid.Empty)
            return Result<ProductCategoryDto>.Failure("Debe seleccionar una línea de producto.");

        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name))
            return Result<ProductCategoryDto>.Failure("Código y nombre son obligatorios.");

        var entity = await _repo.GetProductCategoryByIdAsync(tenantId, command.Id, ct);
        if (entity is null)
            return Result<ProductCategoryDto>.Failure("Categoría no encontrada.");

        var line = await _repo.GetProductLineByIdAsync(tenantId, command.LineId, ct);
        if (line is null)
            return Result<ProductCategoryDto>.Failure("La línea indicada no existe.");
        if (!line.IsActive)
            return Result<ProductCategoryDto>.Failure("No se puede asociar la categoría a una línea deshabilitada.");

        if (await _repo.ProductCategoryCodeExistsAsync(tenantId, command.LineId, command.Code.Trim(), command.Id, ct))
            return Result<ProductCategoryDto>.Failure("Ya existe otra categoría con el mismo código en esta línea.");

        entity.Update(command.Code.Trim(), command.Name.Trim(), command.LineId, userId);
        await _activity.AddAsync(UserActivity.Create(
            tenantId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "catalog",
            action: "productCategory.update",
            entityType: "ProductCategory",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<ProductCategoryDto>.Success(
            new ProductCategoryDto(entity.Id, entity.Code, entity.Name, entity.LineId, entity.IsActive));
    }
}
