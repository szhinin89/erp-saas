using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

using MediatR;

namespace ERP.Application.Products.UseCases.CreateProductSubcategory;

public class CreateProductSubcategoryHandler : IRequestHandler<CreateProductSubcategoryCommand, Result<ProductSubcategoryDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public CreateProductSubcategoryHandler(
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

    public async Task<Result<ProductSubcategoryDto>> Handle(CreateProductSubcategoryCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;

        if (command.CategoryId == Guid.Empty)
            return Result<ProductSubcategoryDto>.Failure("Debe seleccionar una categoría.");

        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name))
            return Result<ProductSubcategoryDto>.Failure("Código y nombre son obligatorios.");

        var category = await _repo.GetProductCategoryByIdAsync(tenantId, command.CategoryId, ct);
        if (category is null)
            return Result<ProductSubcategoryDto>.Failure("La categoría indicada no existe.");
        if (!category.IsActive)
            return Result<ProductSubcategoryDto>.Failure("No se puede crear una subcategoría bajo una categoría deshabilitada.");

        var line = await _repo.GetProductLineByIdAsync(tenantId, category.LineId, ct);
        if (line is null || !line.IsActive)
            return Result<ProductSubcategoryDto>.Failure("La línea asociada a la categoría no está disponible o está deshabilitada.");

        if (await _repo.ProductSubcategoryCodeExistsAsync(tenantId, command.CategoryId, command.Code.Trim(), null, ct))
            return Result<ProductSubcategoryDto>.Failure("Ya existe una subcategoría con el mismo código en esta categoría.");

        var entity = ProductSubcategory.Create(tenantId, command.Code.Trim(), command.Name.Trim(), command.CategoryId, userId);
        await _repo.AddProductSubcategoryAsync(entity, ct);
        await _activity.AddAsync(UserActivity.Create(
            tenantId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "productSubcategory.create",
            entityType: "ProductSubcategory",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), ct);
        await _repo.SaveChangesAsync(ct);
        return Result<ProductSubcategoryDto>.Success(new ProductSubcategoryDto(entity.Id, entity.Code, entity.Name, entity.CategoryId, entity.IsActive));
    }
}

