using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;
using static ERP.Application.Modules.Inventory.Stock.UseCases.GetStockMovements.GetStockMovementsQueryHandler;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetKardexByProduct;

public sealed class GetKardexByProductQueryHandler
    : IRequestHandler<GetKardexByProductQuery, Result<IReadOnlyList<StockMovementDto>>>
{
    private readonly IStockRepository _repo;
    private readonly IAccessRepository _accessRepo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICompanyClock _companyClock;

    public GetKardexByProductQueryHandler(
        IStockRepository repo,
        IAccessRepository accessRepo,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICompanyClock companyClock
    )
    {
        _repo = repo;
        _accessRepo = accessRepo;
        _tenant = tenant;
        _company = company;
        _companyClock = companyClock;
    }

    public async Task<Result<IReadOnlyList<StockMovementDto>>> Handle(
        GetKardexByProductQuery request,
        CancellationToken ct
    )
    {
        var (fromUtc, toUtcExclusive) = await _companyClock.DaysUtcRangeAsync(
            _company.CompanyId,
            _tenant.TenantId,
            request.From,
            request.To,
            ct
        );
        var movements = await _repo.GetMovementsByProductAsync(
            _tenant.TenantId,
            request.ProductId,
            request.WarehouseId,
            fromUtc,
            toUtcExclusive,
            ct
        );

        var userNames = await ResolveActorNamesAsync(_accessRepo, movements, ct);
        var dtos = movements
            .Select(m => ToDto(m) with { CreatedByName = userNames.GetValueOrDefault(m.CreatedBy) })
            .ToList();
        return Result<IReadOnlyList<StockMovementDto>>.Success(dtos);
    }
}
