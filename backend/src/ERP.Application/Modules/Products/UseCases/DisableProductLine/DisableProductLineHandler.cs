using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;

using MediatR;

namespace ERP.Application.Products.UseCases.DisableProductLine;

public class DisableProductLineHandler : IRequestHandler<DisableProductLineCommand, Result<ProductLineDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public DisableProductLineHandler(
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

    public async Task<Result<ProductLineDto>> Handle(DisableProductLineCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId = _currentUser.UserId;

        var entity = await _repo.GetProductLineByIdAsync(subscriberId, command.Id, ct);
        if (entity is null)
            return Result<ProductLineDto>.Failure("Línea no encontrada.");

        if (!entity.IsActive)
            return Result<ProductLineDto>.Failure("La línea ya está deshabilitada.");

        var activeChildren = await _repo.CountActiveCategoriesByLineAsync(subscriberId, command.Id, ct);
        if (activeChildren > 0)
            return Result<ProductLineDto>.Failure("No se puede deshabilitar la línea mientras tenga categorías activas.");

        entity.Disable(userId);
        await _activity.AddAsync(UserActivity.Create(
            subscriberId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "productLine.disable",
            entityType: "ProductLine",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), ct);
        await _repo.SaveChangesAsync(ct);

        return Result<ProductLineDto>.Success(new ProductLineDto(entity.Id, entity.Code, entity.Name, entity.IsActive));
    }
}
