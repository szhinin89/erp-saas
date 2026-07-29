using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Entities;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>Mapeo entidad→DTO centralizado — único lugar donde se proyecta ElectronicDocument.</summary>
internal static class ElectronicDocumentMapper
{
    public static ElectronicDocumentDto ToDto(ElectronicDocument document) =>
        new(
            document.Id,
            document.DocumentType.ToString(),
            document.SourceModule,
            document.SourceEntityId,
            document.CurrentState.ToString(),
            document.AccessKey?.Value,
            document.AuthorizationNumber?.Value,
            document.AuthorizationDate,
            document.RetryCount,
            document.LastAttemptUtc,
            document.CreatedAt,
            document.UpdatedAt
        );
}
