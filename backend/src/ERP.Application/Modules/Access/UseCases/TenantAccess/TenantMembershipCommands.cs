using MediatR;
using ERP.Application.Common;
using ERP.Application.Access.DTOs;

namespace ERP.Application.Access.UseCases.TenantAccess;

public record TenantUpsertMembershipCommand(
    string Email,
    string Role,
    Guid? ProfileId,
    string? FirstName,
    string? LastName,
    string? Password
): IRequest<Result<object>>;

public record TenantRevokeMembershipCommand(string Email) : IRequest<Result<object>>;

public record GetTenantMembershipsQuery(bool OnlyActive) : IRequest<Result<IReadOnlyList<TenantMembershipItemDto>>>;

