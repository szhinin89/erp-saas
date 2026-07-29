using ERP.Application.Common;
using ERP.Application.Modules.Inventory.Stock.DTOs;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;
using static ERP.Application.Modules.Inventory.Stock.UseCases.GetStockMovements.GetStockMovementsQueryHandler;

namespace ERP.Application.Modules.Inventory.Stock.UseCases.GetKardexByDocument;

public sealed class GetKardexByDocumentQueryHandler
    : IRequestHandler<GetKardexByDocumentQuery, Result<IReadOnlyList<StockMovementDto>>>
{
    private readonly IStockRepository _repo;
    private readonly IAccessRepository _accessRepo;
    private readonly ICurrentTenant _tenant;

    public GetKardexByDocumentQueryHandler(IStockRepository repo, IAccessRepository accessRepo, ICurrentTenant tenant)
    {
        _repo = repo;
        _accessRepo = accessRepo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<StockMovementDto>>> Handle(
        GetKardexByDocumentQuery request, CancellationToken ct)
    {
        var movements = await _repo.GetMovementsByDocumentAsync(
            _tenant.TenantId, request.SourceDocId, request.SourceDocType, ct);

        var userNames = await ResolveActorNamesAsync(_accessRepo, movements, ct);
        var dtos = movements.Select(m => ToDto(m) with { CreatedByName = userNames.GetValueOrDefault(m.CreatedBy) }).ToList();
        return Result<IReadOnlyList<StockMovementDto>>.Success(dtos);
    }
}
