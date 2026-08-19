namespace ERP.Application.Modules.Configuration.DTOs;

public sealed record ConfigurationChangeLogDto(
    Guid Id,
    string Scope,
    Guid ScopeId,
    string? Key,
    string EntityType,
    Guid? EntityId,
    string FieldName,
    string? OldValue,
    string? NewValue,
    string ValueType,
    Guid ChangedBy,
    DateTime ChangedAtUtc,
    string Source,
    string? Reason,
    bool IsSensitive
);

public sealed record ConfigurationChangeLogPageDto(
    IReadOnlyList<ConfigurationChangeLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);
