using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;
using MediatR;

namespace ERP.Application.Products.UseCases.DisableUnitOfMeasure;

public class DisableUnitOfMeasureHandler : IRequestHandler<DisableUnitOfMeasureCommand, Result<UnitOfMeasureDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public DisableUnitOfMeasureHandler(
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

    public async Task<Result<UnitOfMeasureDto>> Handle(DisableUnitOfMeasureCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repo.GetUnitOfMeasureByIdAsync(request.UnitOfMeasureId, cancellationToken);
        if (entity is null)
            return Result<UnitOfMeasureDto>.Failure("Unit of measure not found.");

        entity.Disable(_currentUser.UserId);

        await _activity.AddAsync(UserActivity.Create(
            _currentSubscriber.SubscriberId,
            _currentUser.UserId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "unit.disable",
            entityType: "UnitOfMeasure",
            entityId: entity.Id,
            description: entity.Code), cancellationToken);

        await _repo.SaveChangesAsync(cancellationToken);
        return Result<UnitOfMeasureDto>.Success(
            new UnitOfMeasureDto(entity.Id, entity.Code, entity.Name, entity.Symbol, entity.IsActive));
    }
}

public class EnableUnitOfMeasureHandler : IRequestHandler<EnableUnitOfMeasureCommand, Result<UnitOfMeasureDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public EnableUnitOfMeasureHandler(
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

    public async Task<Result<UnitOfMeasureDto>> Handle(EnableUnitOfMeasureCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repo.GetUnitOfMeasureByIdAsync(request.UnitOfMeasureId, cancellationToken);
        if (entity is null)
            return Result<UnitOfMeasureDto>.Failure("Unit of measure not found.");

        entity.Enable(_currentUser.UserId);

        await _activity.AddAsync(UserActivity.Create(
            _currentSubscriber.SubscriberId,
            _currentUser.UserId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "unit.enable",
            entityType: "UnitOfMeasure",
            entityId: entity.Id,
            description: entity.Code), cancellationToken);

        await _repo.SaveChangesAsync(cancellationToken);
        return Result<UnitOfMeasureDto>.Success(
            new UnitOfMeasureDto(entity.Id, entity.Code, entity.Name, entity.Symbol, entity.IsActive));
    }
}
