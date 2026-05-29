using ERP.Application.Common;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.Infrastructure.Services;

public sealed class OnboardingCacheInvalidator : IOnboardingCacheInvalidator
{
    private readonly IMemoryCache _cache;
    public OnboardingCacheInvalidator(IMemoryCache cache) => _cache = cache;
    public void InvalidateCompany(Guid companyId) => _cache.Remove($"onboarding:{companyId}");
}
