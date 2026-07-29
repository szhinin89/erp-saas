namespace Platform.Contracts.Integration.Dtos;

/// <summary>
/// Request to provision a new company under a tenant in the ERP via the public integration API.
/// </summary>
public sealed record CompanyProvisionRequest(
    Guid TenantId,
    string TaxId,
    string LegalName,
    string MainAddress,
    Guid CreatedByUserId,
    string CreatorRole = "Admin",
    string? TradeName = null,
    string? Email = null,
    string? Phone = null,
    string CountryCode = "ECU",
    string Timezone = "America/Guayaquil",
    string CurrencyCode = "USD",
    string? BrandingJson = null
);
