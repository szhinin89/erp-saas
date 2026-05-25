using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Domain.Common;
using ERP.Domain.Modules.Commercial.Entities;
using ERP.Domain.Modules.Commercial.Interfaces;
using ERP.Domain.Modules.Integration.Constants;
using ERP.Domain.Modules.Integration.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Commercial.UseCases.CancelQuote;

public sealed class CancelQuoteCommandHandler : IRequestHandler<CancelQuoteCommand, Result<QuoteDto>>
{
    private readonly IQuoteRepository _quoteRepo;
    private readonly IDocumentRelationRepository _relationRepo;
    private readonly ISnowflakeIdGenerator _idGenerator;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CancelQuoteCommandHandler> _logger;

    public CancelQuoteCommandHandler(
        IQuoteRepository quoteRepo,
        IDocumentRelationRepository relationRepo,
        ISnowflakeIdGenerator idGenerator,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        ILogger<CancelQuoteCommandHandler> logger)
    {
        _quoteRepo = quoteRepo;
        _relationRepo = relationRepo;
        _idGenerator = idGenerator;
        _currentSubscriber = currentSubscriber;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<QuoteDto>> Handle(CancelQuoteCommand command, CancellationToken ct)
    {
        var quote = await _quoteRepo.GetByPublicIdAsync(command.PublicId, ct);
        if (quote is null)
            return Result<QuoteDto>.Failure("Quote not found.");

        if (quote.Status is Quote.Statuses.Converted or Quote.Statuses.Cancelled or Quote.Statuses.Rejected)
            return Result<QuoteDto>.Failure($"Cannot cancel quote in status {quote.Status}.");

        var alreadyConverted = await _relationRepo.ExistsSourceRelationAsync(
            _currentSubscriber.SubscriberId,
            DocumentModules.CommercialQuote,
            quote.Id,
            DocumentRelationTypes.QuoteToOrder,
            ct);

        if (alreadyConverted || quote.Status == Quote.Statuses.Converted)
            return Result<QuoteDto>.Failure("Quote was already converted to a sales order.");

        var userId = _currentUser.UserId;
        quote.Cancel(userId, command.Reason);
        _quoteRepo.AssignPendingStatusHistoryIds(_idGenerator);

        await _quoteRepo.SaveChangesAsync(ct);

        _logger.LogInformation("Quote cancelled {QuoteNumber} ({PublicId})", quote.QuoteNumber, quote.PublicId);

        return Result<QuoteDto>.Success(QuoteDtoMapper.ToDto(quote));
    }
}
