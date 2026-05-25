using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Domain.Common;
using ERP.Domain.Modules.Commercial.Entities;
using ERP.Domain.Modules.Commercial.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Commercial.UseCases.ApproveQuote;

public sealed class ApproveQuoteCommandHandler : IRequestHandler<ApproveQuoteCommand, Result<QuoteDto>>
{
    private readonly IQuoteRepository _quoteRepo;
    private readonly ISnowflakeIdGenerator _idGenerator;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ApproveQuoteCommandHandler> _logger;

    public ApproveQuoteCommandHandler(
        IQuoteRepository quoteRepo,
        ISnowflakeIdGenerator idGenerator,
        ICurrentUser currentUser,
        ILogger<ApproveQuoteCommandHandler> logger)
    {
        _quoteRepo = quoteRepo;
        _idGenerator = idGenerator;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<QuoteDto>> Handle(ApproveQuoteCommand command, CancellationToken ct)
    {
        var quote = await _quoteRepo.GetByPublicIdAsync(command.PublicId, ct);
        if (quote is null)
            return Result<QuoteDto>.Failure("Quote not found.");

        if (quote.Status is not Quote.Statuses.Draft and not Quote.Statuses.Sent)
            return Result<QuoteDto>.Failure($"Only draft or sent quotes can be approved (current: {quote.Status}).");

        if (quote.Lines.Count == 0)
            return Result<QuoteDto>.Failure("Cannot approve a quote without lines.");

        if (DateOnly.FromDateTime(DateTime.UtcNow) > quote.ValidUntil)
            return Result<QuoteDto>.Failure("Quote has expired.");

        var userId = _currentUser.UserId;
        quote.Approve(userId);
        _quoteRepo.AssignPendingStatusHistoryIds(_idGenerator);

        await _quoteRepo.SaveChangesAsync(ct);

        _logger.LogInformation("Quote approved {QuoteNumber} ({PublicId})", quote.QuoteNumber, quote.PublicId);

        return Result<QuoteDto>.Success(QuoteDtoMapper.ToDto(quote));
    }
}
