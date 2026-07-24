using ERP.Domain.Common;

namespace ERP.Domain.Modules.Inventory.Entities;

public sealed class StockTransferLine : AuditableEntity, ITenantScopedEntity
{
    public const int DescriptionMaxLen = 300;

    public Guid    StockTransferId { get; private set; }
    public Guid    ProductId       { get; private set; }
    public decimal Quantity        { get; private set; }
    public string  Description     { get; private set; } = null!;

    private StockTransferLine() { }

    public static StockTransferLine Create(
        Guid    tenantId,
        Guid    stockTransferId,
        Guid    productId,
        decimal quantity,
        string  description,
        Guid    createdBy)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        var d = new StockTransferLine
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            StockTransferId = stockTransferId,
            ProductId       = productId,
            Quantity        = quantity,
            Description     = description.Trim(),
        };
        d.SetCreated(createdBy);
        return d;
    }
}
