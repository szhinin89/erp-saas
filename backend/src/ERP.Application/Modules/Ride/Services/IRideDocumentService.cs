using ERP.Application.Common;
using ERP.Application.Modules.Ride.DTOs;

namespace ERP.Application.Modules.Ride.Services;

/// <summary>
/// Internal pipeline facade used by the public HTTP query handlers and the verified background
/// entry. External consumers never invoke this service directly (ADR-025 background addendum).
/// </summary>
public interface IRideDocumentService
{
    Task<Result<RideGenerationResultDto>> GetOrGenerateAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceEntityId,
        CancellationToken ct = default
    );

    Task<Result<RideGenerationResultDto>> RegenerateAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        Guid sourceEntityId,
        CancellationToken ct = default
    );
}
