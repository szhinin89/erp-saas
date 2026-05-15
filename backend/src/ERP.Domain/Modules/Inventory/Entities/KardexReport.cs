using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

public sealed class KardexReport : ITenantEntity
{
    public const string StatusPending    = "Pending";
    public const string StatusProcessing = "Processing";
    public const string StatusCompleted  = "Completed";
    public const string StatusError      = "Error";

    public Guid      Id          { get; private set; }
    public Guid      TenantId    { get; private set; }
    public Guid      ProductId   { get; private set; }
    public Guid      WarehouseId { get; private set; }
    public DateTime? DateFrom    { get; private set; }
    public DateTime? DateTo      { get; private set; }
    public string    Status      { get; private set; } = StatusPending;
    public string?   ResultJson  { get; private set; }
    public string?   ErrorMessage { get; private set; }
    public DateTime  RequestedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private KardexReport() { }

    public static KardexReport Create(
        Guid tenantId, Guid productId, Guid warehouseId,
        DateTime? dateFrom, DateTime? dateTo)
        => new()
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            ProductId   = productId,
            WarehouseId = warehouseId,
            DateFrom    = dateFrom,
            DateTo      = dateTo,
            Status      = StatusPending,
            RequestedAt = DateTime.UtcNow,
        };

    public void MarkProcessing() => Status = StatusProcessing;

    public void MarkCompleted(string resultJson)
    {
        Status      = StatusCompleted;
        ResultJson  = resultJson;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkError(string message)
    {
        Status       = StatusError;
        ErrorMessage = message;
        CompletedAt  = DateTime.UtcNow;
    }
}
