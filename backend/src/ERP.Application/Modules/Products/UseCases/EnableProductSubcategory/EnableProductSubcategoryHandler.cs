using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;

using MediatR;

namespace ERP.Application.Products.UseCases.EnableProductSubcategory;

public class EnableProductSubcategoryHandler : IRequestHandler<EnableProductSubcategoryCommand, Result<ProductSubcategoryDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public EnableProductSubcategoryHandler(
        IProductCatalogRepository repo,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _activity = activity;
        _currentSubscriber = currentSubscriber;
        _currentUser = currentUser;
    }

    public async Task<Result<ProductSubcategoryDto>> Handle(EnableProductSubcategoryCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId = _currentUser.UserId;

        var entity = await _repo.GetProductSubcategoryByIdAsync(subscriberId, command.Id, ct);
        if (entity is null)
            return Result<ProductSubcategoryDto>.Failure("Subcategoría no encontrada.");

        if (entity.IsActive)
            return Result<ProductSubcategoryDto>.Failure("La subcategoría ya está activa.");

        var category = await _repo.GetProductCategoryByIdAsync(subscriberId, entity.CategoryId, ct);
        if (category is null || !category.IsActive)
            return Result<ProductSubcategoryDto>.Failure("No se puede reactivar: la categoría padre no existe o está deshabilitada.");

        var line = await _repo.GetProductLineByIdAsync(subscriberId, category.LineId, ct);
        if (line is null || !line.IsActive)
            return Result<ProductSubcategoryDto>.Failure("No se puede reactivar: la línea padre no existe o está deshabilitada.");

        entity.Enable(userId);
        await _activity.AddAsync(UserActivity.Create(
            subscriberId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "productSubcategory.enable",
            entityType: "ProductSubcategory",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<ProductSubcategoryDto>.Success(
            new ProductSubcategoryDto(entity.Id, entity.Code, entity.Name, entity.CategoryId, entity.IsActive));
    }
}
