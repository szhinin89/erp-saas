using ERP.Domain.Modules.Ride.Enums;

namespace ERP.Application.Modules.Ride.Storage;

/// <summary>
/// Convención de ruta del PDF en <c>IFileStorage</c> (ADR-025 §15) — mismo estilo de
/// segmentación que <c>ElectronicDocumentStorageNamingStrategy</c> (tenant primero). La versión
/// de plantilla forma parte de la ruta para que huellas distintas de un mismo documento nunca
/// se pisen entre sí.
/// </summary>
public interface IRidePdfStorageNamingStrategy
{
    string BuildRelativePath(
        Guid tenantId, RideDocumentType documentType, Guid electronicDocumentId, string templateVersion);
}
