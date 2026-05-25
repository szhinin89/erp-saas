using ERP.Domain.Common;

namespace ERP.Domain.Modules.Commercial.Events;

public sealed class SalesOrderCreatedEvent : BaseDomainEvent
{
    public long SalesOrderId { get; }
    public Guid SalesOrderPublicId { get; }
    public Guid SubscriberId { get; }
    public Guid BranchId { get; }
    public Guid WarehouseId { get; }
    public Guid UserId { get; }

    public SalesOrderCreatedEvent(
        long salesOrderId,
        Guid salesOrderPublicId,
        Guid subscriberId,
        Guid branchId,
        Guid warehouseId,
        Guid userId)
    {
        SalesOrderId = salesOrderId;
        SalesOrderPublicId = salesOrderPublicId;
        SubscriberId = subscriberId;
        BranchId = branchId;
        WarehouseId = warehouseId;
        UserId = userId;
        SubscriberId = subscriberId;
    }
}
