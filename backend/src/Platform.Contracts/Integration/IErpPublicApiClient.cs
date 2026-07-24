using Platform.Contracts.Integration.Dtos;

namespace Platform.Contracts.Integration;

/// <summary>
/// Client contract for consuming the ERP's public integration API (<c>/api/integration/v1/*</c>).
/// No implementation exists yet — this is a contract for a future Platform/SaaS layer.
/// </summary>
public interface IErpPublicApiClient
{
    Task<TenantStatusResponse> ProvisionTenantAsync(TenantProvisionRequest request, CancellationToken cancellationToken = default);

    Task<TenantStatusResponse> GetTenantStatusAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TenantStatusResponse> ActivateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TenantStatusResponse> SuspendTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<CompanyStatusResponse> ProvisionCompanyAsync(CompanyProvisionRequest request, CancellationToken cancellationToken = default);

    Task<CompanyStatusResponse> GetCompanyStatusAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<CompanyStatusResponse> ActivateCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<CompanyStatusResponse> SuspendCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
}
