using ERP.Domain.Common;
using ERP.Domain.Modules.Pricing.Enums;
using ERP.Domain.Modules.Pricing.Events;

namespace ERP.Domain.Modules.Pricing.Entities;

/// <summary>
/// Contenedor de reglas de precio. No almacena precios absolutos por ítem — representa
/// cómo transformar el precio base (Item.BaseSalePrice). Puede tener una regla general
/// (aplica a todo el catálogo) que las PricingRule por ítem sobrescriben cuando existen.
/// </summary>
public sealed class PriceList : MasterEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }

    public const int MaxCodeLength = 20;
    public const int MaxNameLength = 120;
    public const int CurrencyCodeLen = 3;

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string CurrencyCode { get; private set; } = null!;
    public bool IsDefault { get; private set; }
    public DateOnly? ValidFrom { get; private set; }
    public DateOnly? ValidUntil { get; private set; }

    /// <summary>Regla general de la lista (aplica a todo el catálogo salvo override por ítem). Null = sin ajuste (precio = base).</summary>
    public PricingRuleType? RuleType { get; private set; }
    public decimal? RuleValue { get; private set; }

    private PriceList() { }

    public static PriceList Create(
        Guid tenantId,
        Guid companyId,
        string code,
        string name,
        string currencyCode,
        bool isDefault,
        Guid createdBy,
        DateOnly? validFrom = null,
        DateOnly? validUntil = null,
        PricingRuleType? ruleType = null,
        decimal? ruleValue = null
    )
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException(
                "El código de lista de precios es obligatorio.",
                nameof(code)
            );
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "El nombre de lista de precios es obligatorio.",
                nameof(name)
            );
        if (
            string.IsNullOrWhiteSpace(currencyCode)
            || currencyCode.Trim().Length != CurrencyCodeLen
        )
            throw new ArgumentException(
                "El código de moneda debe tener 3 caracteres.",
                nameof(currencyCode)
            );
        if (validFrom.HasValue && validUntil.HasValue && validUntil < validFrom)
            throw new ArgumentException(
                "La fecha fin no puede ser anterior a la fecha inicio.",
                nameof(validUntil)
            );
        ValidateRule(ruleType, ruleValue);

        var pl = new PriceList
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            CurrencyCode = currencyCode.Trim().ToUpperInvariant(),
            IsDefault = isDefault,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            RuleType = ruleType,
            RuleValue = ruleValue,
        };
        pl.SetCreated(createdBy);
        pl.RaiseDomainEvent(new PriceListCreatedEvent(tenantId, pl.Id));
        return pl;
    }

    /// <summary>
    /// Fábrica exclusiva del Bootstrap de empresa: idéntica a <see cref="Create"/> pero marca el
    /// registro como sembrado por el sistema (<see cref="MasterEntity.IsSystemSeeded"/>), bloqueando
    /// <see cref="Disable"/> — Tipo A (Lista General), ver política de Bootstrap en <c>CLAUDE.md</c>.
    /// <see cref="Update"/> permanece abierto.
    /// </summary>
    public static PriceList CreateSystemSeeded(
        Guid tenantId,
        Guid companyId,
        string code,
        string name,
        string currencyCode,
        bool isDefault,
        Guid createdBy,
        DateOnly? validFrom = null,
        DateOnly? validUntil = null,
        PricingRuleType? ruleType = null,
        decimal? ruleValue = null
    )
    {
        var pl = Create(
            tenantId,
            companyId,
            code,
            name,
            currencyCode,
            isDefault,
            createdBy,
            validFrom,
            validUntil,
            ruleType,
            ruleValue
        );
        pl.MarkAsSystemSeeded();
        return pl;
    }

    /// <summary>Shadow de <see cref="MasterEntity.Enable"/>: mismo comportamiento + evento de auditoría de dominio.</summary>
    public new void Enable(Guid updatedBy)
    {
        base.Enable(updatedBy);
        RaiseDomainEvent(new PriceListEnabledEvent(TenantId, Id));
    }

    /// <summary>
    /// Override real de <see cref="MasterEntity.Disable"/> (antes ocultaba el método base con
    /// <c>new</c>, lo que dejaba sin protección ni evento de dominio cualquier llamada hecha a través
    /// de una referencia <c>MasterEntity</c>) + evento de auditoría de dominio + guard de
    /// <see cref="ISystemSeeded"/>.
    /// </summary>
    public override void Disable(Guid updatedBy)
    {
        this.EnsureEditable("La lista de precios", "deshabilitarse");

        base.Disable(updatedBy);
        RaiseDomainEvent(new PriceListDisabledEvent(TenantId, Id));
    }

    public void Update(
        string name,
        string currencyCode,
        bool isDefault,
        Guid updatedBy,
        DateOnly? validFrom = null,
        DateOnly? validUntil = null,
        PricingRuleType? ruleType = null,
        decimal? ruleValue = null
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "El nombre de lista de precios es obligatorio.",
                nameof(name)
            );
        if (
            string.IsNullOrWhiteSpace(currencyCode)
            || currencyCode.Trim().Length != CurrencyCodeLen
        )
            throw new ArgumentException(
                "El código de moneda debe tener 3 caracteres.",
                nameof(currencyCode)
            );
        if (validFrom.HasValue && validUntil.HasValue && validUntil < validFrom)
            throw new ArgumentException(
                "La fecha fin no puede ser anterior a la fecha inicio.",
                nameof(validUntil)
            );
        ValidateRule(ruleType, ruleValue);

        var oldRuleType = RuleType;
        var oldRuleValue = RuleValue;

        Name = name.Trim();
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        IsDefault = isDefault;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        RuleType = ruleType;
        RuleValue = ruleValue;
        SetUpdated(updatedBy);
        RaiseDomainEvent(
            new PriceListUpdatedEvent(TenantId, Id, oldRuleType, oldRuleValue, ruleType, ruleValue)
        );
    }

    private static void ValidateRule(PricingRuleType? ruleType, decimal? ruleValue)
    {
        if (ruleType.HasValue && !ruleValue.HasValue)
            throw new ArgumentException(
                "El valor de la regla general es obligatorio cuando se especifica un tipo de regla.",
                nameof(ruleValue)
            );
        if (!ruleType.HasValue && ruleValue.HasValue)
            throw new ArgumentException(
                "No se puede especificar un valor de regla sin un tipo de regla.",
                nameof(ruleType)
            );
        if (ruleType is PricingRuleType.FixedPrice && ruleValue < 0)
            throw new ArgumentException(
                "El precio fijo de la regla no puede ser negativo.",
                nameof(ruleValue)
            );
    }

    /// <summary>Vigente en la fecha indicada según ValidFrom/ValidUntil (ambos opcionales).</summary>
    public bool IsValidOn(DateOnly date) =>
        (!ValidFrom.HasValue || date >= ValidFrom.Value)
        && (!ValidUntil.HasValue || date <= ValidUntil.Value);
}
