using ERP.Application.Common;
using ERP.Application.Modules.Communications.Templates;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.Communications.ElectronicDocuments;

/// <summary>
/// ZH-EDOC-COMMUNICATIONS-01 (ADR-038 / ADR-039 fase 5) — aporte del módulo dueño de un comprobante
/// autorizado a su comunicación. El módulo dueño (Sales, Retentions…) conoce a su destinatario y sus
/// datos; Communications es dueño del template, la identidad, los adjuntos y la outbox.
/// <para>
/// Un contributor NUNCA arma asunto/HTML/texto, no renderiza, no escapa HTML y no escribe la outbox
/// (reglas de arquitectura en <c>CommunicationsBoundaryTests</c>): devuelve solo datos.
/// </para>
/// </summary>
public interface IElectronicDocumentCommunicationContributor
{
    /// <summary>Valor exacto de <c>ElectronicDocument.SourceModule</c> que este contributor atiende.</summary>
    string SourceModule { get; }

    /// <summary>
    /// Tipos de comprobante soportados y, para cada uno, el propósito y el <c>SourceType</c> oficial del
    /// documento de negocio: ruta única (SourceModule, DocumentType) → (Purpose, SourceType), usada por el
    /// handler y por la reconciliación (que correlaciona con la outbox por su índice de origen).
    /// </summary>
    IReadOnlyDictionary<
        ElectronicDocumentType,
        ElectronicDocumentCommunicationTarget
    > Targets
    { get; }

    /// <summary>
    /// Fallos estructurados (<see cref="Result{T}.Code"/>): <c>COMMUNICATION_SOURCE_NOT_FOUND</c>
    /// (el documento de origen no existe para el tenant/empresa del comprobante) o
    /// <c>COMMUNICATION_SOURCE_NOT_ELIGIBLE</c> (existe pero su estado no admite la comunicación). Un
    /// destinatario sin correo NO es un fallo del contributor: se devuelve con email null y la cola lo
    /// registra como evidencia durable.
    /// </summary>
    Task<Result<ElectronicDocumentCommunicationContribution>> ContributeAsync(
        ElectronicDocumentCommunicationContext context,
        CancellationToken ct = default
    );
}

/// <summary>Destino de una ruta: propósito (= TemplateKey) y tipo oficial del documento de negocio de origen.</summary>
public sealed record ElectronicDocumentCommunicationTarget(string Purpose, string SourceType);

/// <param name="Document">Comprobante autorizado (solo lectura).</param>
/// <param name="IssuerName">Nombre comercial (o razón social) de la empresa emisora, ya resuelto.</param>
public sealed record ElectronicDocumentCommunicationContext(
    ElectronicDocument Document,
    string IssuerName
);

/// <param name="Purpose">Propósito (= TemplateKey) del tipo de comprobante.</param>
/// <param name="Source">Origen oficial del documento de negocio (módulo, tipo, id): parte de la identidad.</param>
/// <param name="RecipientRole"></param>
/// <param name="RecipientName"></param>
/// <param name="RecipientEmail">Null/vacío si el destinatario no tiene correo: la cola deja evidencia RECIPIENT_MISSING.</param>
/// <param name="BranchId">Sucursal del documento de negocio, si la tiene.</param>
/// <param name="DocumentNumber">Número visible del comprobante, para nombrar los adjuntos.</param>
/// <param name="TemplateModel">Variables tipadas del template del propósito.</param>
public sealed record ElectronicDocumentCommunicationContribution(
    string Purpose,
    CommunicationSource Source,
    CommunicationRecipientRole RecipientRole,
    string? RecipientName,
    string? RecipientEmail,
    Guid? BranchId,
    string DocumentNumber,
    ICommunicationTemplateModel TemplateModel
);

/// <summary>Resultado de pedir la comunicación de un comprobante autorizado (evento o reconciliación).</summary>
public enum ElectronicDocumentCommunicationOutcome
{
    /// <summary>Se creó la comunicación (Pending, o Failed con evidencia si faltó destinatario/template).</summary>
    Queued,

    /// <summary>Ya existía (misma identidad): evento duplicado, reproceso o reconciliación concurrente.</summary>
    AlreadyQueued,

    /// <summary>No corresponde comunicar (preferencia desactivada, origen no soportado/no elegible…).</summary>
    Skipped,
}

/// <param name="Outcome"></param>

/// <param name="CommunicationId"></param>/// <param name="FailureCode">Código estructurado: motivo del Skipped, o fallo previo a la entrega registrado en la fila.</param>
public sealed record ElectronicDocumentCommunicationResult(
    ElectronicDocumentCommunicationOutcome Outcome,
    Guid? CommunicationId = null,
    string? FailureCode = null
);
