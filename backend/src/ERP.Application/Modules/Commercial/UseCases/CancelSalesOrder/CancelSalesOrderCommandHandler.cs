using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Domain.Common;
using ERP.Domain.Modules.Commercial.Entities;
using ERP.Domain.Modules.Commercial.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Commercial.UseCases.CancelSalesOrder;

public sealed class CancelSalesOrderCommandHandler : IRequestHandler<CancelSalesOrderCommand, Result<SalesOrderDto>>
{
    private readonly ISalesOrderRepository _orderRepo;
    private readonly ISnowflakeIdGenerator _idGenerator;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<CancelSalesOrderCommandHandler> _logger;

    public CancelSalesOrderCommandHandler(
        ISalesOrderRepository orderRepo,
        ISnowflakeIdGenerator idGenerator,
        ICurrentUser currentUser,
        ILogger<CancelSalesOrderCommandHandler> logger)
    {
        _orderRepo = orderRepo;
        _idGenerator = idGenerator;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<SalesOrderDto>> Handle(CancelSalesOrderCommand command, CancellationToken ct)
    {
        var order = await _orderRepo.GetByPublicIdAsync(command.PublicId, ct);
        if (order is null)
            return Result<SalesOrderDto>.Failure("Sales order not found.");

        if (order.Status is SalesOrder.Statuses.Invoiced or SalesOrder.Statuses.PartiallyInvoiced)
            return Result<SalesOrderDto>.Failure($"Cannot cancel order in status {order.Status}.");

        if (order.Status == SalesOrder.Statuses.Cancelled)
            return Result<SalesOrderDto>.Failure("Sales order is already cancelled.");

        var userId = _currentUser.UserId;
        order.Cancel(userId, command.Reason);
        _orderRepo.AssignPendingStatusHistoryIds(_idGenerator);

        await _orderRepo.SaveChangesAsync(ct);

        _logger.LogInformation("Sales order cancelled {OrderNumber} ({PublicId})", order.OrderNumber, order.PublicId);

        return Result<SalesOrderDto>.Success(SalesOrderDtoMapper.ToDto(order));
    }
}
