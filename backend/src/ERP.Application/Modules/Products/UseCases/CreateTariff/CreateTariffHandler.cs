using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.CreateTariff;

public class CreateTariffHandler : IRequestHandler<CreateTariffCommand, Result<TariffDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public CreateTariffHandler(
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

    public Task<Result<TariffDto>> HandleAsync(CreateTariffCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<TariffDto>> Handle(CreateTariffCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId = _currentUser.UserId;
        var entity = Tariff.Create(subscriberId, command.Code, command.Description, userId);
        await _repo.AddTariffAsync(entity, ct);
        await _activity.AddAsync(UserActivity.Create(
            subscriberId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "tariff.create",
            entityType: "Tariff",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Description}"), ct);
        await _repo.SaveChangesAsync(ct);
        return Result<TariffDto>.Success(new TariffDto(entity.Id, entity.Code, entity.Description, entity.IsActive));
    }
}

