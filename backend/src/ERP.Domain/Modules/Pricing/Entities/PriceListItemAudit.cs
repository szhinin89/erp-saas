using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Pricing.Entities;

/// <summary>
/// Auditoría de dominio de <see cref="PriceListItem"/>: asignación/activación/desactivación
/// de un ítem a una lista de precios. Sin campos de reglas ni precios — eso es
/// responsabilidad de <see cref="PricingRuleAudit"/>. Append-only.
/// </summary>
public sealed class PriceListItemAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid PriceListId { get; private set; }
    public Guid ItemId { get; private set; }

    private PriceListItemAudit() { }

    public static PriceListItemAudit Create(
        AuditActor actor, Guid companyId, Guid assignmentId, Guid priceListId, Guid itemId, string action,
        string? reason = null)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("companyId requerido.", nameof(companyId));

        var audit = new PriceListItemAudit
        {
            CompanyId = companyId,
            PriceListId = priceListId,
            ItemId = itemId,
        };
        audit.SetCommon(actor, assignmentId, action, reason);
        return audit;
    }
}
