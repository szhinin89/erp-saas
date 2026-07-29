using ERP.Application.Modules.Ride.Storage;
using ERP.Domain.Modules.Ride.Enums;

namespace ERP.Infrastructure.Ride.Storage;

/// <summary>
/// Convención: <c>ride/{tenantId:N}/{documentType}/{electronicDocumentId:N}/{templateVersion}.pdf</c>
/// (ADR-025 §15). Pura, sin I/O — a diferencia del resto del walking skeleton de la Fase 4, se
/// implementa completa desde ahora porque no requiere generación real de PDF para ser correcta,
/// mismo criterio que <c>ElectronicDocumentStorageNamingStrategy</c>.
/// </summary>
public sealed class RidePdfStorageNamingStrategy : IRidePdfStorageNamingStrategy
{
    public string BuildRelativePath(
        Guid tenantId,
        RideDocumentType documentType,
        Guid electronicDocumentId,
        string templateVersion
    ) =>
        string.Join(
            '/',
            "ride",
            tenantId.ToString("N"),
            documentType.ToString().ToLowerInvariant(),
            electronicDocumentId.ToString("N"),
            $"{templateVersion}.pdf"
        );
}
