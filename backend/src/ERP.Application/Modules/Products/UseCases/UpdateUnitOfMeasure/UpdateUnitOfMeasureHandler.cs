using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;
using MediatR;

namespace ERP.Application.Products.UseCases.UpdateUnitOfMeasure;

public class UpdateUnitOfMeasureHandler : IRequestHandler<UpdateUnitOfMeasureCommand, Result<UnitOfMeasureDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public UpdateUnitOfMeasureHandler(
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

    public async Task<Result<UnitOfMeasureDto>> Handle(UpdateUnitOfMeasureCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repo.GetUnitOfMeasureByIdAsync(request.UnitOfMeasureId, cancellationToken);
        if (entity is null)
            return Result<UnitOfMeasureDto>.Failure("Unit of measure not found.");

        entity.Update(
            request.Code.Trim(),
            request.Name.Trim(),
            request.Symbol?.Trim() ?? null,
            _currentUser.UserId);

        await _activity.AddAsync(UserActivity.Create(
            _currentSubscriber.SubscriberId,
            _currentUser.UserId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "unit.update",
            entityType: "UnitOfMeasure",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), cancellationToken);

        await _repo.SaveChangesAsync(cancellationToken);
        return Result<UnitOfMeasureDto>.Success(
            new UnitOfMeasureDto(entity.Id, entity.Code, entity.Name, entity.Symbol, entity.IsActive));
    }
}
