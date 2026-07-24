using ERP.Application.Common;
using ERP.Domain.Modules.Ride.Enums;

namespace ERP.Application.Modules.Ride.Storage;

/// <summary>
/// Persiste el PDF generado vía <c>IFileStorage</c> (ADR-025 §15) — el PDF nunca se almacena en
/// base de datos, solo esta ruta se guarda como metadato en <c>RidePdfDocument</c>.
/// </summary>
public interface IRidePdfStorageService
{
    Task<Result<string>> StoreAsync(
        Guid tenantId,
        RideDocumentType documentType,
        Guid electronicDocumentId,
        string templateVersion,
        byte[] pdf,
        CancellationToken ct = default);
}
