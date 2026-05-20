using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Purchasing.DTOs;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Application.Modules.Purchasing.UseCases.Retenciones;

public sealed record GetPurchaseIssuedRetentionsListQuery(Guid? SupplierId)
    : IRequest<Result<IReadOnlyList<IssuedRetentionListItemDto>>>;

public sealed class GetPurchaseIssuedRetentionsListQueryHandler
    : IRequestHandler<GetPurchaseIssuedRetentionsListQuery, Result<IReadOnlyList<IssuedRetentionListItemDto>>>
{
    private readonly IPurchBillRepository _compraRepository;
    private readonly ICurrentSubscriber  _currentSubscriber;

    public GetPurchaseIssuedRetentionsListQueryHandler(
        IPurchBillRepository compraRepository,
        ICurrentSubscriber currentSubscriber)
    {
        _compraRepository = compraRepository;
        _currentSubscriber    = currentSubscriber;
    }

    public async Task<Result<IReadOnlyList<IssuedRetentionListItemDto>>> Handle(
        GetPurchaseIssuedRetentionsListQuery request,
        CancellationToken ct)
    {
        var items = await _compraRepository.GetIssuedRetentionsAsync(
            _currentSubscriber.SubscriberId, request.SupplierId, ct);
        var dto = items.Select(r => new IssuedRetentionListItemDto(
            r.Id, r.SupplierId, r.AccessKey, r.Status, r.TotalRetained, r.IssueDate)).ToList();
        return Result<IReadOnlyList<IssuedRetentionListItemDto>>.Success(dto);
    }
}
