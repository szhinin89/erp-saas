using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.CreateBrand;

public class CreateBrandHandler
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public CreateBrandHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant, ICurrentUser currentUser)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
    }

    public async Task<Result<BrandDto>> HandleAsync(CreateBrandCommand command, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var userId = _currentUser.UserId;
        var entity = Brand.Create(tenantId, command.Code, command.Name, userId);
        await _repo.AddBrandAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);
        return Result<BrandDto>.Success(new BrandDto(entity.Id, entity.Code, entity.Name, entity.IsActive));
    }
}

