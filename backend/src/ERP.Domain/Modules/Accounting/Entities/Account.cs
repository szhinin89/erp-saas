using ERP.Domain.Common;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Events;
using ERP.Domain.Modules.Accounting.ValueObjects;

namespace ERP.Domain.Modules.Accounting.Entities;

/// <summary>
/// Cuenta del Plan de Cuentas. CompanyId-scoped obligatorio (ADR-026 §2) — no existe cuenta
/// compartida entre companies. Jerarquía avanzada (validación de ciclos, resolución de
/// ancestros) queda para una fase posterior; este esqueleto solo fija identidad y
/// clasificación (ADR-026 §5).
/// </summary>
/// <remarks>
/// Los siguientes invariantes de <see cref="ParentAccountId"/> son cross-aggregate — requieren
/// consultar otras filas de <c>Account</c> y no pueden validarse dentro de una única instancia
/// de este aggregate — y quedan pendientes para la capa de Application/Repository cuando se
/// implemente persistencia (fase posterior), no son responsabilidad de este esqueleto:
/// <list type="bullet">
/// <item><description>Validación de jerarquía padre/hijo (que <see cref="ParentAccountId"/> exista y admita hijas).</description></item>
/// <item><description>Detección de ciclos (que una cuenta no termine siendo ancestro de sí misma a través de la cadena de padres).</description></item>
/// <item><description>Validación de pertenencia a la misma <see cref="CompanyId"/> (que el padre referenciado pertenezca a la misma empresa que la cuenta hija).</description></item>
/// </list>
/// Estas reglas no se mueven al dominio en esta fase — mismo criterio ya aplicado al invariante
/// de solapamiento de <see cref="AccountingPeriod"/>.
/// </remarks>
public sealed class Account : AuditableEntity, ITenantScopedEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public AccountCode Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public Guid? ParentAccountId { get; private set; }
    public AccountType AccountType { get; private set; }
    public AccountNature Nature { get; private set; }
    public bool AllowsPosting { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Account() { }

    public static Account Create(
        Guid tenantId,
        Guid companyId,
        AccountCode code,
        string name,
        Guid? parentAccountId,
        AccountType accountType,
        AccountNature nature,
        bool allowsPosting,
        Guid createdBy
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la cuenta es obligatorio.", nameof(name));

        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            Code = code,
            Name = name.Trim(),
            ParentAccountId = parentAccountId,
            AccountType = accountType,
            Nature = nature,
            AllowsPosting = allowsPosting,
            IsActive = true,
        };
        account.SetCreated(createdBy);
        account.RaiseDomainEvent(new AccountCreatedEvent(tenantId, account.Id, companyId));
        return account;
    }

    public void Activate(Guid updatedBy)
    {
        if (IsActive)
            throw new InvalidOperationException("La cuenta ya está activa.");
        IsActive = true;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new AccountActivatedEvent(TenantId, Id, CompanyId));
    }

    /// <summary>Baja lógica — nunca DELETE físico (regla general del proyecto).</summary>
    public void Disable(Guid updatedBy)
    {
        if (!IsActive)
            throw new InvalidOperationException("La cuenta ya está deshabilitada.");
        IsActive = false;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new AccountDisabledEvent(TenantId, Id, CompanyId));
    }

    /// <summary>Corrige el nombre descriptivo de la cuenta. No toca Code/AccountType/Nature/ParentAccountId.</summary>
    public void Rename(string name, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la cuenta es obligatorio.", nameof(name));

        Name = name.Trim();
        SetUpdated(updatedBy);
    }

    /// <summary>
    /// ACCOUNTING-CHART-OF-ACCOUNTS-02: reubica la cuenta en la jerarquía. Existencia del padre,
    /// pertenencia a la misma Company y ausencia de ciclos son invariantes cross-aggregate — se
    /// validan en Application antes de llamar este método (mismo criterio ya documentado en los
    /// <c>&lt;remarks&gt;</c> de esta clase para <see cref="ParentAccountId"/>).
    /// </summary>
    public void UpdateParent(Guid? parentAccountId, Guid updatedBy)
    {
        ParentAccountId = parentAccountId;
        SetUpdated(updatedBy);
    }

    /// <summary>ACCOUNTING-CHART-OF-ACCOUNTS-02: habilita/deshabilita que esta cuenta reciba líneas de asiento.</summary>
    public void SetAllowsPosting(bool allowsPosting, Guid updatedBy)
    {
        AllowsPosting = allowsPosting;
        SetUpdated(updatedBy);
    }
}
