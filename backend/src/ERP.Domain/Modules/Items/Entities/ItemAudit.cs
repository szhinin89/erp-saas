using ERP.Domain.Audit;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Auditoría de dominio de <see cref="Item"/>: creación, actualización, cambio de precio
/// base, activación y desactivación. Item es tenant-scoped únicamente (compartido entre
/// companies del tenant, ver Item.cs) — a diferencia de Pricing, esta auditoría no
/// lleva CompanyId. Solo los campos que Item necesita — nada de "God table". Append-only.
/// </summary>
public sealed class ItemAudit : AuditRecordBase
{
    /// <summary>Solo poblado para Action == "PriceChanged" — null en el resto de las acciones.</summary>
    public decimal? OldBaseSalePrice { get; private set; }

    /// <summary>Solo poblado para Action == "PriceChanged" — null en el resto de las acciones.</summary>
    public decimal? NewBaseSalePrice { get; private set; }

    private ItemAudit() { }

    public static ItemAudit Create(
        AuditActor actor, Guid itemId, string action,
        decimal? oldBaseSalePrice = null, decimal? newBaseSalePrice = null, string? reason = null)
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
