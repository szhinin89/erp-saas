using ERP.Application.Common;
using ERP.Domain.Modules.Ride.ValueObjects;

namespace ERP.Application.Modules.Ride.Branding;

/// <summary>
/// Resuelve el branding de un RIDE (ADR-025 §12) — nunca lee archivos de disco directamente
/// (la implementación real vive sobre <c>org_settings</c> + <c>IFileStorage</c>, Fase 8 del plan).
/// <c>branchId</c>/<c>emissionPointId</c> ya están presentes desde v1.0 para soportar la
/// jerarquía completa sin cambiar esta firma.
/// </summary>
public interface IRideBrandingProvider
{
    Task<Result<RideBranding>> GetAsync(
        Guid tenantId,
        Guid companyId,
        Guid? branchId,
        Guid? emissionPointId,
        CancellationToken ct = default
    );
}
