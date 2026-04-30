using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.CreateProductType;

public class CreateProductTypeHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public CreateProductTypeHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant, ICurrentUser currentUser)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<ProductTypeDto>> HandleAsync(CreateProductTypeCommand command, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;
        var entity = ProductType.Create(tenantId, command.Code, command.Name, userId);
        await _repo.AddProductTypeAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<ProductTypeDto>.Success(new ProductTypeDto(entity.Id, entity.Code, entity.Name, entity.IsActive));
    }
}

