using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Commercial.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Commercial.UseCases.GetSalesOrdersList;

public sealed class GetSalesOrdersListQueryHandler
    : IRequestHandler<GetSalesOrdersListQuery, Result<SalesOrdersPagedResult>>
{
    private readonly ISalesOrderRepository _orderRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;

    public GetSalesOrdersListQueryHandler(
        ISalesOrderRepository orderRepo,
        IBusinessPartnerRepository bpRepo,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany)
    {
        _orderRepo = orderRepo;
        _bpRepo = bpRepo;
        _currentSubscriber = currentSubscriber;
        _currentCompany = currentCompany;
    }

    public async Task<Result<SalesOrdersPagedResult>> Handle(
        GetSalesOrdersListQuery query,
        CancellationToken ct)
    {
        if (!_currentCompany.HasCompanyContext)
            return Result<SalesOrdersPagedResult>.Failure("No hay empresa operativa seleccionada.");

        var subscriberId = _currentSubscriber.SubscriberId;
        var branchId = _currentCompany.CompanyId;

        var (items, total) = await _orderRepo.GetPagedAsync(
            subscriberId,
            branchId,
            query.PageNumber,
            query.PageSize,
            query.BusinessPartnerId,
            query.Status,
            query.DateFrom,
            query.DateTo,
            ct);

        var partnerIds = items.Select(o => o.BusinessPartnerId).Distinct().ToList();
        var partnerNames = new Dictionary<Guid, string>();
        foreach (var partnerId in partnerIds)
        {
            var bp = await _bpRepo.GetByIdAsync(partnerId, ct);
            partnerNames[partnerId] = bp?.Name.LegalName ?? partnerId.ToString();
        }

        var dtos = items
            .Select(o => SalesOrderDtoMapper.ToListItem(
                o,
                partnerNames.GetValueOrDefault(o.BusinessPartnerId, o.BusinessPartnerId.ToString())))
            .ToList();

        return Result<SalesOrdersPagedResult>.Success(
            new SalesOrdersPagedResult(dtos, total, query.PageNumber, query.PageSize));
    }
}

