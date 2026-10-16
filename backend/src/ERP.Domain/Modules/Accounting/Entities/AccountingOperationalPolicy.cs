using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — versión de la decisión operativa de Accounting de una Company, con
/// vigencia <see cref="EffectiveFrom"/>..<see cref="EffectiveTo"/> (fechas de negocio, inclusivas;
/// <see cref="EffectiveTo"/> null = vigente sin fin). Las versiones no se solapan: una versión
/// nueva cierra la anterior el día previo a su inicio. Nunca se borra ni reescribe una versión;
/// la única mutación permitida es fijar el fin de la versión abierta. La obligación fiscal sigue
/// siendo exclusivamente <c>Company.IsAccountingReq</c> (docs/architecture/accounting.md §3).
/// </summary>
public sealed class AccountingOperationalPolicy
    : AuditableEntity,
        ITenantScopedEntity,
        ICompanyOperationalEntity
{
    public const int ReasonMaxLength = 500;

    public Guid CompanyId { get; private set; }
    public AccountingOperationalMode Mode { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string? Reason { get; private set; }

    private AccountingOperationalPolicy() { }

    public static AccountingOperationalPolicy Create(
        Guid tenantId,
        Guid companyId,
        AccountingOperationalMode mode,
        DateOnly effectiveFrom,
        string? reason,
        Guid createdBy
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("El tenant es obligatorio.", nameof(tenantId));
        if (companyId == Guid.Empty)
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        if (!Enum.IsDefined(mode))
            throw new ArgumentException("Modo operativo inválido.", nameof(mode));
        if (effectiveFrom == default)
            throw new ArgumentException(
                "La fecha de vigencia es obligatoria.",
                nameof(effectiveFrom)
            );

        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmed is { Length: > ReasonMaxLength })
            throw new ArgumentException(
                $"El motivo no puede superar {ReasonMaxLength} caracteres.",
                nameof(reason)
            );

        var policy = new AccountingOperationalPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            Mode = mode,
            EffectiveFrom = effectiveFrom,
            Reason = trimmed,
        };
        policy.SetCreated(createdBy);
        return policy;
    }

    public bool Covers(DateOnly date) =>
        date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo.Value);

    /// <summary>Cierra esta versión abierta el día anterior a <paramref name="nextEffectiveFrom"/>.</summary>
    public void EndBefore(DateOnly nextEffectiveFrom, Guid updatedBy)
    {
        if (EffectiveTo is not null)
            throw new DomainRuleViolationException(
                "La versión de la política contable ya tiene fin de vigencia."
            );
        if (nextEffectiveFrom <= EffectiveFrom)
            throw new DomainRuleViolationException(
                $"La nueva vigencia debe iniciar después del {EffectiveFrom:yyyy-MM-dd}, inicio de la versión vigente."
            );

        EffectiveTo = nextEffectiveFrom.AddDays(-1);
        SetUpdated(updatedBy);
    }
}
