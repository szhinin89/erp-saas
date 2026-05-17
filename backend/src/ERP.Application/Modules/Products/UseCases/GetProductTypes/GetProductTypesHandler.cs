using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using MediatR;
using ERP.Domain.Products.Interfaces;

namespace ERP.Application.Products.UseCases.GetProductTypes;

public class GetProductTypesHandler : IRequestHandler<GetProductTypesQuery, Result<IReadOnlyList<ProductTypeDto>>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetProductTypesHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public Task<Result<IReadOnlyList<ProductTypeDto>>> HandleAsync(bool onlyActive, CancellationToken ct = default)
        => Handle(new GetProductTypesQuery(onlyActive), ct);

    public async Task<Result<IReadOnlyList<ProductTypeDto>>> Handle(GetProductTypesQuery request, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var items = await _repo.GetProductTypesAsync(tenantId, request.OnlyActive, ct);
        var dtos = items.Select(x => new ProductTypeDto(x.Id, x.Code, x.Name, x.IsActive)).ToList();
        return Result<IReadOnlyList<ProductTypeDto>>.Success(dtos);
    }
}

