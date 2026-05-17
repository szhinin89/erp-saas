using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;
using MediatR;

namespace ERP.Application.Products.UseCases.DisableProductType;

public class DisableProductTypeHandler : IRequestHandler<DisableProductTypeCommand, Result<ProductTypeDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public DisableProductTypeHandler(
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

    public async Task<Result<ProductTypeDto>> Handle(DisableProductTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repo.GetProductTypeByIdAsync(request.ProductTypeId, cancellationToken);
        if (entity is null)
            return Result<ProductTypeDto>.Failure("Product type not found.");

        entity.Disable(_currentUser.UserId);

        await _activity.AddAsync(UserActivity.Create(
            _currentTenant.TenantId,
            _currentUser.UserId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "productType.disable",
            entityType: "ProductType",
            entityId: entity.Id,
            description: entity.Code), cancellationToken);

        await _repo.SaveChangesAsync(cancellationToken);
        return Result<ProductTypeDto>.Success(new ProductTypeDto(entity.Id, entity.Code, entity.Name, entity.IsActive));
    }
}

public class EnableProductTypeHandler : IRequestHandler<EnableProductTypeCommand, Result<ProductTypeDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public EnableProductTypeHandler(
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

    public async Task<Result<ProductTypeDto>> Handle(EnableProductTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repo.GetProductTypeByIdAsync(request.ProductTypeId, cancellationToken);
        if (entity is null)
            return Result<ProductTypeDto>.Failure("Product type not found.");

        entity.Enable(_currentUser.UserId);

        await _activity.AddAsync(UserActivity.Create(
            _currentTenant.TenantId,
            _currentUser.UserId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "productType.enable",
            entityType: "ProductType",
            entityId: entity.Id,
            description: entity.Code), cancellationToken);

        await _repo.SaveChangesAsync(cancellationToken);
        return Result<ProductTypeDto>.Success(new ProductTypeDto(entity.Id, entity.Code, entity.Name, entity.IsActive));
    }
}
