using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Auth.UseCases.PlatformOwnerSetup;

/// <summary>
/// Creates the internal platform owner (Subscriber + Company) during first-run provisioning.
/// Called once by the "Crear-PlatformOperator.ps1" script before creating the platform operator user.
/// NOT created automatically on startup — must be called explicitly via POST /api/setup/platform-owner.
/// </summary>
public sealed record PlatformOwnerSetupCommand(
    string SetupToken,
    string PlatformName,
    string TaxId,
    string LegalName,
    string MainAddress,
    string Timezone,
    string Email,
    string? TradeName = null,
    string PreferredLanguage = "es"
) : IRequest<Result<PlatformOwnerSetupResultDto>>;

public sealed record PlatformOwnerSetupResultDto(
    Guid   SubscriberId,
    Guid   CompanyId,
    string PlatformName,
    string TaxId,
    string Slug,
    string Message);
