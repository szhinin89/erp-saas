using ERP.Domain.Common;
using ERP.Domain.Modules.Pricing.Enums;
using ERP.Domain.Modules.Pricing.Events;

namespace ERP.Domain.Modules.Pricing.Entities;

/// <summary>
/// Override de la regla general de una PriceList para un ítem específico. Transforma
/// Item.BaseSalePrice — nunca almacena un precio absoluto propio salvo que RuleType sea
/// FixedPrice. Descuentos por volumen/cantidad, promociones, cupones y campañas pertenecen
/// al futuro Promotion Engine — fuera de este agregado.
///
/// No soporta pricing por variante (ItemVariantId) — se evaluó y se retiró deliberadamente
/// (auditoría de cierre 2026-07-07): no existía flujo funcional end-to-end (el repositorio
/// exigía igualdad exacta sin fallback a la regla del ítem, ningún llamador real de
/// IPricingResolver pasaba variante, la simulación no la soportaba, y la UI no tenía
/// selector). Reintroducir pricing por variante requiere una implementación completa
/// (repositorio, resolver, comandos, DTOs, simulación, UI) en una única entrega — nunca
/// soporte parcial.
/// </summary>
public sealed class PricingRule : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public Guid PriceListId { get; private set; }
    public Guid ItemId { get; private set; }
    public PricingRuleType RuleType { get; private set; }
    public decimal RuleValue { get; private set; }
    public bool IsActive { get; private set; } = true;

    private PricingRule() { }

    public static PricingRule Create(
        Guid tenantId,
        Guid companyId,
        Guid priceListId,
        Guid itemId,
        PricingRuleType ruleType,
        decimal ruleValue,
        Guid createdBy)
    {
        ValidateRule(ruleType, ruleValue);

        var rule = new PricingRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            PriceListId = priceListId,
            ItemId = itemId,
            RuleType = ruleType,
            RuleValue = ruleValue,
            IsActive = true,
        };
        rule.SetCreated(createdBy);
        return rule;
    }

    public void UpdateRule(PricingRuleType ruleType, decimal ruleValue, Guid updatedBy)
    {
        ValidateRule(ruleType, ruleValue);
        var oldRuleType = RuleType;
        var oldRuleValue = RuleValue;
        RuleType = ruleType;
        RuleValue = ruleValue;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new PricingRuleUpdatedEvent(
            TenantId, Id, PriceListId, ItemId, oldRuleType, oldRuleValue, ruleType, ruleValue));
    }

    /// <summary>
    /// Deshabilita la regla (nunca se elimina físicamente). El valor anterior queda
    /// registrado en la auditoría de dominio (PricingRuleAudit), vía PricingRuleDisabledEvent.
    /// </summary>
    public void Disable(Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("La regla ya está deshabilitada.");
        IsActive = false;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new PricingRuleDisabledEvent(TenantId, Id, PriceListId, ItemId, RuleType, RuleValue));
    }

    public void Enable(Guid updatedBy)
    {
        if (IsActive)
            throw new InvalidOperationException("La regla ya está activa.");
        IsActive = true;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new PricingRuleEnabledEvent(TenantId, Id, PriceListId, ItemId, RuleType, RuleValue));
    }

    private static void ValidateRule(PricingRuleType ruleType, decimal ruleValue)
    {
        if (ruleType == PricingRuleType.FixedPrice && ruleValue < 0)
            throw new ArgumentException("El precio fijo no puede ser negativo.", nameof(ruleValue));
    }
}
