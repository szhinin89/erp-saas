using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;
using MediatR;

namespace ERP.Application.Products.UseCases.UpdateBrand;

public class UpdateBrandHandler : IRequestHandler<UpdateBrandCommand, Result<BrandDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public UpdateBrandHandler(
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

    public async Task<Result<BrandDto>> Handle(UpdateBrandCommand command, CancellationToken ct)
    {
        var entity = await _repo.GetBrandByIdAsync(command.BrandId, ct);
        if (entity is null || entity.TenantId != _currentTenant.TenantId)
            return Result<BrandDto>.Failure("Brand not found.");

        var userId = _currentUser.UserId;
        entity.Update(command.Code, command.Name, userId, command.Manufacturer, command.CountryOfOrigin);

        await _activity.AddAsync(UserActivity.Create(
            _currentTenant.TenantId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "brand.update",
            entityType: "Brand",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), ct);

        await _repo.SaveChangesAsync(ct);
        return Result<BrandDto>.Success(new BrandDto(entity.Id, entity.Code, entity.Name, entity.IsActive, entity.Manufacturer, entity.CountryOfOrigin));
    }
}
