// CA1711: 'AuditedPermission' suffix matches business domain semantics.
#pragma warning disable CA1711
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using MediatR;
using System.Text.Json.Serialization;

namespace ERP.Application.Access.UseCases.Permissions;

public record GetProfilePermissionAuditQuery(Guid ProfileId)
    : IRequest<Result<ProfilePermissionAuditDto>>;

public sealed record ProfilePermissionAuditDto(
    Guid ProfileId,
    string ProfileName,
    IReadOnlyList<AuditedPermission> Permissions
);

public sealed record AuditedPermission(
    string PermissionKey,
    bool IsAllowed,
    PermissionAuditStatus AuditStatus,
    string? Note
);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PermissionAuditStatus
{
    Effective = 0,
    BlockedByPlan = 1,
    UnknownPrefix = 2,
}

public sealed class GetProfilePermissionAuditHandler
    : IRequestHandler<GetProfilePermissionAuditQuery, Result<ProfilePermissionAuditDto>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public GetProfilePermissionAuditHandler(IAccessRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    public async Task<Result<ProfilePermissionAuditDto>> Handle(
        GetProfilePermissionAuditQuery request,
        CancellationToken cancellationToken
    )
    {
        var tenantId = _currentTenant.TenantId;

        var profile = await _repo.GetProfileByIdAsync(
            tenantId,
            request.ProfileId,
            cancellationToken
        );
        if (profile is null)
            return Result<ProfilePermissionAuditDto>.NotFound("Perfil no encontrado.");

        var dbPerms = await _repo.GetProfilePermissionsAsync(
            tenantId,
            request.ProfileId,
            cancellationToken
        );

        var audited = dbPerms
            .Select(p => new AuditedPermission(
                p.PermissionKey,
                p.IsAllowed,
                PermissionAuditStatus.Effective,
                null
            ))
            .OrderBy(p => p.PermissionKey)
            .ToList();

        return Result<ProfilePermissionAuditDto>.Success(
            new ProfilePermissionAuditDto(profile.Id, profile.Name, audited)
        );
    }
}
