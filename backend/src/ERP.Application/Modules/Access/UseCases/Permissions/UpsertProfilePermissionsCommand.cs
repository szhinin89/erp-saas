using MediatR;
using ERP.Application.Common;
using ERP.Application.Access.DTOs;

namespace ERP.Application.Access.UseCases.Permissions;

public record UpsertProfilePermissionsCommand(
    Guid ProfileId,
    IReadOnlyList<PermissionUpsertItem> Items
) : IRequest<Result<object>>;

public record PermissionUpsertItem(
    string PermissionKey,
    bool IsAllowed
);

public record GetProfilePermissionsQuery(Guid ProfileId) : IRequest<Result<ProfilePermissionsDto>>;
public record GetMyPermissionsQuery : IRequest<Result<MyPermissionsDto>>;

