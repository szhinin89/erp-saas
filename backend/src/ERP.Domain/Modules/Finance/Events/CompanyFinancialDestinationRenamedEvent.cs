using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Finance.Events;

/// <summary>Se levanta cuando <c>CompanyFinancialDestination.UpdateName()</c> cambia el nombre (diseño §6.4ter, §20.1).</summary>
public sealed class CompanyFinancialDestinationRenamedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid DestinationId { get; }
    public string Code { get; }
    public string OldName { get; }
    public string NewName { get; }

    public CompanyFinancialDestinationRenamedEvent(
        Guid tenantId,
        Guid destinationId,
        string code,
        string oldName,
        string newName
    )
    {
        TenantId = tenantId;
        DestinationId = destinationId;
        Code = code;
        OldName = oldName;
        NewName = newName;
    }

    Guid IAuditEvent.EntityId => DestinationId;
    string IAuditEvent.Action => "Renamed";
    string? IAuditEvent.Reason => null;
}
