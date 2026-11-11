using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;

namespace ERP.Application.Common;

public interface ICompanyProvisioningService
{
    /// <summary>
    /// Auto-sanación (login): garantiza una empresa activa para el tenant. La empresa que cree
    /// NO recibe los perfiles operativos iniciales.
    /// </summary>
    Task<Company> EnsureDefaultCompanyAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// First Run: crea la empresa inicial de una instalación nueva, con los perfiles operativos
    /// iniciales (Facturador y Contador). Misma atomicidad que <see cref="EnsureDefaultCompanyAsync"/>.
    /// </summary>
    Task<Company> EnsureInitialCompanyAsync(
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
        string? website = null,
        CancellationToken cancellationToken = default,
        InitialCompanyAdministrator? initialAdministrator = null
    );
}
