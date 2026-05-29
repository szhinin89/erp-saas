namespace ERP.Application.Common;

/// <summary>
/// Removes the onboarding status cache entry for a company.
/// Called by CompleteOnboardingHandler after successful onboarding so that
/// the next request passes through the CompanyOnboardingMiddleware without the cached "false".
/// </summary>
public interface IOnboardingCacheInvalidator
{
    void InvalidateCompany(Guid companyId);
}
