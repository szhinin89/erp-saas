using MediatR;
using ERP.Application.Common;
using ERP.Application.Access.DTOs;

namespace ERP.Application.Access.UseCases.Permissions;

public record UpsertProfilePermissionsCommand(
    Guid ProfileId,
    IReadOnlyList<PermissionUpsertItem> Items
) : IRequest<Result<PermissionUpsertResultDto>>;

public record PermissionUpsertItem(
    string PermissionKey,
    bool IsAllowed
);

/// <summary>
/// Explicit result showing which permissions were saved and which were rejected.
/// Backward compat: the <c>success</c> field in ApiResponse is <c>true</c> even when
/// some permissions are rejected — partial success is the norm (admin gets full feedback).
/// </summary>
public sealed record PermissionUpsertResultDto(
    IReadOnlyList<string>             Saved,
    IReadOnlyList<RejectedPermission> Rejected)
{
    /// <summary>True when every submitted permission was saved successfully.</summary>
    public bool AllSaved => Rejected.Count == 0;
}

public sealed record RejectedPermission(
    string PermissionKey,
    string Reason,
    string RejectionCode);

public static class RejectionCodes
{
    public const string BlockedByPlan  = "blocked_by_plan";
    public const string UnknownPrefix  = "unknown_prefix";
    public const string InvalidKey     = "invalid_key";
}

public record GetProfilePermissionsQuery(Guid ProfileId) : IRequest<Result<ProfilePermissionsDto>>;
public record GetMyPermissionsQuery : IRequest<Result<MyPermissionsDto>>;

