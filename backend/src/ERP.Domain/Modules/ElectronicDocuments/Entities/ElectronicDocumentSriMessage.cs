using ERP.Domain.Audit;
using ERP.Domain.Common;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;

namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>
/// Un mensaje SRI real e individual (<c>&lt;mensaje&gt;</c>) asociado a un
/// <see cref="ElectronicDocument"/> — nunca resumido ni traducido. Distinto de
/// <see cref="ElectronicDocumentAudit"/> (que audita transiciones de estado): esta entidad
/// audita el contenido textual íntegro de una respuesta SRI, potencialmente varios mensajes por
/// transición. Append-only — nunca se edita ni se borra. Sigue el mismo patrón de
/// <c>AuditRecordBase</c> ya usado por <see cref="ElectronicDocumentAudit"/>/<c>PricingRuleAudit</c>
/// — reutiliza <c>IAuditReader&lt;T&gt;</c>/<c>IAuditWriter&lt;T&gt;</c> genéricos sin
/// modificarlos (AI-RULES/AUDIT-INFRASTRUCTURE.md).
/// </summary>
public sealed class ElectronicDocumentSriMessage : AuditRecordBase, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public string? Code { get; private set; }
    public string MessageType { get; private set; } = null!;
    public string Message { get; private set; } = null!;
    public string? AdditionalInfo { get; private set; }

    private ElectronicDocumentSriMessage() { }

    public static ElectronicDocumentSriMessage Create(
        AuditActor actor, Guid companyId, Guid electronicDocumentId, SriMessage message)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("companyId requerido.", nameof(companyId));

        var entry = new ElectronicDocumentSriMessage
        {
            CompanyId = companyId,
            Code = message.Code,
            MessageType = message.MessageType,
            Message = message.Message,
            AdditionalInfo = message.AdditionalInfo,
        };
        entry.SetCommon(actor, electronicDocumentId, "SriMessage", reason: null);
        return entry;
    }
}
