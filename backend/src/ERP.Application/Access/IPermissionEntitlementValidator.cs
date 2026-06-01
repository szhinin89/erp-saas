using ERP.Application.Common;

namespace ERP.Application.Access;

/// <summary>
/// Validates whether a permission key is allowed by the subscriber's commercial plan,
/// using the same logic as <see cref="EffectivePermissionKeysProvider"/> — no parallel rules.
///
/// Use this at the POINT OF ASSIGNMENT (UpsertProfilePermissions) so phantom permissions
/// never reach the database. The runtime gate in EffectivePermissionKeysProvider remains
/// as defense-in-depth.
/// </summary>
public interface IPermissionEntitlementValidator
{
    /// <summary>
    /// Validates a set of permission keys against the subscriber's plan entitlements.
    /// Returns per-key results classified as Allowed, BlockedByPlan, or UnknownPrefix.
    /// </summary>
    Task<IReadOnlyList<PermissionValidationResult>> ValidateAsync(
        Guid subscriberId,
        IEnumerable<string> permissionKeys,
        CancellationToken ct = default);

    /// <summary>
    /// Quick single-key check. Returns Allowed, BlockedByPlan, or UnknownPrefix.
    /// </summary>
    Task<PermissionValidationResult> ValidateOneAsync(
        Guid subscriberId,
        string permissionKey,
        CancellationToken ct = default);
}

public sealed record PermissionValidationResult(
    string PermissionKey,
    PermissionValidationStatus Status,
    string? Reason = null)
{
    public bool IsAllowed => Status == PermissionValidationStatus.Allowed
                          || Status == PermissionValidationStatus.UnknownPrefix;
}

public enum PermissionValidationStatus
{
    /// <summary>Permission is allowed: module is included in the subscriber's plan.</summary>
    Allowed = 0,

    /// <summary>
    /// Permission is blocked: the associated module is NOT in the subscriber's plan.
    /// Saving this permission would create a phantom (stored but never effective).
    /// </summary>
    BlockedByPlan = 1,

    /// <summary>
    /// Permission key has an unknown prefix — not mapped to any commercial module.
    /// Current policy: allow (same as EffectivePermissionKeysProvider behavior).
    /// Flagged for awareness so admins know the plan-gating is not applicable.
    /// </summary>
    UnknownPrefix = 2,
}
