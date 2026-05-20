using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;

using MediatR;

namespace ERP.Application.Products.UseCases.UpdateProductCategory;

public class UpdateProductCategoryHandler : IRequestHandler<UpdateProductCategoryCommand, Result<ProductCategoryDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public UpdateProductCategoryHandler(
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

    public async Task<Result<ProductCategoryDto>> Handle(UpdateProductCategoryCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId = _currentUser.UserId;

        if (command.LineId == Guid.Empty)
            return Result<ProductCategoryDto>.Failure("Debe seleccionar una línea de producto.");

        if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name))
            return Result<ProductCategoryDto>.Failure("Código y nombre son obligatorios.");

        var entity = await _repo.GetProductCategoryByIdAsync(subscriberId, command.Id, ct);
        if (entity is null)
            return Result<ProductCategoryDto>.Failure("Categoría no encontrada.");

        var line = await _repo.GetProductLineByIdAsync(subscriberId, command.LineId, ct);
        if (line is null)
            return Result<ProductCategoryDto>.Failure("La línea indicada no existe.");
        if (!line.IsActive)
            return Result<ProductCategoryDto>.Failure("No se puede asociar la categoría a una línea deshabilitada.");

        if (await _repo.ProductCategoryCodeExistsAsync(subscriberId, command.LineId, command.Code.Trim(), command.Id, ct))
            return Result<ProductCategoryDto>.Failure("Ya existe otra categoría con el mismo código en esta línea.");

        entity.Update(command.Code.Trim(), command.Name.Trim(), command.LineId, userId);
        await _activity.AddAsync(UserActivity.Create(
            subscriberId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "productCategory.update",
            entityType: "ProductCategory",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<ProductCategoryDto>.Success(
            new ProductCategoryDto(entity.Id, entity.Code, entity.Name, entity.LineId, entity.IsActive));
    }
}
