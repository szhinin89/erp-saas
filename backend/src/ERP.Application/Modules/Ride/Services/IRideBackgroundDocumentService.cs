using ERP.Application.Common;
using ERP.Application.Modules.Ride.DTOs;

namespace ERP.Application.Modules.Ride.Services;

/// <summary>Internal entry for background consumers; explicit scope must match the execution context.</summary>
public interface IRideBackgroundDocumentService
{
    Task<Result<RideGenerationResultDto>> GetOrGenerateAsync(
        Guid tenantId, Guid companyId, string sourceModule, Guid sourceEntityId,
        CancellationToken ct = default);
}

public sealed class RideBackgroundDocumentService(
    IRideDocumentService documents, ICurrentTenant tenant, ICurrentCompany company)
    : IRideBackgroundDocumentService
{
    public Task<Result<RideGenerationResultDto>> GetOrGenerateAsync(
        Guid tenantId, Guid companyId, string sourceModule, Guid sourceEntityId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || companyId == Guid.Empty
            || tenantId != tenant.TenantId || !company.HasCompanyContext
            || companyId != company.CompanyId)
            return Task.FromResult(Result<RideGenerationResultDto>.Forbidden(
                "El alcance solicitado no coincide con el contexto de ejecución."));

        return documents.GetOrGenerateAsync(tenantId, companyId, sourceModule, sourceEntityId, ct);
    }
}
