namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Orchestrates all default data creation when a new subscriber/company is registered.
///
/// Called automatically by <c>PlatformCreateSubscriberWithAdminHandler</c>.
///
/// Current steps (in order):
///   1. Default access profiles  — Facturador / Bodeguero / Contador
///   2. Consumidor Final customer — CI 9999999999
///
/// Company-level data (Branch, Warehouse) is handled by <c>ICompanyBootstrapService</c>.
/// </summary>
public interface ISubscriberOnboardingService
{
    /// <summary>
    /// Runs all onboarding steps for <paramref name="subscriberId"/>.
    /// Every step is idempotent — safe to call on an already-onboarded subscriber.
    /// </summary>
    Task OnboardAsync(Guid subscriberId, Guid actorId, CancellationToken ct = default);
}
