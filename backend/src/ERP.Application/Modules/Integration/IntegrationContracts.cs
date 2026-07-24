using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Enums;

namespace ERP.Application.Modules.Integration;

public sealed record IntegrationTenantCreateRequest(
    string Name,
    string Slug,
    string? PreferredLanguage = "es");

public sealed record IntegrationTenantStatusDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    string PreferredLanguage,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record IntegrationCompanyCreateRequest(
    Guid TenantId,
    string TaxId,
    string LegalName,
    string MainAddress,
    Guid CreatedByUserId,
    string CreatorRole = SecurityRoles.Admin,
    string? TradeName = null,
    string? Email = null,
    string? Phone = null,
    string CountryCode = "ECU",
    string Timezone = "America/Guayaquil",
    string CurrencyCode = "USD",
    string? BrandingJson = null);

public sealed record IntegrationCompanyStatusDto(
    Guid Id,
    Guid TenantId,
    string LegalName,
    string TaxId,
    bool IsActive,
    bool OnboardingCompleted,
    CompanyOperationalStatus OperationalStatus,
    DateTime CreatedAt,
    DateTime UpdatedAt);
