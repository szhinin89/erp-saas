using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Domain.Common;
using ERP.Domain.MasterData.Interfaces;
using ERP.Domain.Modules.Commercial.Entities;
using ERP.Domain.Modules.Commercial.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Products.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Commercial.UseCases.CreateSalesOrder;

public sealed class CreateSalesOrderCommandHandler : IRequestHandler<CreateSalesOrderCommand, Result<SalesOrderDto>>
{
    private readonly ISalesOrderRepository _orderRepo;
    private readonly IBusinessPartnerRepository _bpRepo;
    private readonly IWarehouseRepository _warehouseRepo;
    private readonly IProductRepository _productRepo;
    private readonly ISnowflakeIdGenerator _idGenerator;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CreateSalesOrderCommandHandler> _logger;

    public CreateSalesOrderCommandHandler(
        ISalesOrderRepository orderRepo,
        IBusinessPartnerRepository bpRepo,
        IWarehouseRepository warehouseRepo,
        IProductRepository productRepo,
        ISnowflakeIdGenerator idGenerator,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        ILogger<CreateSalesOrderCommandHandler> logger)
    {
        _orderRepo = orderRepo;
        _bpRepo = bpRepo;
        _warehouseRepo = warehouseRepo;
        _productRepo = productRepo;
        _idGenerator = idGenerator;
        _currentSubscriber = currentSubscriber;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<SalesOrderDto>> Handle(CreateSalesOrderCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var branchId = _currentCompany.CompanyId;
        var userId = _currentUser.UserId;

        if (!_currentCompany.HasCompanyContext || branchId == Guid.Empty)
            return Result<SalesOrderDto>.Failure("Company context is required.");

        var bp = await _bpRepo.GetByIdAsync(command.BusinessPartnerId, ct);
        if (bp is null || !bp.IsActive)
            return Result<SalesOrderDto>.Failure("Business partner not found or inactive.");

        var warehouse = await _warehouseRepo.GetByIdAsync(subscriberId, command.WarehouseId, ct);
        if (warehouse is null || !warehouse.IsActive)
            return Result<SalesOrderDto>.Failure("Warehouse not found or inactive.");

        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        if (command.RequiredDate.HasValue && command.RequiredDate.Value < issueDate)
            return Result<SalesOrderDto>.Failure("RequiredDate must be today or later.");

        var sequential = await _orderRepo.GetNextSequentialAsync(subscriberId, branchId, ct);
        var orderNumber = $"SO-{sequential:D6}";

        var order = SalesOrder.Create(
            _idGenerator.NextId(),
            Guid.NewGuid(),
            subscriberId,
            branchId,
            orderNumber,
            command.BusinessPartnerId,
            command.WarehouseId,
            issueDate,
            command.RequiredDate,
            command.PaymentTermDays,
            command.Notes,
            userId);

        short lineNo = 1;
        foreach (var item in command.Lines)
        {
            var product = await _productRepo.GetByIdAsync(item.ProductId, subscriberId, ct);
            if (product is null || !product.IsActive)
                return Result<SalesOrderDto>.Failure($"Product {item.ProductId} not found or inactive.");

            var detail = SalesOrderDetail.Create(
                _idGenerator.NextId(),
                order.Id,
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

            order.AddLine(detail);
        }

        order.AssignSnowflakeChildIds(_idGenerator);

        await _orderRepo.AddAsync(order, ct);
        await _orderRepo.SaveChangesAsync(ct);

        _logger.LogInformation("Sales order created {OrderNumber} ({PublicId})", order.OrderNumber, order.PublicId);

        return Result<SalesOrderDto>.Success(SalesOrderDtoMapper.ToDto(order));
    }
}
