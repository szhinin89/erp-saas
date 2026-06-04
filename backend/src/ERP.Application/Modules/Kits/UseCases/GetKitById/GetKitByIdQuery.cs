using MediatR;
using ERP.Application.Common;
using ERP.Application.Kits.DTOs;
using ERP.Domain.Modules.Kits.Interfaces;

namespace ERP.Application.Kits.UseCases.GetKitById;

public sealed record GetKitByItemIdQuery(Guid ItemId)
    : IRequest<Result<KitDto>>, ICompanyScopedRequest;

public sealed class GetKitByItemIdQueryHandler
    : IRequestHandler<GetKitByItemIdQuery, Result<KitDto>>
{
    private readonly IKitRepository _repository;

    public GetKitByItemIdQueryHandler(IKitRepository repository) => _repository = repository;

    public async Task<Result<KitDto>> Handle(GetKitByItemIdQuery query, CancellationToken ct)
    {
        var kit = await _repository.GetByItemIdAsync(query.ItemId, ct);
        if (kit is null)
            return Result<KitDto>.NotFound("Kit no encontrado para este ítem.");

        var dto = new KitDto(
            kit.Id, kit.ItemId, kit.KitType.ToString(), kit.IsActive,
            kit.Lines.Select(l => new KitLineDto(
                l.Id, l.ComponentItemId, l.ComponentVariantId,
                l.Quantity, l.UomCode, l.IsOptional, l.SortOrder, l.IsActive)).ToList());

        return Result<KitDto>.Success(dto);
    }
}
