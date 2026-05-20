using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.CreateBrand;

public class CreateBrandHandler : IRequestHandler<CreateBrandCommand, Result<BrandDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public CreateBrandHandler(
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

    public Task<Result<BrandDto>> HandleAsync(CreateBrandCommand command, CancellationToken ct = default)
        => Handle(command, ct);

    public async Task<Result<BrandDto>> Handle(CreateBrandCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId = _currentUser.UserId;
        var entity = Brand.Create(subscriberId, command.Code, command.Name, userId, command.Manufacturer, command.CountryOfOrigin);
        await _repo.AddBrandAsync(entity, ct);
        await _activity.AddAsync(UserActivity.Create(
            subscriberId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "brand.create",
            entityType: "Brand",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), ct);
        await _repo.SaveChangesAsync(ct);
        return Result<BrandDto>.Success(new BrandDto(entity.Id, entity.Code, entity.Name, entity.IsActive, entity.Manufacturer, entity.CountryOfOrigin));
    }
}

