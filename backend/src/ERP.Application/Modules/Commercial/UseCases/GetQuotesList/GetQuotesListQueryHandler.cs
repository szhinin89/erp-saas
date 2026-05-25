using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Commercial.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Commercial.UseCases.GetQuotesList;

public sealed class GetQuotesListQueryHandler
    : IRequestHandler<GetQuotesListQuery, Result<QuotesPagedResult>>
{
    private readonly IQuoteRepository _quoteRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;

    public GetQuotesListQueryHandler(
        IQuoteRepository quoteRepo,
        IBusinessPartnerRepository bpRepo,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany)
    {
        _quoteRepo = quoteRepo;
        _bpRepo = bpRepo;
        _currentSubscriber = currentSubscriber;
        _currentCompany = currentCompany;
    }

    public async Task<Result<QuotesPagedResult>> Handle(GetQuotesListQuery query, CancellationToken ct)
    {
        if (!_currentCompany.HasCompanyContext)
            return Result<QuotesPagedResult>.Failure("No hay empresa operativa seleccionada.");

        var subscriberId = _currentSubscriber.SubscriberId;
        var branchId = _currentCompany.CompanyId;

        var (items, total) = await _quoteRepo.GetPagedAsync(
            subscriberId,
            branchId,
            query.PageNumber,
            query.PageSize,
            query.BusinessPartnerId,
            query.Status,
            query.DateFrom,
            query.DateTo,
            ct);

        var partnerIds = items.Select(q => q.BusinessPartnerId).Distinct().ToList();
        var partnerNames = new Dictionary<Guid, string>();
        foreach (var partnerId in partnerIds)
        {
            var bp = await _bpRepo.GetByIdAsync(partnerId, ct);
            partnerNames[partnerId] = bp?.LegalName ?? partnerId.ToString();
        }

        var dtos = items
            .Select(q => QuoteDtoMapper.ToListItem(
                q,
                partnerNames.GetValueOrDefault(q.BusinessPartnerId, q.BusinessPartnerId.ToString())))
            .ToList();

        return Result<QuotesPagedResult>.Success(
            new QuotesPagedResult(dtos, total, query.PageNumber, query.PageSize));
    }
}
