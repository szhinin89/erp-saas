using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Items.UseCases.GetLots;

public sealed record GetLotsQuery(
    Guid       ItemId,
    Guid?      WarehouseId = null,
    LotStatus? Status      = null
) : IRequest<Result<IReadOnlyList<LotDto>>>, ICompanyScopedRequest;

public sealed class GetLotsQueryHandler
    : IRequestHandler<GetLotsQuery, Result<IReadOnlyList<LotDto>>>
{
    private readonly ILotRepository _repository;

    public GetLotsQueryHandler(ILotRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<LotDto>>> Handle(GetLotsQuery query, CancellationToken ct)
    {
        var lots = await _repository.GetByItemAsync(query.ItemId, query.WarehouseId, query.Status, ct);

        var dtos = lots.Select(l => new LotDto(
            l.Id, l.LotNumber, l.ItemId, l.VariantId, l.WarehouseId,
            l.ManufactureDate, l.ExpirationDate,
            l.InitialQty, l.CurrentQty, l.Status.ToString(),
            l.Notes, l.CreatedAt)).ToList();

        return Result<IReadOnlyList<LotDto>>.Success(dtos);
    }
}
