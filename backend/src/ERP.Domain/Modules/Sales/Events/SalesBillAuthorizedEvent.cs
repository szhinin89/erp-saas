using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Events;

public sealed record SalesBillAuthorizedStockLine(Guid ProductId, decimal Quantity);

public sealed class SalesBillAuthorizedEvent : IDomainEvent
{
    public Guid     Id            { get; } = Guid.NewGuid();
    public DateTime OccurredOn   { get; } = DateTime.UtcNow;
    public Guid     SalesBillId  { get; }
    public Guid     TenantId     { get; }
    public Guid     UserId       { get; }
    public Guid     WarehouseId  { get; }
    public string   BillNumber   { get; }
    public IReadOnlyList<SalesBillAuthorizedStockLine> StockLines { get; }

    public SalesBillAuthorizedEvent(
        Guid   salesBillId,
        Guid   tenantId,
        Guid   userId,
        Guid   warehouseId,
        string billNumber,
        IReadOnlyList<SalesBillAuthorizedStockLine> stockLines)
    {
        SalesBillId = salesBillId;
        TenantId    = tenantId;
        UserId      = userId;
        WarehouseId = warehouseId;
        BillNumber  = billNumber;
        StockLines  = stockLines;
    }
}
