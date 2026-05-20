using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.CreateUnitOfMeasure;

public class CreateUnitOfMeasureHandler : IRequestHandler<CreateUnitOfMeasureCommand, Result<UnitOfMeasureDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public CreateUnitOfMeasureHandler(
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

    public Task<Result<UnitOfMeasureDto>> HandleAsync(CreateUnitOfMeasureCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<UnitOfMeasureDto>> Handle(CreateUnitOfMeasureCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId = _currentUser.UserId;
        var entity = UnitOfMeasure.Create(subscriberId, command.Code, command.Name, userId, command.Symbol);
        await _repo.AddUnitOfMeasureAsync(entity, ct);
        await _activity.AddAsync(UserActivity.Create(
            subscriberId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "unitOfMeasure.create",
            entityType: "UnitOfMeasure",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), ct);
        await _repo.SaveChangesAsync(ct);
        return Result<UnitOfMeasureDto>.Success(new UnitOfMeasureDto(entity.Id, entity.Code, entity.Name, entity.Symbol, entity.IsActive));
    }
}

