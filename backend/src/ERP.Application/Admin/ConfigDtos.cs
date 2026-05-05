namespace ERP.Application.Admin;

public sealed record ConfigEntryDto(
    Guid Id,
    Guid TenantId,
    string Scope,
    string? Module,
    string? Feature,
    string Key,
    string Value,
    string DataType,
    DateTime? UpdatedAt,
    Guid? UpdatedBy);

public sealed record ResolvedConfigValueDto(
    Guid TenantId,
    string Key,
    string? Module,
    string? Feature,
    string ScopeResolved,
    string Value,
    string DataType);

