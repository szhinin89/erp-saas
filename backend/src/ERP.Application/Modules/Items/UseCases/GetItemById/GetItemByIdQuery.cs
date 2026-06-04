using MediatR;
using ERP.Application.Common;
using ERP.Application.Items.DTOs;
using ERP.Domain.Modules.Items.Interfaces;

namespace ERP.Application.Items.UseCases.GetItemById;

public sealed record GetItemByIdQuery(Guid Id)
    : IRequest<Result<ItemDetailDto>>, ICompanyScopedRequest;

public sealed class GetItemByIdQueryHandler
    : IRequestHandler<GetItemByIdQuery, Result<ItemDetailDto>>
{
    private readonly IItemRepository _repository;
    private readonly ICurrentSubscriber _subscriber;

    public GetItemByIdQueryHandler(IItemRepository repository, ICurrentSubscriber subscriber)
    {
        _repository = repository;
        _subscriber = subscriber;
    }

    public async Task<Result<ItemDetailDto>> Handle(GetItemByIdQuery query, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(query.Id, _subscriber.SubscriberId, ct);
        if (item is null)
            return Result<ItemDetailDto>.NotFound("Ítem no encontrado.");

        return Result<ItemDetailDto>.Success(ItemMappingService.ToDetailDto(item));
    }
}
