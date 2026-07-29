using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Pricing.Enums;

namespace ERP.Domain.Modules.Pricing.Entities;

/// <summary>
/// Auditoría de dominio de <see cref="PriceList"/>: creación, cambios en la regla general
/// y activación/desactivación administrativa. Sin campos de PricingRule ni PriceListItem —
/// cada uno tiene su propia auditoría (ver <see cref="PricingRuleAudit"/>,
/// <see cref="PriceListItemAudit"/>). Append-only.
/// </summary>
public sealed class PriceListAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public PricingRuleType? OldRuleType { get; private set; }
    public decimal? OldRuleValue { get; private set; }
    public PricingRuleType? NewRuleType { get; private set; }
    public decimal? NewRuleValue { get; private set; }

    private PriceListAudit() { }

    public static PriceListAudit Create(
        AuditActor actor,
        Guid companyId,
        Guid priceListId,
        string action,
        PricingRuleType? oldRuleType = null,
        decimal? oldRuleValue = null,
        PricingRuleType? newRuleType = null,
        decimal? newRuleValue = null,
        string? reason = null
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));

        var audit = new PriceListAudit
        {
            CompanyId = companyId,
            OldRuleType = oldRuleType,
            OldRuleValue = oldRuleValue,
            NewRuleType = newRuleType,
            NewRuleValue = newRuleValue,
        };
        audit.SetCommon(actor, priceListId, action, reason);
        return audit;
    }
}
