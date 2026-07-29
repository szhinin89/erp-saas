namespace ERP.Application.Access.Caching;

public sealed record PermissionsCacheReadResult(
    IReadOnlyList<string>? Keys,
    PermissionsCacheMissReason? MissReason
);
