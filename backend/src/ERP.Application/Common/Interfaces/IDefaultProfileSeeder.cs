namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Seeds the baseline access profiles of a newly created company (SEC-05: profiles belong to
/// <c>TenantId + CompanyId</c>). Currently seeds only "DataEntry". Operational profiles are
/// company-configurable through AccessProfile and are intentionally not seeded as fixed product
/// roles. Invoked exclusively from the company bootstrap (<c>AccessBootstrapStep</c>) — no handler
/// should call it directly. Implementation: <c>ERP.Infrastructure.Seeding.DefaultProfileSeeder</c>.
/// </summary>
public interface IDefaultProfileSeeder
{
    /// <summary>
    /// Creates the default profiles and their permissions for <paramref name="companyId"/>.
    /// Idempotent — safe to call on a company that already has the profiles.
    /// </summary>
    Task SeedForCompanyAsync(
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        CancellationToken cancellationToken = default
    );
}
