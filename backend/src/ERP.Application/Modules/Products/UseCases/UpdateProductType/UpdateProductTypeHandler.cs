using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Products.Interfaces;
using MediatR;

namespace ERP.Application.Products.UseCases.UpdateProductType;

public class UpdateProductTypeHandler : IRequestHandler<UpdateProductTypeCommand, Result<ProductTypeDto>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public UpdateProductTypeHandler(
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

    public async Task<Result<ProductTypeDto>> Handle(UpdateProductTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repo.GetProductTypeByIdAsync(request.ProductTypeId, cancellationToken);
        if (entity is null)
            return Result<ProductTypeDto>.Failure("Product type not found.");

        entity.Update(request.Code.Trim(), request.Name.Trim(), _currentUser.UserId);

        await _activity.AddAsync(UserActivity.Create(
            _currentTenant.TenantId,
            _currentUser.UserId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "inventario",
            action: "productType.update",
            entityType: "ProductType",
            entityId: entity.Id,
            description: $"{entity.Code} — {entity.Name}"), cancellationToken);

        await _repo.SaveChangesAsync(cancellationToken);
        return Result<ProductTypeDto>.Success(new ProductTypeDto(entity.Id, entity.Code, entity.Name, entity.IsActive));
    }
}
