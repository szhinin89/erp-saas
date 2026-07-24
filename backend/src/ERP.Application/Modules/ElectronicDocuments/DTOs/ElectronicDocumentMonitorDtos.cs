namespace ERP.Application.Modules.ElectronicDocuments.DTOs;

/// <summary>Fila de la tabla principal del Monitor — proyección de solo lectura, sin datos comerciales propios del agregado.</summary>
public sealed record ElectronicDocumentListItemDto(
    Guid Id,
    DateTime CreatedAt,
    string DocumentType,
    string? DocumentNumber,
    Guid CompanyId,
    string CompanyName,
    string? CounterpartyName,
    string CurrentState,
    string? Environment,
    string? AccessKey,
    int RetryCount,
    DateTime? UpdatedAt,
    /// <summary>Último mensaje/motivo relevante (<c>ElectronicDocument.LastError</c>) — null si el documento no tiene ninguno registrado (en proceso o autorizado).</summary>
    string? LastMessage);

public sealed record ElectronicDocumentsListResponse(
    IReadOnlyList<ElectronicDocumentListItemDto> Items,
    int Total,
    int PageNumber,
    int PageSize);

/// <summary>Un evento del timeline — reconstruido desde <c>ElectronicDocumentAudit</c> (append-only), nunca inventado.</summary>
public sealed record ElectronicDocumentTimelineEventDto(
    string Action,
    string? FromState,
    string ToState,
    DateTime OccurredAtUtc,
    string UserName,
    double? DurationSinceLastMinutes);

/// <summary>
/// Detalle de Monitor — datos propios de esta pantalla (compañía, documento de origen) más el
/// <see cref="Diagnostic"/> genérico y reutilizable (estado, mensajes SRI, timeline, info
/// técnica, disponibilidad de XML) que también consumen otros módulos vía
/// <c>GetElectronicDocumentDiagnosticBySourceQuery</c>. No duplica campos que ya vienen en
/// <see cref="Diagnostic"/> (antes existían sueltos aquí: Error/Timeline/XmlXxxAvailable — ver
/// ADR-024).
/// </summary>
public sealed record ElectronicDocumentDetailDto(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string CompanyTaxId,
    string DocumentType,
    string SourceModule,
    Guid SourceEntityId,
    string? DocumentNumber,
    string? CounterpartyName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? Observations,
    ElectronicDocumentDiagnosticDto Diagnostic);

public sealed record ElectronicDocumentsDashboardDto(
    int Pending,
    int Signed,
    int Sent,
    int Authorized,
    int Rejected,
    int Error,
    int AuthorizedToday,
    int ErrorsToday,
    double? AverageAuthorizationMinutes,
    int PendingRetries,
    /// <summary>Documentos creados hoy, sin importar su estado — distinto de <see cref="AuthorizedToday"/>.</summary>
    int TotalToday);
