using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Fiscal.Mapping;
using ERP.Application.Sales.DTOs;
using ERP.Domain.Modules.Fiscal.Interfaces;

namespace ERP.Application.Sales.UseCases.GetVentasList;

public sealed class GetSalesListQueryHandler
    : IRequestHandler<GetSalesListQuery, Result<SalesPagedResult>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetSalesListQueryHandler(IInvoiceRepository invoiceRepository, ICurrentSubscriber currentSubscriber)
    {
        _invoiceRepository = invoiceRepository;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<SalesPagedResult>> Handle(GetSalesListQuery query, CancellationToken ct)
    {
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) = await _invoiceRepository.GetPagedAsync(
            _currentSubscriber.SubscriberId,
            pageNumber,
            pageSize,
            query.BusinessPartnerId,
            query.DateFrom,
            query.DateTo,
            query.Status,
            query.Search,
            ct);

        var dtos = items.Select(i => InvoiceDtoMapper.ToListDto(i)).ToList();
        return Result<SalesPagedResult>.Success(new SalesPagedResult(dtos, totalCount, pageNumber, pageSize));
    }
}
