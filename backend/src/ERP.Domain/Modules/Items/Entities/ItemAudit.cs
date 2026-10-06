using ERP.Domain.Audit;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Auditoría de dominio de <see cref="Item"/>: creación, actualización, cambio de precio
/// base, activación y desactivación. Hereda el scope Tenant + Company a través del Item
/// auditado, sin duplicar CompanyId. Append-only.
/// </summary>
public sealed class ItemAudit : AuditRecordBase
{
    /// <summary>Solo poblado para Action == "PriceChanged" — null en el resto de las acciones.</summary>
    public decimal? OldBaseSalePrice { get; private set; }

    /// <summary>Solo poblado para Action == "PriceChanged" — null en el resto de las acciones.</summary>
    public decimal? NewBaseSalePrice { get; private set; }

    private ItemAudit() { }

    public static ItemAudit Create(
        AuditActor actor,
        Guid itemId,
        string action,
        decimal? oldBaseSalePrice = null,
        decimal? newBaseSalePrice = null,
        string? reason = null
    )
    {
        var audit = new ItemAudit
        {
            OldBaseSalePrice = oldBaseSalePrice,
            NewBaseSalePrice = newBaseSalePrice,
        };
        audit.SetCommon(actor, itemId, action, reason);
        return audit;
    }
}
