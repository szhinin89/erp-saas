using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Pricing.Enums;

namespace ERP.Domain.Modules.Pricing.Entities;

/// <summary>
/// Auditoría de dominio de <see cref="PricingRule"/>: solo los campos que Pricing
/// necesita (old/new tipados), nada de columnas de otros módulos. Append-only —
/// nunca se edita ni se borra.
/// </summary>
public sealed class PricingRuleAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid PriceListId { get; private set; }
    public Guid ItemId { get; private set; }
    public PricingRuleType OldRuleType { get; private set; }
    public decimal OldRuleValue { get; private set; }
    public PricingRuleType NewRuleType { get; private set; }
    public decimal NewRuleValue { get; private set; }

    private PricingRuleAudit() { }

    public static PricingRuleAudit Create(
        AuditActor actor,
        Guid companyId,
        Guid ruleId,
        Guid priceListId,
        Guid itemId,
        string action,
        PricingRuleType oldRuleType,
        decimal oldRuleValue,
        PricingRuleType newRuleType,
        decimal newRuleValue,
        string? reason = null
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));

        var audit = new PricingRuleAudit
        {
            CompanyId = companyId,
            PriceListId = priceListId,
            ItemId = itemId,
            OldRuleType = oldRuleType,
            OldRuleValue = oldRuleValue,
            NewRuleType = newRuleType,
            NewRuleValue = newRuleValue,
        };
        audit.SetCommon(actor, ruleId, action, reason);
        return audit;
    }
}
