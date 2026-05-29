using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Modules.Onboarding.CompleteCompanyOnboarding;

/// <summary>
/// Completes company onboarding: updates fiscal data, bootstraps ERP infrastructure,
/// and marks OnboardingCompleted = true.
/// All steps are idempotent and safe to retry.
/// </summary>
public sealed class CompleteCompanyOnboardingHandler
    : IRequestHandler<CompleteCompanyOnboardingCommand, Result<CompanyOnboardingResultDto>>
{
    private readonly ICompanyRepository          _companies;
    private readonly ICompanyBootstrapService    _bootstrap;
    private readonly IOnboardingCacheInvalidator _cacheInvalidator;
    private readonly ICurrentCompany             _currentCompany;
    private readonly ICurrentSubscriber          _currentSubscriber;
    private readonly ICurrentUser                _currentUser;
    private readonly ILogger<CompleteCompanyOnboardingHandler> _log;

    public CompleteCompanyOnboardingHandler(
        ICompanyRepository companies,
        ICompanyBootstrapService bootstrap,
        IOnboardingCacheInvalidator cacheInvalidator,
        ICurrentCompany currentCompany,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser,
        ILogger<CompleteCompanyOnboardingHandler> log)
    {
        _companies         = companies;
        _bootstrap         = bootstrap;
        _cacheInvalidator  = cacheInvalidator;
        _currentCompany    = currentCompany;
        _currentSubscriber = currentSubscriber;
        _currentUser       = currentUser;
        _log               = log;
    }

    public async Task<Result<CompanyOnboardingResultDto>> Handle(
        CompleteCompanyOnboardingCommand request, CancellationToken ct)
    {
        var companyId    = _currentCompany.CompanyId;
        var subscriberId = _currentSubscriber.SubscriberId;
        var actorId      = _currentUser.UserId;

        if (companyId == Guid.Empty)
            return Result<CompanyOnboardingResultDto>.Failure("No hay empresa activa en la sesión.");

        var company = await _companies.GetTrackedByIdForSubscriberAsync(companyId, subscriberId, ct);
        if (company is null)
            return Result<CompanyOnboardingResultDto>.NotFound("Empresa no encontrada.");

        // Idempotency guard
        if (company.OnboardingCompleted)
        {
            _log.LogDebug("CompleteOnboarding: company={CompanyId} already completed", companyId);
            return Result<CompanyOnboardingResultDto>.Success(new CompanyOnboardingResultDto(
                companyId, company.Ruc, company.LegalName, company.OperationalStatus.ToString()));
        }

        // TaxId uniqueness check
        var taxId = request.TaxId.Trim();
        if (!string.Equals(company.Ruc, taxId, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _companies.GetByRucAsync(taxId, ct);
            if (existing is not null && existing.Id != companyId)
                return Result<CompanyOnboardingResultDto>.Failure(
                    $"El RUC '{taxId}' ya está registrado en otra empresa.");
        }

        _log.LogInformation("CompleteOnboarding: company={CompanyId}", companyId);

        // Step 1 — Update profile
        company.UpdateProfile(
            legalName:    request.LegalName,
            tradeName:    request.TradeName,
            mainAddress:  request.MainAddress,
            phone:        request.Phone,
            email:        request.Email,
            countryCode:  company.CountryCode,
            timezone:     company.Timezone,
            currencyCode: company.CurrencyCode,
            logoUrl:      company.LogoUrl,
            brandingJson: company.BrandingJson,
            isActive:     true);

        // Step 2 — Promote TaxId to official
        company.UpdateTaxId(taxId, isProvisional: false, TaxIdStatus.Verified);
        await _companies.SaveChangesAsync(ct);

        // Step 3 — Bootstrap ERP infrastructure (idempotent)
        await _bootstrap.BootstrapCompanyAsync(subscriberId, companyId, actorId, ct);

        // Step 4 — Mark complete
        company.CompleteOnboarding();
        await _companies.SaveChangesAsync(ct);

        // Step 5 — Invalidate middleware cache
        _cacheInvalidator.InvalidateCompany(companyId);

        _log.LogInformation("CompleteOnboarding: DONE company={CompanyId} taxId={TaxId}", companyId, taxId);

        return Result<CompanyOnboardingResultDto>.Success(new CompanyOnboardingResultDto(
            companyId, company.Ruc, company.LegalName, company.OperationalStatus.ToString()));
    }
}
