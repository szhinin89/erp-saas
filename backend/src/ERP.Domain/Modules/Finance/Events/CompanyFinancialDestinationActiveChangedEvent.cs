using ERP.Domain.Audit;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Finance.Events;

/// <summary>
/// Se levanta cuando <c>CompanyFinancialDestination.SetActive()</c> cambia <c>IsActive</c> (diseño
/// §6.4ter, §20.1). Habilita/deshabilita la selección del destino en operaciones nuevas — nunca
/// afecta transacciones ya confirmadas.
/// </summary>
public sealed class CompanyFinancialDestinationActiveChangedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid DestinationId { get; }
    public string Code { get; }
    public bool OldIsActive { get; }
    public bool NewIsActive { get; }

    public CompanyFinancialDestinationActiveChangedEvent(
        Guid tenantId,
        Guid destinationId,
        string code,
        bool oldIsActive,
        bool newIsActive
    )
    {
        TenantId = tenantId;
        DestinationId = destinationId;
        Code = code;
        OldIsActive = oldIsActive;
        NewIsActive = newIsActive;
    }

    Guid IAuditEvent.EntityId => DestinationId;
    string IAuditEvent.Action => NewIsActive ? "Activated" : "Deactivated";
    string? IAuditEvent.Reason => null;
}
