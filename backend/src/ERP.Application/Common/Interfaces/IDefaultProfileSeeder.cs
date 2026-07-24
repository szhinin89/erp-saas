namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Seeds the default access profiles for a newly created tenant. Currently seeds only "DataEntry";
/// additional profiles (e.g. Facturador, Bodeguero, Contador) are a pending product decision, not
/// yet implemented. Invoked exclusively from <c>ERP.Infrastructure.Services.CompanyProvisioningService</c>
/// — the single production entry point that creates a Company. No handler should call it directly.
/// Implementation: <c>ERP.Infrastructure.Seeding.DefaultProfileSeeder</c>.
/// </summary>
public interface IDefaultProfileSeeder
{
    /// <summary>
    /// Creates the default profiles and their permissions for <paramref name="tenantId"/>.
    /// Idempotent — safe to call on a tenant that already has the profiles.
    /// </summary>
    Task SeedForTenantAsync(Guid tenantId, Guid actorId, CancellationToken cancellationToken = default);
}
