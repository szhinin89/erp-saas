using ERP.Application.Common;
using ERP.Application.Products.DTOs;
using ERP.Domain.Products.Interfaces;

using MediatR;

namespace ERP.Application.Products.UseCases.GetProductLines;

public class GetProductLinesHandler : IRequestHandler<GetProductLinesQuery, Result<IReadOnlyList<ProductLineDto>>>
{
    private readonly IProductCatalogRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetProductLinesHandler(IProductCatalogRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<IReadOnlyList<ProductLineDto>>> Handle(
        GetProductLinesQuery query,
        CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var items = await _repo.GetProductLinesAsync(tenantId, query.ActiveFilter, query.Search, ct);
        var dtos = items.Select(x => new ProductLineDto(x.Id, x.Code, x.Name, x.IsActive)).ToList();
        return Result<IReadOnlyList<ProductLineDto>>.Success(dtos);
    }
}

