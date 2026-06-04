using MediatR;
using ERP.Application.Common;
using ERP.Application.DigitalItems.DTOs;
using ERP.Domain.Modules.DigitalItems.Interfaces;

namespace ERP.Application.DigitalItems.UseCases.GetDigitalItem;

public sealed record GetDigitalItemByItemIdQuery(Guid ItemId)
    : IRequest<Result<DigitalItemDto>>, ICompanyScopedRequest;

public sealed class GetDigitalItemByItemIdQueryHandler
    : IRequestHandler<GetDigitalItemByItemIdQuery, Result<DigitalItemDto>>
{
    private readonly IDigitalItemRepository _repository;

    public GetDigitalItemByItemIdQueryHandler(IDigitalItemRepository repository)
        => _repository = repository;

    public async Task<Result<DigitalItemDto>> Handle(
        GetDigitalItemByItemIdQuery query, CancellationToken ct)
    {
        var di = await _repository.GetByItemIdAsync(query.ItemId, ct);
        if (di is null)
            return Result<DigitalItemDto>.NotFound("Configuración digital no encontrada para este ítem.");

        var dto = new DigitalItemDto(
            di.Id, di.ItemId,
            di.LicenseType.ToString(), di.DeliveryMethod.ToString(),
            di.MaxDownloads, di.ValidityDays, di.IsActive,
            di.Deliverables.Where(d => d.IsActive)
                .Select(d => new DeliverableDto(d.Id, d.Name, d.StorageObjectId, d.Version, d.IsActive))
                .ToList());

        return Result<DigitalItemDto>.Success(dto);
    }
}
