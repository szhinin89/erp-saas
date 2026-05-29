using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;

namespace ERP.Application.Modules.Onboarding.GetOnboardingContext;

public sealed class GetOnboardingContextHandler
    : IRequestHandler<GetOnboardingContextQuery, Result<OnboardingContextDto>>
{
    private readonly ICompanyRepository    _companies;
    private readonly ISubscriberRepository _subscribers;
    private readonly ICurrentCompany       _currentCompany;
    private readonly ICurrentSubscriber    _currentSubscriber;

    public GetOnboardingContextHandler(
        ICompanyRepository companies,
        ISubscriberRepository subscribers,
        ICurrentCompany currentCompany,
        ICurrentSubscriber currentSubscriber)
    {
        _companies         = companies;
        _subscribers       = subscribers;
        _currentCompany    = currentCompany;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<OnboardingContextDto>> Handle(
        GetOnboardingContextQuery request, CancellationToken ct)
    {
        var companyId    = _currentCompany.CompanyId;
        var subscriberId = _currentSubscriber.SubscriberId;

        var company = await _companies.GetByIdForSubscriberAsync(companyId, subscriberId, ct);
        if (company is null)
            return Result<OnboardingContextDto>.NotFound("Empresa no encontrada.");

        var subscriber = await _subscribers.GetByIdAsync(subscriberId, ct);

        return Result<OnboardingContextDto>.Success(new OnboardingContextDto(
            CompanyId:           companyId,
            OnboardingCompleted: company.OnboardingCompleted,
            OperationalStatus:   company.OperationalStatus.ToString(),
            CurrentTaxId:        company.Ruc,
            IsProvisionalTaxId:  company.IsProvisionalTaxId,
            CurrentLegalName:    company.LegalName,
            CurrentAddress:      company.MainAddress,
            CurrentEmail:        company.Email,
            CurrentPhone:        company.Phone,
            SubscriberName:      subscriber?.Name,
            PreferredLanguage:   subscriber?.PreferredLanguage ?? "es"));
    }
}
