using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.CreateProductCategory;

public class CreateProductCategoryHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public CreateProductCategoryHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant, ICurrentUser currentUser)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<ProductCategoryDto>> HandleAsync(CreateProductCategoryCommand command, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;

        if (command.LineId == Guid.Empty)
            return Result<ProductCategoryDto>.Failure("Debe seleccionar una línea de producto.");

        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name))
            return Result<ProductCategoryDto>.Failure("Código y nombre son obligatorios.");

        var line = await _repo.GetProductLineByIdAsync(tenantId, command.LineId, ct);
        if (line is null)
            return Result<ProductCategoryDto>.Failure("La línea indicada no existe.");
        if (!line.IsActive)
            return Result<ProductCategoryDto>.Failure("No se puede crear una categoría bajo una línea deshabilitada.");

        if (await _repo.ProductCategoryCodeExistsAsync(tenantId, command.LineId, command.Code.Trim(), null, ct))
            return Result<ProductCategoryDto>.Failure("Ya existe una categoría con el mismo código en esta línea.");

        var entity = ProductCategory.Create(tenantId, command.Code.Trim(), command.Name.Trim(), command.LineId, userId);
        await _repo.AddProductCategoryAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<ProductCategoryDto>.Success(new ProductCategoryDto(entity.Id, entity.Code, entity.Name, entity.LineId, entity.IsActive));
    }
}

