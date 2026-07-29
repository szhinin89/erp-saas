namespace Platform.Contracts.Integration.Dtos;

/// <summary>
/// Status snapshot of a company returned by the ERP's public integration API.
/// </summary>
public sealed record CompanyStatusResponse(
    Guid Id,
    Guid TenantId,
    string LegalName,
    string TaxId,
    bool IsActive,
    bool OnboardingCompleted,
    string OperationalStatus,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
