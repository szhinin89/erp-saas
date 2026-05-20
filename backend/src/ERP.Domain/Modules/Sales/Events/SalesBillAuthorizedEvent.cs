using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Events;

public sealed record SalesBillAuthorizedStockLine(Guid ProductId, decimal Quantity);

public sealed class SalesBillAuthorizedEvent : IDomainEvent
{
    public Guid     Id            { get; } = Guid.NewGuid();
    public DateTime OccurredOn   { get; } = DateTime.UtcNow;
    public Guid     SalesBillId  { get; }
    public Guid     SubscriberId     { get; }
    public Guid     UserId       { get; }
    public Guid     WarehouseId  { get; }
    public Guid     CompanyId    { get; }
    public string   BillNumber   { get; }
    public IReadOnlyList<SalesBillAuthorizedStockLine> StockLines { get; }

    public SalesBillAuthorizedEvent(
        Guid   salesBillId,
        Guid   subscriberId,
        Guid   userId,
        Guid   warehouseId,
        Guid   companyId,
        string billNumber,
        IReadOnlyList<SalesBillAuthorizedStockLine> stockLines)
    {
        SalesBillId = salesBillId;
        SubscriberId    = subscriberId;
        UserId      = userId;
        WarehouseId = warehouseId;
        CompanyId   = companyId;
        BillNumber  = billNumber;
        StockLines  = stockLines;
    }
}
