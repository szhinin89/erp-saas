namespace ERP.Application.Modules.ElectronicDocuments.DTOs;

/// <summary>
/// Un mensaje SRI real e individual — nunca resumido, traducido ni reescrito.
/// <see cref="Code"/>/<see cref="AdditionalInfo"/> son opcionales porque el SRI no siempre los
/// envía; <see cref="Message"/> siempre existe. <see cref="MessageType"/> refleja el literal de
/// <c>&lt;tipo&gt;</c> del SRI (p.ej. "ERROR", "ADVERTENCIA") — o <c>"ERROR"</c> como único
/// fallback documentado si el mensaje no vino de una respuesta SRI real y se sintetizó desde
/// <c>ElectronicDocument.LastError</c> (ver <see cref="ElectronicDocumentDiagnosticAssembler"/>).
/// </summary>
public sealed record ElectronicDocumentMessageDto(
    string? Code,
    string MessageType,
    string Message,
    string? AdditionalInfo,
    DateTime OccurredAtUtc
);

/// <summary>Datos técnicos "solo para soporte" — nunca para decisiones de negocio del módulo consumidor.</summary>
public sealed record ElectronicDocumentTechnicalInfoDto(
    string? AccessKey,
    string? AuthorizationNumber,
    string? Environment,
    DateTime? AuthorizationDate,
    int RetryCount,
    DateTime? LastAttemptUtc,
    string? CorrelationId
);

/// <summary>
/// Contrato único y reutilizable del diagnóstico de un documento electrónico — consumido por
/// cualquier módulo del ERP (Monitor, Ventas, y a futuro Retenciones/Notas/Guías cuando tengan
/// emisión activa) a través de un único componente frontend
/// (<c>ElectronicDocumentDiagnosticPanel</c>). No conoce nada del módulo de origen (sin
/// CompanyName/DocumentNumber/etc. — eso es responsabilidad de cada consumidor, que ya lo sabe).
/// </summary>
public sealed record ElectronicDocumentDiagnosticDto(
    string CurrentState,
    string? Environment,
    DateTime? LastAttemptUtc,
    IReadOnlyList<ElectronicDocumentMessageDto> Messages,
    IReadOnlyList<ElectronicDocumentTimelineEventDto> Timeline,
    ElectronicDocumentTechnicalInfoDto TechnicalInfo,
    bool XmlDraftAvailable,
    bool XmlSignedAvailable,
    bool XmlAuthorizedAvailable
);
