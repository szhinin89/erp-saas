using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>
/// Auditoría de dominio de <see cref="ElectronicDocument"/> — únicamente metadatos del ciclo
/// electrónico (tipo de documento, transición de estado). Nunca datos comerciales: eso
/// pertenece a la auditoría del módulo de origen (Sales, Purchases, ...), no a esta.
/// Append-only — nunca se edita ni se borra.
/// </summary>
public sealed class ElectronicDocumentAudit : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public ElectronicDocumentType DocumentType { get; private set; }
    public ElectronicDocumentState? FromState { get; private set; }
    public ElectronicDocumentState ToState { get; private set; }

    private ElectronicDocumentAudit() { }

    public static ElectronicDocumentAudit Create(
        AuditActor actor,
        Guid companyId,
        Guid electronicDocumentId,
        string action,
        ElectronicDocumentType documentType,
        ElectronicDocumentState? fromState,
        ElectronicDocumentState toState,
        string? reason = null
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("companyId requerido.", nameof(companyId));

        var audit = new ElectronicDocumentAudit
        {
            CompanyId = companyId,
            DocumentType = documentType,
            FromState = fromState,
            ToState = toState,
        };
        audit.SetCommon(actor, electronicDocumentId, action, reason);
        return audit;
    }
}
