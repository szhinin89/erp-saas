using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Commercial.Interfaces;
using ERP.Domain.Modules.Integration.Constants;
using ERP.Domain.Modules.Integration.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Commercial.UseCases.GetQuoteByPublicId;

public sealed class GetQuoteByPublicIdQueryHandler
    : IRequestHandler<GetQuoteByPublicIdQuery, Result<QuoteDetailDto?>>
{
    private readonly IQuoteRepository _quoteRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly ISalesOrderRepository _orderRepo;
    private readonly IDocumentRelationRepository _relationRepo;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;

    public GetQuoteByPublicIdQueryHandler(
        IQuoteRepository quoteRepo,
        IBusinessPartnerRepository bpRepo,
        ISalesOrderRepository orderRepo,
        IDocumentRelationRepository relationRepo,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany)
    {
        _quoteRepo = quoteRepo;
        _bpRepo = bpRepo;
        _orderRepo = orderRepo;
        _relationRepo = relationRepo;
        _currentSubscriber = currentSubscriber;
        _currentCompany = currentCompany;
    }

    public async Task<Result<QuoteDetailDto?>> Handle(GetQuoteByPublicIdQuery query, CancellationToken ct)
    {
        if (!_currentCompany.HasCompanyContext)
            return Result<QuoteDetailDto?>.Failure("No hay empresa operativa seleccionada.");

        var subscriberId = _currentSubscriber.SubscriberId;
        var quote = await _quoteRepo.GetByPublicIdReadOnlyAsync(query.PublicId, ct);
        if (quote is null || quote.SubscriberId != subscriberId)
            return Result<QuoteDetailDto?>.Success(null);

        if (quote.BranchId != _currentCompany.CompanyId)
            return Result<QuoteDetailDto?>.Success(null);

        var bp = await _bpRepo.GetByIdAsync(quote.BusinessPartnerId, ct);

        Guid? relatedOrderPublicId = null;
        var orderInternalId = await _relationRepo.GetTargetIdBySourceAsync(
            subscriberId,
            DocumentModules.CommercialQuote,
            quote.Id,
            DocumentRelationTypes.QuoteToOrder,
            ct);
        if (orderInternalId is > 0)
        {
            relatedOrderPublicId = await _orderRepo.GetPublicIdByInternalIdAsync(
                orderInternalId.Value,
                subscriberId,
                ct);
        }

        return Result<QuoteDetailDto?>.Success(
            QuoteDtoMapper.ToDetail(
                quote,
                bp?.Name.LegalName ?? quote.BusinessPartnerId.ToString(),
                relatedOrderPublicId));
    }
}

