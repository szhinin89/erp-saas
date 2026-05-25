using ERP.Application.Common;
using ERP.Application.Modules.Commercial.DTOs;
using ERP.Domain.Common;
using ERP.Domain.Modules.Commercial.Entities;
using ERP.Domain.Modules.Commercial.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Commercial.UseCases.ConfirmSalesOrder;

public sealed class ConfirmSalesOrderCommandHandler : IRequestHandler<ConfirmSalesOrderCommand, Result<SalesOrderDto>>
{
    private readonly ISalesOrderRepository _orderRepo;
    private readonly ISnowflakeIdGenerator _idGenerator;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ConfirmSalesOrderCommandHandler> _logger;

    public ConfirmSalesOrderCommandHandler(
        ISalesOrderRepository orderRepo,
        ISnowflakeIdGenerator idGenerator,
        ICurrentUser currentUser,
        ILogger<ConfirmSalesOrderCommandHandler> logger)
    {
        _orderRepo = orderRepo;
        _idGenerator = idGenerator;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Result<SalesOrderDto>> Handle(ConfirmSalesOrderCommand command, CancellationToken ct)
    {
        var order = await _orderRepo.GetByPublicIdAsync(command.PublicId, ct);
        if (order is null)
            return Result<SalesOrderDto>.Failure("Sales order not found.");

        if (order.Status != SalesOrder.Statuses.Draft)
            return Result<SalesOrderDto>.Failure($"Only draft orders can be confirmed (current: {order.Status}).");

        if (order.Lines.Count == 0)
            return Result<SalesOrderDto>.Failure("Cannot confirm an order without lines.");

        var userId = _currentUser.UserId;
        order.Confirm(userId);
        _orderRepo.AssignPendingStatusHistoryIds(_idGenerator);

        await _orderRepo.SaveChangesAsync(ct);

        _logger.LogInformation("Sales order confirmed {OrderNumber} ({PublicId})", order.OrderNumber, order.PublicId);

        return Result<SalesOrderDto>.Success(SalesOrderDtoMapper.ToDto(order));
    }
}
