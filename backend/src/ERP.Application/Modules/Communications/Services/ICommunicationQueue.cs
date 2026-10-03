using ERP.Application.Modules.Communications.DTOs;
using ERP.Application.Modules.Communications.Templates;
using ERP.Domain.Modules.Communications.Enums;
using ERP.Domain.Modules.Communications.ValueObjects;

namespace ERP.Application.Modules.Communications.Services;

/// <summary>
/// ZH-COMMUNICATIONS-CONTRACT-01 — ÚNICO camino para crear una comunicación (ADR-039 D6/D7). El
/// alcance llega explícito en la request (nunca se infiere del contexto ambiente) y la identidad
/// idempotente la construye <see cref="CommunicationIdentity"/>: el caller no arma claves.
/// </summary>
public interface ICommunicationQueue
{
    Task<QueuedCommunicationDto> EnqueueAsync(CommunicationRequest request, CancellationToken ct = default);
}

/// <param name="Scope">Alcance explícito: <see cref="CommunicationScope.Company"/> o <see cref="CommunicationScope.System"/>.</param>
/// <param name="Purpose">Propósito registrado en <c>CommunicationPurposes</c> (define alcance y canales permitidos).</param>
/// <param name="Source">Origen de negocio (módulo, tipo, id): parte de la identidad.</param>
/// <param name="RecipientRole">Rol estable del destinatario: parte de la identidad (el email no).</param>
/// <param name="Template">
/// Variables tipadas del template del propósito (ZH-COMMUNICATIONS-TEMPLATES-01): el módulo origen
/// aporta datos, nunca asunto/HTML/texto. La cola resuelve el template (default u override de la
/// empresa) y lo renderiza AL ENCOLAR.
/// </param>
/// <param name="MaxRetries">Override explícito; si es null se copia del perfil resuelto para el alcance.</param>
public sealed record CommunicationRequest(
    CommunicationScope Scope,
    string Purpose,
    CommunicationSource Source,
    CommunicationRecipientRole RecipientRole,
    string? RecipientName,
    string RecipientEmail,
    ICommunicationTemplateModel Template,
    IReadOnlyCollection<QueueCommunicationAttachmentDto>? Attachments = null,
    CommunicationChannel Channel = CommunicationChannel.Email,
    CommunicationPriority Priority = CommunicationPriority.Normal,
    DateTime? ScheduledAtUtc = null,
    int? MaxRetries = null
);
