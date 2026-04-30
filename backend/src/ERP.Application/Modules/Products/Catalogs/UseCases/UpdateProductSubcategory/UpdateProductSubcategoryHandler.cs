using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.UpdateProductSubcategory;

public class UpdateProductSubcategoryHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public UpdateProductSubcategoryHandler(
        IProductCatalogRepository repo,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<ProductSubcategoryDto>> HandleAsync(UpdateProductSubcategoryCommand command, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;

        if (command.CategoryId == Guid.Empty)
            return Result<ProductSubcategoryDto>.Failure("Debe seleccionar una categoría.");

        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name))
            return Result<ProductSubcategoryDto>.Failure("Código y nombre son obligatorios.");

        var entity = await _repo.GetProductSubcategoryByIdAsync(tenantId, command.Id, ct);
        if (entity is null)
            return Result<ProductSubcategoryDto>.Failure("Subcategoría no encontrada.");

        var category = await _repo.GetProductCategoryByIdAsync(tenantId, command.CategoryId, ct);
        if (category is null)
            return Result<ProductSubcategoryDto>.Failure("La categoría indicada no existe.");
        if (!category.IsActive)
            return Result<ProductSubcategoryDto>.Failure("No se puede asociar la subcategoría a una categoría deshabilitada.");

        var line = await _repo.GetProductLineByIdAsync(tenantId, category.LineId, ct);
        if (line is null || !line.IsActive)
            return Result<ProductSubcategoryDto>.Failure("La línea asociada a la categoría no está disponible o está deshabilitada.");

        if (await _repo.ProductSubcategoryCodeExistsAsync(tenantId, command.CategoryId, command.Code.Trim(), command.Id, ct))
            return Result<ProductSubcategoryDto>.Failure("Ya existe otra subcategoría con el mismo código en esta categoría.");

        entity.Update(command.Code.Trim(), command.Name.Trim(), command.CategoryId, userId);
        await _repo.SaveChangesAsync(ct);

        return Result<ProductSubcategoryDto>.Success(
            new ProductSubcategoryDto(entity.Id, entity.Code, entity.Name, entity.CategoryId, entity.IsActive));
    }
}
