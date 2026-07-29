namespace ERP.Application.Modules.ElectronicDocuments.DTOs;

/// <summary>Proyección de solo lectura del estado del documento electrónico — sin datos comerciales.</summary>
public sealed record ElectronicDocumentDto(
    Guid Id,
    string DocumentType,
    string SourceModule,
    Guid SourceEntityId,
    string CurrentState,
    string? AccessKey,
    string? AuthorizationNumber,
    DateTime? AuthorizationDate,
    int RetryCount,
    DateTime? LastAttemptUtc,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
