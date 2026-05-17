namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Orchestrates all default data creation when a new tenant/company is registered.
///
/// Called automatically by:
///   - <c>SuperAdminCreateTenantWithAdminHandler</c>
///   - <c>RegisterTenantWithAdminHandler</c>
///
/// Add new onboarding steps by adding a private method to
/// <c>ERP.Infrastructure.Seeding.TenantOnboardingService</c>
/// and calling it from <see cref="OnboardAsync"/>.
///
/// Current steps (in order):
///   1. Default access profiles  — Facturador / Bodeguero / Contador
///   2. Consumidor Final customer — CI 9999999999
///   3. Main branch              — Sucursal Principal
///   4. Main warehouse           — Bodega Principal (linked to main branch)
/// </summary>
public interface ITenantOnboardingService
{
    /// <summary>
    /// Runs all onboarding steps for <paramref name="tenantId"/>.
    /// Every step is idempotent — safe to call on an already-onboarded tenant.
    /// </summary>
    Task OnboardAsync(Guid tenantId, Guid actorId, CancellationToken ct = default);
}
