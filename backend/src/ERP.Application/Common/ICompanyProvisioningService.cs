using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;

namespace ERP.Application.Common;

public interface ICompanyProvisioningService
{
    Task<Company> EnsureDefaultCompanyAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default
    );

    Task<Company> CreateManagedCompanyAsync(
        Guid tenantId,
        string ruc,
        string legalName,
        string mainAddress,
        Guid createdByUserId,
        string creatorRole,
        string? tradeName = null,
        string? email = null,
        string? phone = null,
        string countryCode = "ECU",
        string timezone = "America/Guayaquil",
        string currencyCode = "USD",
        string? brandingJson = null,
        string? website = null,
        CancellationToken cancellationToken = default
    );
}
