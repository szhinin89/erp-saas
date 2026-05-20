using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;
using MediatR;

namespace ERP.Application.Products.UseCases.UpdateBrand;

public class DisableBrandHandler : IRequestHandler<DisableBrandCommand, Result<BrandDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public DisableBrandHandler(
        IProductCatalogRepository repo, IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber, ICurrentUser currentUser)
    {
        _repo = repo; _activity = activity;
        _currentSubscriber = currentSubscriber; _currentUser = currentUser;
    }

    public async Task<Result<BrandDto>> Handle(DisableBrandCommand command, CancellationToken ct)
    {
        var entity = await _repo.GetBrandByIdAsync(command.BrandId, ct);
        if (entity is null || entity.SubscriberId != _currentSubscriber.SubscriberId)
            return Result<BrandDto>.Failure("Brand not found.");

        var userId = _currentUser.UserId;
        entity.Disable(userId);
        await _activity.AddAsync(UserActivity.Create(
            _currentSubscriber.SubscriberId, userId, _currentUser.Email, _currentUser.FullName,
            "inventario", "brand.disable", "Brand", entity.Id, $"{entity.Code} — {entity.Name}"), ct);
        await _repo.SaveChangesAsync(ct);
        return Result<BrandDto>.Success(new BrandDto(entity.Id, entity.Code, entity.Name, entity.IsActive, entity.Manufacturer, entity.CountryOfOrigin));
    }
}

public class EnableBrandHandler : IRequestHandler<EnableBrandCommand, Result<BrandDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public EnableBrandHandler(
        IProductCatalogRepository repo, IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber, ICurrentUser currentUser)
    {
        _repo = repo; _activity = activity;
        _currentSubscriber = currentSubscriber; _currentUser = currentUser;
    }

    public async Task<Result<BrandDto>> Handle(EnableBrandCommand command, CancellationToken ct)
    {
        var entity = await _repo.GetBrandByIdAsync(command.BrandId, ct);
        if (entity is null || entity.SubscriberId != _currentSubscriber.SubscriberId)
            return Result<BrandDto>.Failure("Brand not found.");

        var userId = _currentUser.UserId;
        entity.Enable(userId);
        await _activity.AddAsync(UserActivity.Create(
            _currentSubscriber.SubscriberId, userId, _currentUser.Email, _currentUser.FullName,
            "inventario", "brand.enable", "Brand", entity.Id, $"{entity.Code} — {entity.Name}"), ct);
        await _repo.SaveChangesAsync(ct);
        return Result<BrandDto>.Success(new BrandDto(entity.Id, entity.Code, entity.Name, entity.IsActive, entity.Manufacturer, entity.CountryOfOrigin));
    }
}
