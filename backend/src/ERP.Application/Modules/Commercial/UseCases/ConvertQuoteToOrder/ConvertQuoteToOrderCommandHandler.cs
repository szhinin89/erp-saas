using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Domain.Common;
using ERP.Domain.Modules.Commercial.Entities;
using ERP.Domain.Modules.Commercial.Interfaces;
using ERP.Domain.Modules.Integration.Constants;
using ERP.Domain.Modules.Integration.Entities;
using ERP.Domain.Modules.Integration.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Commercial.UseCases.ConvertQuoteToOrder;

public sealed class ConvertQuoteToOrderCommandHandler : IRequestHandler<ConvertQuoteToOrderCommand, Result<SalesOrderDto>>
{
    private readonly IQuoteRepository _quoteRepo;
    private readonly ISalesOrderRepository _orderRepo;
    private readonly IDocumentRelationRepository _relationRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly ISnowflakeIdGenerator _idGenerator;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConvertQuoteToOrderCommandHandler> _logger;

    public ConvertQuoteToOrderCommandHandler(
        IQuoteRepository quoteRepo,
        ISalesOrderRepository orderRepo,
        IDocumentRelationRepository relationRepo,
        IWarehouseRepository warehouseRepo,
        ISnowflakeIdGenerator idGenerator,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        ILogger<ConvertQuoteToOrderCommandHandler> logger)
    {
        _quoteRepo = quoteRepo;
        _orderRepo = orderRepo;
        _relationRepo = relationRepo;
        _warehouseRepo = warehouseRepo;
        _idGenerator = idGenerator;
        _currentSubscriber = currentSubscriber;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<SalesOrderDto>> Handle(ConvertQuoteToOrderCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var branchId = _currentCompany.CompanyId;
        var userId = _currentUser.UserId;

        if (!_currentCompany.HasCompanyContext || branchId == Guid.Empty)
            return Result<SalesOrderDto>.Failure("Company context is required.");

        var quote = await _quoteRepo.GetByPublicIdAsync(command.QuotePublicId, ct);
        if (quote is null)
            return Result<SalesOrderDto>.Failure("Quote not found.");

        if (quote.Status != Quote.Statuses.Approved)
            return Result<SalesOrderDto>.Failure($"Quote must be approved before conversion (current: {quote.Status}).");

        if (DateOnly.FromDateTime(DateTime.UtcNow) > quote.ValidUntil)
            return Result<SalesOrderDto>.Failure("Quote has expired.");

        var alreadyConverted = await _relationRepo.ExistsSourceRelationAsync(
            subscriberId,
            DocumentModules.CommercialQuote,
            quote.Id,
            DocumentRelationTypes.QuoteToOrder,
            ct);

        if (alreadyConverted)
            return Result<SalesOrderDto>.Failure("Quote was already converted to a sales order.");

        var warehouse = await _warehouseRepo.GetByIdAsync(subscriberId, command.WarehouseId, ct);
        if (warehouse is null || !warehouse.IsActive)
            return Result<SalesOrderDto>.Failure("Warehouse not found or inactive.");

        var requiredDate = command.RequiredDate ?? quote.ValidUntil;
        if (requiredDate < quote.IssueDate)
            return Result<SalesOrderDto>.Failure("RequiredDate must be on or after quote issue date.");

        var sequential = await _orderRepo.GetNextSequentialAsync(subscriberId, branchId, ct);
        var orderNumber = $"SO-{sequential:D6}";

        var order = SalesOrder.Create(
            _idGenerator.NextId(),
            Guid.NewGuid(),
            subscriberId,
            branchId,
            orderNumber,
            quote.BusinessPartnerId,
            command.WarehouseId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            requiredDate,
            quote.PaymentTermDays,
            quote.Notes,
            userId);

        foreach (var line in quote.Lines.OrderBy(l => l.LineNo))
        {
            var detail = SalesOrderDetail.Create(
                _idGenerator.NextId(),
                order.Id,
                subscriberId,
                branchId,
                line.LineNo,
                line.ProductId,
                line.ProductNameSnapshot,
                line.SkuSnapshot,
                line.UnitNameSnapshot,
                line.Quantity,
                line.UnitPrice,
                line.TaxRateSnapshot,
                order.IssueDate);

            order.AddLine(detail);
        }

        order.AssignSnowflakeChildIds(_idGenerator);

        var relation = DocumentRelation.Create(
            _idGenerator.NextId(),
            Guid.NewGuid(),
            subscriberId,
            branchId,
            DocumentModules.CommercialQuote,
            quote.Id,
            DocumentModules.CommercialSalesOrder,
            order.Id,
            DocumentRelationTypes.QuoteToOrder,
            userId);

        quote.MarkConverted(userId);
        _quoteRepo.AssignPendingStatusHistoryIds(_idGenerator);

        await _orderRepo.AddAsync(order, ct);
        await _relationRepo.AddAsync(relation, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Quote {QuoteNumber} converted to order {OrderNumber}",
            quote.QuoteNumber,
            order.OrderNumber);

        return Result<SalesOrderDto>.Success(SalesOrderDtoMapper.ToDto(order));
    }
}
