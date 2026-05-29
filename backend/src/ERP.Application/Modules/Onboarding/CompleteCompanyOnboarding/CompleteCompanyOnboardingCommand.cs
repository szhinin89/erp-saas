using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.Onboarding.CompleteCompanyOnboarding;

/// <summary>
/// Completes the onboarding wizard for the active company:
/// 1. Updates Company with real fiscal data (RUC, LegalName, etc.)
/// 2. Runs CompanyBootstrapService (Branch, Warehouse, Establishment, EmissionPoint, DocumentSequences)
/// 3. Marks Company.OnboardingCompleted = true, OperationalStatus = Operational
///
/// All steps run in a single transaction — full rollback on failure.
/// </summary>
public sealed record CompleteCompanyOnboardingCommand(
    /// <summary>If true, pre-fills LegalName/TradeName from Subscriber.Name (UX autofill only — no structural dependency).</summary>
    bool    UseSubscriberData,

    string  TaxId,
    string  LegalName,
    string? TradeName,
    string  MainAddress,
    string? Phone,
    string? Email,

    /// <summary>If true, creates Branch Principal + Warehouse Principal during bootstrap (recommended: true).</summary>
    bool    CreateDefaultBranch = true
) : IRequest<Result<CompanyOnboardingResultDto>>;

public sealed record CompanyOnboardingResultDto(
    Guid   CompanyId,
    string TaxId,
    string LegalName,
    string Status);
