namespace Platform.Contracts.Integration.Dtos;

/// <summary>
/// Request to provision a new tenant in the ERP via the public integration API.
/// </summary>
public sealed record TenantProvisionRequest(
    string Name,
    string Slug,
    string? PreferredLanguage = "es"
);
