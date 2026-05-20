namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Orchestrates all default data creation when a new tenant/company is registered.
///
/// Called automatically by:
///   - <c>SuperAdminCreateSubscriberWithAdminHandler</c>
///   - <c>RegisterSubscriberWithAdminHandler</c>
///
/// Add new onboarding steps by adding a private method to
/// <c>ERP.Infrastructure.Seeding.SubscriberOnboardingService</c>
/// and calling it from <see cref="OnboardAsync"/>.
///
/// Current steps (in order):
///   1. Default access profiles  — Facturador / Bodeguero / Contador
///   2. Consumidor Final customer — CI 9999999999
///   3. Main branch              — Sucursal Principal
///   4. Main warehouse           — Bodega Principal (linked to main branch)
/// </summary>
public interface ISubscriberOnboardingService
{
    /// <summary>
    /// Runs all onboarding steps for <paramref name="subscriberId"/>.
    /// Every step is idempotent — safe to call on an already-onboarded tenant.
    /// </summary>
    Task OnboardAsync(Guid subscriberId, Guid actorId, CancellationToken ct = default);
}
