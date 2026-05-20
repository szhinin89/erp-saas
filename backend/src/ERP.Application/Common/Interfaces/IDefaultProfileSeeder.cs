namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Seeds the default access profiles (Facturador, Bodeguero, Contador)
/// for a newly created tenant. Called automatically by both tenant-creation handlers.
/// Implementation: <c>ERP.Infrastructure.Seeding.DefaultProfileSeeder</c>.
/// </summary>
public interface IDefaultProfileSeeder
{
    /// <summary>
    /// Creates the three default profiles and their permissions for <paramref name="subscriberId"/>.
    /// Idempotent — safe to call on a tenant that already has the profiles.
    /// </summary>
    Task SeedForTenantAsync(Guid subscriberId, Guid actorId, CancellationToken ct = default);
}
