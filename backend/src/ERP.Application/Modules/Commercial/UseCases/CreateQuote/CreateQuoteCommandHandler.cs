using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Domain.Common;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Commercial.Entities;
using ERP.Domain.Modules.Commercial.Interfaces;
using ERP.Domain.Products.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Commercial.UseCases.CreateQuote;

public sealed class CreateQuoteCommandHandler : IRequestHandler<CreateQuoteCommand, Result<QuoteDto>>
{
    private readonly IQuoteRepository _quoteRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IProductRepository _productRepo;
    private readonly ISnowflakeIdGenerator _idGenerator;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CreateQuoteCommandHandler> _logger;

    public CreateQuoteCommandHandler(
        IQuoteRepository quoteRepo,
        IBusinessPartnerRepository bpRepo,
        IProductRepository productRepo,
        ISnowflakeIdGenerator idGenerator,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        ILogger<CreateQuoteCommandHandler> logger)
    {
        _quoteRepo = quoteRepo;
        _bpRepo = bpRepo;
        _productRepo = productRepo;
        _idGenerator = idGenerator;
        _currentSubscriber = currentSubscriber;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<QuoteDto>> Handle(CreateQuoteCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var branchId = _currentCompany.CompanyId;
        var userId = _currentUser.UserId;

        if (!_currentCompany.HasCompanyContext || branchId == Guid.Empty)
            return Result<QuoteDto>.Failure("Company context is required.");

        var bp = await _bpRepo.GetByIdAsync(command.BusinessPartnerId, ct);
        if (bp is null || !bp.IsActive)
            return Result<QuoteDto>.Failure("Business partner not found or inactive.");

        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        if (command.ValidUntil < issueDate)
            return Result<QuoteDto>.Failure("ValidUntil must be today or later.");

        var sequential = await _quoteRepo.GetNextSequentialAsync(subscriberId, branchId, ct);
        var quoteNumber = $"QT-{sequential:D6}";

        var quote = Quote.Create(
            _idGenerator.NextId(),
            Guid.NewGuid(),
            subscriberId,
            branchId,
            quoteNumber,
            command.BusinessPartnerId,
            issueDate,
            command.ValidUntil,
            command.PaymentTermDays,
            command.Notes,
            userId);

        short lineNo = 1;
        foreach (var item in command.Lines)
        {
            var product = await _productRepo.GetByIdAsync(item.ProductId, subscriberId, ct);
            if (product is null || !product.IsActive)
                return Result<QuoteDto>.Failure($"Product {item.ProductId} not found or inactive.");

            var detail = QuoteDetail.Create(
                _idGenerator.NextId(),
                quote.Id,
                subscriberId,
                branchId,
                lineNo++,
                item.ProductId,
                product.ShortName,
                product.SaleCode,
                "UND",
                item.Quantity,
                item.UnitPrice,
                item.TaxRatePct,
                issueDate);

            quote.AddLine(detail);
        }

        quote.AssignSnowflakeChildIds(_idGenerator);

        await _quoteRepo.AddAsync(quote, ct);
        await _quoteRepo.SaveChangesAsync(ct);

        _logger.LogInformation("Quote created {QuoteNumber} ({PublicId})", quote.QuoteNumber, quote.PublicId);

        return Result<QuoteDto>.Success(QuoteDtoMapper.ToDto(quote));
    }

    internal static QuoteDto ToDto(Quote quote) => QuoteDtoMapper.ToDto(quote);
}
