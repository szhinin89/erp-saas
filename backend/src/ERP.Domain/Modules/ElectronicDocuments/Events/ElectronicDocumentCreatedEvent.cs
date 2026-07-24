using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Events;

/// <summary>Se levanta cuando se registra un nuevo documento electrónico ya firmado y almacenado.</summary>
public sealed class ElectronicDocumentCreatedEvent : BaseDomainEvent, IAuditEvent
{
    public Guid ElectronicDocumentId { get; }
    public ElectronicDocumentType DocumentType { get; }
    public string SourceModule { get; }
    public Guid SourceEntityId { get; }

    public ElectronicDocumentCreatedEvent(
        Guid tenantId,
        Guid electronicDocumentId,
        ElectronicDocumentType documentType,
        string sourceModule,
        Guid sourceEntityId)
    {
        TenantId = tenantId;
        ElectronicDocumentId = electronicDocumentId;
        DocumentType = documentType;
        SourceModule = sourceModule;
        SourceEntityId = sourceEntityId;
    }

    Guid IAuditEvent.EntityId => ElectronicDocumentId;
    string IAuditEvent.Action => "Created";
    string? IAuditEvent.Reason => null;
}
