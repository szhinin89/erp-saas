using ERP.Domain.Common;
using ERP.Domain.Modules.Inventory.Enums;

namespace ERP.Domain.Modules.Inventory.Entities;

public sealed class StockMovement : AuditableEntity, ISubscriberScopedEntity, ICompanyOperationalEntity
{
    public Guid? CompanyId { get; private set; }
    public const int ReferenceMaxLen        = 100;
    public const int SourceDocTypeMaxLen    = 50;

    public Guid              ProductId        { get; private set; }
    public Guid              WarehouseId      { get; private set; }
    public StockMovementType MovementType     { get; private set; }
    public decimal           Quantity         { get; private set; }
    public decimal           PreviousQuantity { get; private set; }
    public decimal           ResultQuantity   { get; private set; }
    public string?           Reference        { get; private set; }
    public Guid?             SourceDocId      { get; private set; }
    public string?           SourceDocType    { get; private set; }
    public decimal?          UnitCost         { get; private set; }
    public decimal?          TotalCost        { get; private set; }

    private StockMovement() { }

    public static StockMovement Create(
        Guid             subscriberId,
        Guid             productId,
        Guid             warehouseId,
        StockMovementType movementType,
        decimal          quantity,
        decimal          previousQuantity,
        string?          reference,
        Guid?            sourceDocId,
        string?          sourceDocType,
        Guid             createdBy,
        decimal?         unitCost = null,
        Guid?            companyId = null)
    {
        var m = new StockMovement
        {
            Id               = Guid.NewGuid(),
            SubscriberId         = subscriberId,
            CompanyId            = companyId,
            ProductId        = productId,
            WarehouseId      = warehouseId,
            MovementType     = movementType,
            Quantity         = quantity,
            PreviousQuantity = previousQuantity,
            ResultQuantity   = previousQuantity + quantity,
            Reference        = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            SourceDocId      = sourceDocId,
            SourceDocType    = string.IsNullOrWhiteSpace(sourceDocType) ? null : sourceDocType.Trim(),
            UnitCost         = unitCost,
            TotalCost        = unitCost.HasValue ? Math.Abs(quantity) * unitCost.Value : null,
        };
        m.SetCreated(createdBy);
        return m;
    }
}
