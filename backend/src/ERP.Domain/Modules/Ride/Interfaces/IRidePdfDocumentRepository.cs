using ERP.Domain.Modules.Ride.Entities;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Domain.Modules.Ride.Interfaces;

public interface IRidePdfDocumentRepository
{
    /// <summary>
    /// Busca el registro de una huella (fingerprint) exacta — es la operación que sustenta la
    /// estrategia de cache y el índice único de ADR-025 §14 (H4: sin duplicados bajo concurrencia).
    /// </summary>
    Task<RidePdfDocument?> GetByFingerprintAsync(
        Guid tenantId,
        Guid electronicDocumentId,
        RideContentHash sourceXmlHash,
        string templateVersion,
        string brandingVersion,
        string rendererVersion,
        string rideSpecificationVersion,
        CancellationToken ct = default
    );

    Task AddAsync(RidePdfDocument document, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
