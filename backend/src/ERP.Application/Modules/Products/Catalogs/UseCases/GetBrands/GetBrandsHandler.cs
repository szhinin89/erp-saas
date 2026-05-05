using ERP.Application.Common;
using ERP.Application.Products.Catalogs.DTOs;
using MediatR;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.Catalogs.UseCases.GetBrands;

public class GetBrandsHandler : IRequestHandler<GetBrandsQuery, Result<IReadOnlyList<BrandDto>>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetBrandsHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public Task<Result<IReadOnlyList<BrandDto>>> HandleAsync(bool onlyActive, CancellationToken ct = default)
        => Handle(new GetBrandsQuery(onlyActive), ct);

    public async Task<Result<IReadOnlyList<BrandDto>>> Handle(GetBrandsQuery request, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var items = await _repo.GetBrandsAsync(tenantId, request.OnlyActive, ct);
        var dtos = items.Select(x => new BrandDto(x.Id, x.Code, x.Name, x.IsActive)).ToList();
        return Result<IReadOnlyList<BrandDto>>.Success(dtos);
    }
}

