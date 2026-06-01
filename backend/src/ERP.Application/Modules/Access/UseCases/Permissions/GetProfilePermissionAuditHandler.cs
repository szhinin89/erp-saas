using System.Text.Json.Serialization;
using ERP.Application.Access;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Access.UseCases.Permissions;

public record GetProfilePermissionAuditQuery(Guid ProfileId)
    : IRequest<Result<ProfilePermissionAuditDto>>;

public sealed record ProfilePermissionAuditDto(
    Guid   ProfileId,
    string ProfileName,
    IReadOnlyList<AuditedPermission> Permissions);

public sealed record AuditedPermission(
    string PermissionKey,
    bool   IsAllowed,
    PermissionAuditStatus AuditStatus,
    string? Note);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PermissionAuditStatus
{
    /// <summary>Assigned AND allowed by plan — will be effective for users.</summary>
    Effective = 0,
    /// <summary>Assigned but blocked by plan — phantom: never effective for users.</summary>
    BlockedByPlan = 1,
    /// <summary>Assigned with unknown prefix — allowed by policy, not plan-gated.</summary>
    UnknownPrefix = 2,
}

public sealed class GetProfilePermissionAuditHandler
    : IRequestHandler<GetProfilePermissionAuditQuery, Result<ProfilePermissionAuditDto>>
{
    private readonly IAccessRepository              _repo;
    private readonly ICurrentSubscriber             _currentSubscriber;
    private readonly IPermissionEntitlementValidator _validator;

    public GetProfilePermissionAuditHandler(
        IAccessRepository repo,
        ICurrentSubscriber currentSubscriber,
        IPermissionEntitlementValidator validator)
    {
        _repo              = repo;
        _currentSubscriber = currentSubscriber;
        _validator         = validator;
    }

    public async Task<Result<ProfilePermissionAuditDto>> Handle(
        GetProfilePermissionAuditQuery request, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;

        var profile = await _repo.GetProfileByIdAsync(subscriberId, request.ProfileId, ct);
        if (profile is null)
            return Result<ProfilePermissionAuditDto>.NotFound("Perfil no encontrado.");

        var dbPerms = await _repo.GetProfilePermissionsAsync(subscriberId, request.ProfileId, ct);

        // Validate all assigned keys against the plan
        var keys            = dbPerms.Select(p => p.PermissionKey).ToList();
        var validationResults = await _validator.ValidateAsync(subscriberId, keys, ct);
        var validationMap   = validationResults.ToDictionary(
            v => v.PermissionKey, v => v, StringComparer.OrdinalIgnoreCase);

        var audited = dbPerms.Select(p =>
        {
            validationMap.TryGetValue(p.PermissionKey, out var vr);
            var status = vr?.Status switch
            {
                PermissionValidationStatus.BlockedByPlan => PermissionAuditStatus.BlockedByPlan,
                PermissionValidationStatus.UnknownPrefix => PermissionAuditStatus.UnknownPrefix,
                _                                        => PermissionAuditStatus.Effective,
            };
            var note = status == PermissionAuditStatus.BlockedByPlan
                ? vr!.Reason
                : status == PermissionAuditStatus.UnknownPrefix
                    ? vr!.Reason
                    : null;

            return new AuditedPermission(p.PermissionKey, p.IsAllowed, status, note);
        }).OrderBy(p => p.AuditStatus).ThenBy(p => p.PermissionKey).ToList();

        return Result<ProfilePermissionAuditDto>.Success(
            new ProfilePermissionAuditDto(profile.Id, profile.Name, audited));
    }
}
