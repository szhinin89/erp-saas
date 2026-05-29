using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Onboarding.GetOnboardingContext;

public sealed record GetOnboardingContextQuery : IRequest<Result<OnboardingContextDto>>;

public sealed record OnboardingContextDto(
    Guid    CompanyId,
    bool    OnboardingCompleted,
    string  OperationalStatus,

    // Current company data (possibly provisional)
    string  CurrentTaxId,
    bool    IsProvisionalTaxId,
    string  CurrentLegalName,
    string  CurrentAddress,
    string? CurrentEmail,
    string? CurrentPhone,

    // Subscriber data for autofill (UX convenience — no structural dependency)
    string? SubscriberName,
    string  PreferredLanguage);
