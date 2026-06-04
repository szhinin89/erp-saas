using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Items.UseCases.GetSerials;

public sealed record GetSerialsQuery(
    Guid          ItemId,
    Guid?         WarehouseId = null,
    SerialStatus? Status      = null
) : IRequest<Result<IReadOnlyList<SerialDto>>>, ICompanyScopedRequest;

public sealed class GetSerialsQueryHandler
    : IRequestHandler<GetSerialsQuery, Result<IReadOnlyList<SerialDto>>>
{
    private readonly ISerialRepository _repository;

    public GetSerialsQueryHandler(ISerialRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<SerialDto>>> Handle(GetSerialsQuery query, CancellationToken ct)
    {
        var serials = await _repository.GetByItemAsync(query.ItemId, query.WarehouseId, query.Status, ct);

        var dtos = serials.Select(sn => new SerialDto(
            sn.Id, sn.Serial, sn.ItemId, sn.VariantId, sn.WarehouseId,
            sn.LotId, sn.Status.ToString(), sn.AcquiredAt, sn.SoldAt, sn.DocumentRef)).ToList();

        return Result<IReadOnlyList<SerialDto>>.Success(dtos);
    }
}
