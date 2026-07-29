namespace Platform.Contracts.Integration.Dtos;

/// <summary>
/// Status snapshot of a tenant returned by the ERP's public integration API.
/// </summary>
public sealed record TenantStatusResponse(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    string PreferredLanguage,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
