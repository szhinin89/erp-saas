using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.Finance.Enums;

namespace ERP.Domain.Modules.Finance.Events;

/// <summary>Se levanta cuando <c>CompanyFinancialDestination.Create()</c> crea un destino nuevo (diseño §20.1).</summary>
public sealed class CompanyFinancialDestinationCreatedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid DestinationId { get; }
    public string Code { get; }
    public string Name { get; }
    public FinancialDestinationTypeCode DestinationTypeCode { get; }
    public Guid AccountingAccountId { get; }
    public bool IsActive { get; }

    public CompanyFinancialDestinationCreatedEvent(
        Guid tenantId,
        Guid destinationId,
        string code,
        string name,
        FinancialDestinationTypeCode destinationTypeCode,
        Guid accountingAccountId,
        bool isActive
    )
    {
        TenantId = tenantId;
        DestinationId = destinationId;
        Code = code;
        Name = name;
        DestinationTypeCode = destinationTypeCode;
        AccountingAccountId = accountingAccountId;
        IsActive = isActive;
    }

    Guid IAuditEvent.EntityId => DestinationId;
    string IAuditEvent.Action => "Created";
    string? IAuditEvent.Reason => null;
}
