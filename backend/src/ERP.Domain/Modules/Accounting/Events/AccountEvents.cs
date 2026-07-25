using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Events;

/// <summary>Se levanta cuando <c>Account.Create()</c> registra una cuenta nueva en el Plan de Cuentas.</summary>
public sealed class AccountCreatedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid AccountId { get; }
    public Guid CompanyId { get; }

    public AccountCreatedEvent(Guid tenantId, Guid accountId, Guid companyId)
    {
        TenantId = tenantId;
        AccountId = accountId;
        CompanyId = companyId;
    }

    Guid IAuditEvent.EntityId => AccountId;
    string IAuditEvent.Action => "Created";
    string? IAuditEvent.Reason => null;
}

/// <summary>Se levanta cuando <c>Account.Activate()</c> reactiva una cuenta deshabilitada.</summary>
public sealed class AccountActivatedEvent : BaseDomainEvent
{
    public Guid AccountId { get; }
    public Guid CompanyId { get; }

    public AccountActivatedEvent(Guid tenantId, Guid accountId, Guid companyId)
    {
        TenantId = tenantId;
        AccountId = accountId;
        CompanyId = companyId;
    }
}

/// <summary>Se levanta cuando <c>Account.Disable()</c> desactiva una cuenta activa (soft disable — nunca DELETE físico).</summary>
public sealed class AccountDisabledEvent : BaseDomainEvent
{
    public Guid AccountId { get; }
    public Guid CompanyId { get; }

    public AccountDisabledEvent(Guid tenantId, Guid accountId, Guid companyId)
    {
        TenantId = tenantId;
        AccountId = accountId;
        CompanyId = companyId;
    }
}
