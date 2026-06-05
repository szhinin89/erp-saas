using MediatR;
using ERP.Application.Common;
using ERP.Application.Sales.DTOs;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Sales.UseCases.ReceivedRetentions;

public record GetSalesRetentionsReceivedListQuery : IRequest<Result<IReadOnlyList<SalesRetentionListItemDto>>>, ICompanyScopedRequest;


public sealed class GetSalesRetentionsReceivedListQueryHandler
    : IRequestHandler<GetSalesRetentionsReceivedListQuery, Result<IReadOnlyList<SalesRetentionListItemDto>>>
{
    private readonly ISalesRepository _ventasRepository;
    private readonly ICurrentSubscriber  _currentSubscriber;

    public GetSalesRetentionsReceivedListQueryHandler(
        ISalesRepository ventasRepository,
        ICurrentSubscriber currentSubscriber)
    {
        _ventasRepository = ventasRepository;
        _currentSubscriber    = currentSubscriber;
    }

    public async Task<Result<IReadOnlyList<SalesRetentionListItemDto>>> Handle(
        GetSalesRetentionsReceivedListQuery request,
        CancellationToken ct)
    {
        var list = await _ventasRepository.GetRetentionsAsync(_currentSubscriber.SubscriberId, ct);
        var dto = list.Select(r => new SalesRetentionListItemDto(
            r.Id, r.BusinessPartnerId, r.AccessKey, r.IssueDate, r.TotalRetained, r.SalesBillId)).ToList();
        return Result<IReadOnlyList<SalesRetentionListItemDto>>.Success(dto);
    }
}
