using ERP.Application.Audit;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Entities;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Ensamblador único del <see cref="ElectronicDocumentDiagnosticDto"/> — usado por los tres
/// puntos que construyen (o deberían construir) el diagnóstico completo de un documento
/// electrónico: <c>GetElectronicDocumentDetailQueryHandler</c> (Monitor),
/// <c>RetryElectronicDocumentCommandHandler</c> (respuesta del reintento manual) y
/// <c>GetElectronicDocumentDiagnosticBySourceQueryHandler</c> (consumo desde cualquier módulo,
/// p.ej. Ventas). Ningún handler construye este DTO a mano — evita que los tres diverjan.
/// </summary>
public static class ElectronicDocumentDiagnosticAssembler
{
    private const int MessageTake = 50;

    public static async Task<ElectronicDocumentDiagnosticDto> BuildAsync(
        ElectronicDocument document,
        IReadOnlyList<ElectronicDocumentAudit> timelineRecords,
        IAuditReader<ElectronicDocumentSriMessage> sriMessageReader,
        CancellationToken ct)
    {
        var timeline = ElectronicDocumentTimelineBuilder.Build(timelineRecords);

        var sriMessages = await sriMessageReader.GetByEntityAsync(document.TenantId, document.Id, MessageTake, ct);
        var messages = BuildMessages(document, sriMessages);

        return new ElectronicDocumentDiagnosticDto(
            document.CurrentState.ToString(),
            document.Environment,
            document.LastAttemptUtc,
            messages,
            timeline,
            new ElectronicDocumentTechnicalInfoDto(
                document.AccessKey?.Value,
                document.AuthorizationNumber?.Value,
                document.Environment,
                document.AuthorizationDate,
                document.RetryCount,
                document.LastAttemptUtc,
                CorrelationId: null),
            !string.IsNullOrWhiteSpace(document.XmlDraftPath),
            !string.IsNullOrWhiteSpace(document.SignedXmlPath),
            !string.IsNullOrWhiteSpace(document.AuthorizedXmlPath));
    }

    /// <summary>
    /// Mensajes SRI reales (<see cref="ElectronicDocumentSriMessage"/>) si existen. Si no hay
    /// ninguno y el documento tiene <c>LastError</c>, sintetiza un único mensaje de respaldo a
    /// partir de ese texto — nunca ambas fuentes a la vez, para no duplicar el mismo contenido
    /// (un rechazo real con mensajes estructurados ya tiene su <c>LastError</c> como el mismo
    /// texto aplanado). El fallback cubre errores técnicos/internos (SOAP Fault, timeout, XML
    /// inválido, certificado, excepciones, Hangfire) que nunca tuvieron una respuesta SRI
    /// estructurada real.
    /// </summary>
    private static IReadOnlyList<ElectronicDocumentMessageDto> BuildMessages(
        ElectronicDocument document, IReadOnlyList<ElectronicDocumentSriMessage> sriMessages)
    {
        if (sriMessages.Count > 0)
        {
            return sriMessages
                .OrderBy(m => m.OccurredAtUtc)
                .Select(m => new ElectronicDocumentMessageDto(m.Code, m.MessageType, m.Message, m.AdditionalInfo, m.OccurredAtUtc))
                .ToList();
        }

        if (string.IsNullOrWhiteSpace(document.LastError))
            return [];

        var occurredAt = document.LastAttemptUtc ?? document.UpdatedAt ?? document.CreatedAt;
        return [new ElectronicDocumentMessageDto(Code: null, MessageType: "ERROR", document.LastError, AdditionalInfo: null, occurredAt)];
    }
}
